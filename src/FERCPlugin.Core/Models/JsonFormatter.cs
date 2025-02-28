using Newtonsoft.Json.Linq;

namespace FERCPlugin.Core.Models
{
    public class JsonFormatter
    {
        public void FormatJson(string inputFilePath)
        {
            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException("Входной JSON файл не найден.", inputFilePath);
            }

            string jsonContent = File.ReadAllText(inputFilePath);

            JObject rootObj = JObject.Parse(jsonContent);

            JToken drawingToken = rootObj.SelectToken("result.drawing");
            if (drawingToken == null)
            {
                throw new Exception("В исходном файле не найден объект 'result.drawing'.");
            }

            string formattedJson = drawingToken.ToString(Newtonsoft.Json.Formatting.Indented);

            string directory = Path.GetDirectoryName(inputFilePath);
            string outputFilePath = Path.Combine(directory, "drawing_formatted.json");

            File.WriteAllText(outputFilePath, formattedJson);

            Console.WriteLine($"Преобразованный файл сохранён: {outputFilePath}");
        }
    }
}
 