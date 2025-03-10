using Newtonsoft.Json.Linq;

namespace FERCPlugin.Core.Models
{
    public class JsonFormatter
    {
        private static readonly string[] ElementsToRemove = new[]
        {
            "partsSpec",
            "automaticSpec",
            "priceTotal",
            "massTotal",
            "partsSpecSkeleton",
            "partsSpecStands",
            "partsSpecPanels",
            "partsSpecFrame"
        };

        public void FormatJson(string inputFilePath)
        {
            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException("Входной JSON файл не найден.", inputFilePath);
            }

            string jsonContent = File.ReadAllText(inputFilePath);
            JObject rootObj = JObject.Parse(jsonContent);

            // Получаем isIntakeBelow из design
            bool isIntakeBelow = rootObj.SelectToken("design.isIntakeBelow")?.Value<bool>() ?? false;

            // Получаем serviceSideIntake из корня объекта
            string serviceSideIntake = rootObj.SelectToken("serviceSideIntake")?.Value<string>() ?? "right";

            // Получаем result.drawing
            JToken drawingToken = rootObj.SelectToken("result.drawing") ?? throw new Exception("В исходном файле не найден объект 'result.drawing'.");

            RemoveUnwantedElements(drawingToken);

            // Преобразуем drawingToken в объект
            if (drawingToken is JObject drawingObj)
            {
                drawingObj["isIntakeBelow"] = isIntakeBelow;
                drawingObj["serviceSideIntake"] = serviceSideIntake; // ✅ Добавляем serviceSideIntake
            }

            string formattedJson = drawingToken.ToString(Newtonsoft.Json.Formatting.Indented);

            string directory = Path.GetDirectoryName(inputFilePath);
            string outputFilePath = Path.Combine(directory, "drawing_formatted.json");

            File.WriteAllText(outputFilePath, formattedJson);
        }


        private void RemoveUnwantedElements(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (string element in ElementsToRemove)
                {
                    if (obj.ContainsKey(element))
                    {
                        obj.Remove(element);
                    }
                }

                foreach (JProperty property in obj.Properties())
                {
                    RemoveUnwantedElements(property.Value);
                }
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    RemoveUnwantedElements(item);
                }
            }
        }
    }
}
