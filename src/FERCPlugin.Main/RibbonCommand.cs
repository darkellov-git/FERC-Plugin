using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Ninject;
using FERCPlugin.Core.Helpers;
using FERCPlugin.Core.Models;
using FERCPlugin.Main.Helpers;
using FERCPlugin.UI.ViewModels;
using FERCPlugin.UI.Views;
using System.IO;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

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
            RevitTaskRunner revitTaskRunner = new();

            JsonFormatter jsonFormatter = new();

            string folderPath = @"C:\Users\lopat\source\repos";
            //string folderPath = @"C:\Users\a.lapatniou\Downloads";

            string inputFilePath = Path.Combine(folderPath, "drawing.json");

            try
            {
                jsonFormatter.FormatJson(inputFilePath);

            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Ошибка при форматировании JSON: {ex.Message}");
                return Result.Failed;
            }

            //var mainWindowViewModel = App.ServiceLocator.Get<MainWindowViewModel>();

            //var window = new MainWindow
            //{
            //    DataContext = mainWindowViewModel,
            //    Owner = RevitWindowHandler.GetRevitWindow()
            //};

            //window.Show();

            return Result.Succeeded;
        }
    }
}