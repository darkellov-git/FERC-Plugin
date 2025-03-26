using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using FERCPlugin.Core.Helpers;
using FERCPlugin.Core.Models;
using FERCPlugin.Main.Host;
using Ninject;
using System.Windows.Media.Imaging;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace FERCPlugin.Main;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class App : IExternalApplication {
    private UIControlledApplication _uiControlledApplication;
    private RevitHttpServer _server;
    private RevitTaskRunner _taskRunner;

    public static IKernel ServiceLocator { get; private set; }

    public Result OnStartup(UIControlledApplication application) {
        _uiControlledApplication = application;
        _uiControlledApplication.ControlledApplication.ApplicationInitialized
            += ControlledApplicationOnApplicationInitialized;

        _uiControlledApplication.Idling += OnIdling;

        InitializeDependencies();

        try {
        } catch (Exception ex) {
            TaskDialog.Show($"Error in {nameof(OnStartup)} method", ex.ToString());
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) {
        try {
            _server?.Stop();
        } catch (Exception ex) {
            TaskDialog.Show($"Error in {nameof(OnShutdown)} method", ex.ToString());
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private void ControlledApplicationOnApplicationInitialized(object sender,
                                                ApplicationInitializedEventArgs e) {
        var appDataProperties = ServiceLocator.Get<IApplicationDataProperties>();
        _uiControlledApplication.ViewActivated
            += appDataProperties.OnViewActivatedSubscriber;

    }

    private void OnIdling(object sender, IdlingEventArgs e) {
        _uiControlledApplication.Idling -= OnIdling;

        var uiapp = sender as UIApplication;

        if (uiapp != null) {
            _taskRunner = new RevitTaskRunner();
            _server = new RevitHttpServer(_taskRunner);
            _server.Start();
        }
    }

    private void InitializeDependencies() {
        ServiceLocator = new StandardKernel();
        ServiceLocator.Load(new DependencyInjectionManager());
    }

    private static BitmapImage GetIcon(string iconPath) {
        var uri = new Uri($"pack://application:,,,/{iconPath}");
        return new BitmapImage(uri);
    }
}
