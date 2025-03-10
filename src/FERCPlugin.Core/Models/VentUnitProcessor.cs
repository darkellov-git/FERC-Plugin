using FERCPlugin.Core.Models;
using Newtonsoft.Json.Linq;

public class VentUnitProcessor
{
    public List<VentUnitItem> Intake { get; private set; } = new();
    public List<VentUnitItem> Exhaust { get; private set; } = new();

    public void ProcessJson(string jsonFilePath, bool hasUtilizationCross, string intakeServiceside)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException("Файл JSON не найден.", jsonFilePath);
        }

        string jsonContent = File.ReadAllText(jsonFilePath);
        JObject rootObj = JObject.Parse(jsonContent);

        JArray intakeArray = rootObj.SelectToken("intake") as JArray;
        JArray exhaustArray = rootObj.SelectToken("exhaust") as JArray;

        if (intakeArray != null) Intake = ParseVentUnitItems(intakeArray);
        if (exhaustArray != null) Exhaust = ParseVentUnitItems(exhaustArray);

        RemoveDuplicateItemsFromExhaust();

        if (hasUtilizationCross & intakeServiceside == "left")
        {
            AdjustListsBasedOnUtilization();
        }
    }

    private List<VentUnitItem> ParseVentUnitItems(JArray jsonArray)
    {
        List<VentUnitItem> items = new();
        foreach (JObject obj in jsonArray)
        {
            VentUnitItem item = obj.ToObject<VentUnitItem>();

            if (obj["displayIndex"] == null || !obj["displayIndex"].HasValues)
            {
                item.DisplayIndex = -1;
            }

            if (obj.ContainsKey("children") && obj["children"] is JArray childrenArray)
            {
                item.Children = childrenArray.ToObject<List<VentUnitChild>>();
            }

            items.Add(item);
        }
        return items;
    }

    private void RemoveDuplicateItemsFromExhaust()
    {
        HashSet<string> intakeIds = new(Intake.Select(item => item.Id));
        Exhaust.RemoveAll(item => intakeIds.Contains(item.Id));
    }

    private void AdjustListsBasedOnUtilization()
    {
        int intakeUtilIndex = Intake.FindIndex(item => item.Category.Contains("utilization_cross"));
        int exhaustUtilIndex = Exhaust.FindIndex(item => item.Category.Contains("utilization_cross"));

        if (intakeUtilIndex >= 0 && exhaustUtilIndex >= 0)
        {
            List<VentUnitItem> intakeToMove = Intake.Skip(intakeUtilIndex + 1).ToList();
            Intake.RemoveRange(intakeUtilIndex + 1, intakeToMove.Count);

            List<VentUnitItem> exhaustToMove = Exhaust.Skip(exhaustUtilIndex + 1).ToList();
            Exhaust.RemoveRange(exhaustUtilIndex + 1, exhaustToMove.Count);

            Exhaust.InsertRange(exhaustUtilIndex + 1, intakeToMove);
            Intake.InsertRange(intakeUtilIndex + 1, exhaustToMove);
        }
    }
}
