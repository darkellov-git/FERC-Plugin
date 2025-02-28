using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Ninject;
using FERCPlugin.Core.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace FERCPlugin.Main
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        private UIControlledApplication _uiControlledApplication;

        public static IKernel ServiceLocator { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            this._uiControlledApplication = application;
            _uiControlledApplication.ControlledApplication.ApplicationInitialized +=
                ControlledApplicationOnApplicationInitialized;

            InitializeDependencies();
            InitializeRibbon();

            try
            {
                // Your code here
            }
            catch (Exception ex)
            {
                TaskDialog.Show($"Error in {nameof(OnStartup)} method", ex.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Your shutdown code here
            }
            catch (Exception ex)
            {
                TaskDialog.Show($"Error in {nameof(OnShutdown)} method", ex.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private void ControlledApplicationOnApplicationInitialized(object sender, ApplicationInitializedEventArgs e)
        {
            var appDataProperties = ServiceLocator.Get<IApplicationDataProperties>();
            _uiControlledApplication.ViewActivated += appDataProperties.OnViewActivatedSubscriber;
        }

        private void InitializeRibbon()
        {
            // Step 1: Create a new Ribbon Tab
            try
            {
                string ribbonTabName = "FERC"; // Direct string for the tab name
                _uiControlledApplication.CreateRibbonTab(ribbonTabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Ignore if the tab already exists
            }

            // Step 2: Create a Ribbon Panel
            RibbonPanel ribbonPanel = _uiControlledApplication.CreateRibbonPanel("FERC", "Main Panel");

            // Step 3: Create a PushButton for the Ribbon
            string buttonText = "Create Family"; // Direct string for the button name
            string buttonTooltip = "Allows you to create a new ventilation object family automatically"; // Tooltip string

            // Create PushButton with direct references to the RibbonCommand
            PushButton pushButton = ribbonPanel.AddItem(new PushButtonData("cmdCreateFamily", buttonText,
                Assembly.GetExecutingAssembly().Location, "FERCPlugin.Main.RibbonCommand")) as PushButton;

            if (pushButton != null)
            {
                pushButton.ToolTip = buttonTooltip;
                pushButton.LargeImage = new BitmapImage(new Uri("file:///C:/path/to/your/project/Resources/Icons/Icon_32.png"));
            }
        }

        private void InitializeDependencies()
        {
            ServiceLocator = new StandardKernel();
            ServiceLocator.Load(new DependencyInjectionManager());
        }

        // Helper function to load icon
        private static System.Windows.Media.Imaging.BitmapImage GetIcon(string iconPath)
        {
            var uri = new Uri($"pack://application:,,,/{iconPath}");
            return new System.Windows.Media.Imaging.BitmapImage(uri);
        }
    }
}
