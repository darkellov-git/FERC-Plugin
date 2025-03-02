using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using FERCPlugin.Core.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Application = Autodesk.Revit.ApplicationServices.Application;

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

                VentUnitProcessor processor = new();
                processor.ProcessJson(formattedJsonPath);

                Document familyDoc = revitApp.NewFamilyDocument(templatePath);
                if (familyDoc == null)
                {
                    TaskDialog.Show("Error", "Ошибка при создании семейства.");
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

                VentUnitGeometryBuilder builder = new VentUnitGeometryBuilder(reopenedFamilyDoc, processor.Intake, processor.Exhaust);
                builder.BuildGeometry();


            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Ошибка: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
