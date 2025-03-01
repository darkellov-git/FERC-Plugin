using Newtonsoft.Json.Linq;

namespace FERCPlugin.Core.Models
{
    public class VentUnitProcessor
    {
        public List<VentUnitItem> Intake { get; private set; } = new();
        public List<VentUnitItem> Exhaust { get; private set; } = new();

        public void ProcessJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException("Файл JSON не найден.", jsonFilePath);
            }

            string jsonContent = File.ReadAllText(jsonFilePath);
            JObject rootObj = JObject.Parse(jsonContent);

            JArray intakeArray = rootObj.SelectToken("intake") as JArray;
            JArray exhaustArray = rootObj.SelectToken("exhaust") as JArray;

            if (intakeArray != null)
            {
                Intake = ParseVentUnitItems(intakeArray);
            }

            if (exhaustArray != null)
            {
                Exhaust = ParseVentUnitItems(exhaustArray);
            }

            RemoveDuplicateItemsFromExhaust();
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
            HashSet<string> intakeIds = new();
            foreach (var item in Intake)
            {
                intakeIds.Add(item.Id);
            }

            Exhaust.RemoveAll(item => intakeIds.Contains(item.Id));
        }
    }
}
