using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using FERCPlugin.Core.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Newtonsoft.Json.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace FERCPlugin.Main
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RibbonCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Application revitApp = uiApp.Application;

            string folderPath = @"C:\Users\lopat\source\repos";
            string inputFilePath = Path.Combine(folderPath, "drawing.json");
            string formattedJsonPath = Path.Combine(folderPath, "drawing_formatted.json");

            string templatePath = @"C:\ProgramData\Autodesk\RVT 2025\Family Templates\English\Metric Mechanical Equipment.rft";
            string familySavePath = Path.Combine(folderPath, "TEST_Family.rfa");

            try
            {
                JsonFormatter jsonFormatter = new();
                jsonFormatter.FormatJson(inputFilePath);

                bool isIntakeBelow = true;
                string intakeServiceside = "unknown";
                string exhaustServiceSide = "unknown";
                double frameHeight = 0.0;

                if (File.Exists(formattedJsonPath))
                {
                    string jsonContent = File.ReadAllText(formattedJsonPath);
                    JObject rootObj = JObject.Parse(jsonContent);
                    isIntakeBelow = rootObj.SelectToken("isIntakeBelow")?.Value<bool>() ?? false;
                    intakeServiceside = rootObj.SelectToken("serviceSideIntake")?.Value<string>() ?? "unknown";
                    exhaustServiceSide = rootObj.SelectToken("serviceSideExhaust")?.Value<string>() ?? "unknown";
                    frameHeight = rootObj.SelectToken("frameHeight")?.Value<double>() ?? 0.0;
                }

                bool hasUtilizationCross = false;
                if (File.Exists(formattedJsonPath))
                {
                    string jsonContent = File.ReadAllText(formattedJsonPath);
                    JObject rootObj = JObject.Parse(jsonContent);

                    hasUtilizationCross = rootObj
                        .Descendants()
                        .OfType<JObject>()
                        .Any(obj => obj["category"]?.ToString() == "utilization_cross");
                }

                VentUnitProcessor processor = new();
                processor.ProcessJson(formattedJsonPath, hasUtilizationCross, intakeServiceside);

                Document familyDoc = revitApp.NewFamilyDocument(templatePath);
                if (familyDoc == null)
                {
                    TaskDialog.Show("Error", "������ ��� �������� ���������.");
                    return Result.Failed;
                }

                using (Transaction tx = new Transaction(familyDoc, "Set Family Category"))
                {
                    tx.Start();
                    familyDoc.OwnerFamily.FamilyCategory = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_MechanicalEquipment);
                    tx.Commit();
                }

                SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                familyDoc.SaveAs(familySavePath, saveOptions);
                familyDoc.Close(false);

                UIDocument uiFamilyDoc = uiApp.OpenAndActivateDocument(familySavePath);
                Document reopenedFamilyDoc = uiFamilyDoc.Document;

                VentUnitGeometryBuilder builder = new VentUnitGeometryBuilder(reopenedFamilyDoc, processor.Intake, processor.Exhaust, isIntakeBelow, frameHeight, intakeServiceside, exhaustServiceSide);

                var (intakeElements, exhaustElements, maxHeightIntake, maxHeightExhaust, maxWidth) = builder.BuildGeometry();

                AnnotationBuilder annotationBuilder = new AnnotationBuilder(reopenedFamilyDoc, intakeElements, exhaustElements, hasUtilizationCross,
                                                                            isIntakeBelow, maxHeightIntake, maxHeightExhaust, maxWidth, intakeServiceside, exhaustServiceSide);
                annotationBuilder.AddAnnotations();

                //DuctConnectorCreator connectorCreator = new DuctConnectorCreator(reopenedFamilyDoc, flexibleDampers);
                //connectorCreator.CreateConnectors();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"������: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
