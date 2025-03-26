using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FERCPlugin.Core.Models;
using FERCPlugin.Main.Host;
using System.IO;

namespace FERCPlugin.Main;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class PluginCommands : IExternalCommand {
    private string ExecuteExternal(UIApplication uiApp, RequestDto dto) {

        var revitApp = uiApp.Application;

        // TODO DO NOT MISS TO FIX THE PATH ON SERVER!!!!
        var folderPath = @"C:\Users\user\Downloads";

        // TODO DO NOT MISS TO FIX THE PATH ON SERVER!!!!
        //var templatePath = @"C:\ProgramData\Autodesk\RVT 2025\Family Templates\English\Metric Mechanical Equipment.rft";
        var templatePath = @"C:\ProgramData\Autodesk\RVT 2022\Family Templates\English\Metric Mechanical Equipment.rft";
        var familySavePath = Path.Combine(folderPath, "TEST_Family.rfa");

        Document familyDoc = null;

        try {
            // TODO implement swap items logic for dto instance (not json)

            familyDoc = revitApp.NewFamilyDocument(templatePath)
                ?? throw new Exception("Family creation error");

            using (var tx = new Transaction(familyDoc, "Set Family Category")) {
                tx.Start();
                familyDoc.OwnerFamily.FamilyCategory = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_MechanicalEquipment);
                tx.Commit();
            }

            var builder = new VentUnitGeometryBuilder(familyDoc,
                                                      dto.Intake,
                                                      dto.Exhaust,
                                                      dto.IsIntakeBelow,
                                                      dto.FrameHeight);

            var (intakeElements,
                exhaustElements,
                maxHeightIntake,
                maxHeightExhaust,
                maxWidth) = builder.BuildGeometry();

            // TODO estimate if it's necessary or it would be given
            // by some method which will swap items in case there is
            // this type of utilization exists
            var hasUtilizationCross = dto.Intake.Any(d => d.Category ==
                nameof(VentUnitGroupCategoryEnum.utilization_cross));

            var annotationBuilder = new AnnotationBuilder(familyDoc,
                                                          intakeElements,
                                                          exhaustElements,
                                                          hasUtilizationCross,
                                                          dto.IsIntakeBelow,
                                                          maxHeightIntake,
                                                          maxHeightExhaust,
                                                          maxWidth);
            annotationBuilder.AddAnnotations();

            // TODO fill family with description, name, manufacturer...

            var saveAsOptions = new SaveAsOptions { OverwriteExistingFile = true };
            familyDoc.SaveAs(familySavePath, saveAsOptions);
            familyDoc.Close(false);

            return familySavePath;
        } catch (Exception ex) {
            throw ex;
        }
    }

    public byte[] GetFamily(UIApplication uiApp, RequestDto dto) {
        var familySavePath = ExecuteExternal(uiApp, dto);

        using (var memoryStream = new MemoryStream()) {
            var fileBytes = File.ReadAllBytes(familySavePath);
            memoryStream.Write(fileBytes, 0, fileBytes.Length);

            File.Delete(familySavePath);

            return memoryStream.ToArray();
        }
    }

    public byte[] GetViews(UIApplication uiApp, RequestDto dto) {
        var familySavePath = ExecuteExternal(uiApp, dto);

        // TODO fill with views generation logic

        using (var memoryStream = new MemoryStream()) {
            var fileBytes = File.ReadAllBytes(familySavePath);
            memoryStream.Write(fileBytes, 0, fileBytes.Length);

            File.Delete(familySavePath);

            return memoryStream.ToArray();
        }
    }

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) => Result.Succeeded;
}
