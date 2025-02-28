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
using Autodesk.Revit.DB;
using System.IO;
using System.Windows;

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
            try
            {
                string ribbonTabName = "FERC"; 
                _uiControlledApplication.CreateRibbonTab(ribbonTabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Ignore if the tab already exists
            }

            RibbonPanel ribbonPanel = _uiControlledApplication.CreateRibbonPanel("FERC", "FERC");

            string buttonText = "Create Family";


            if(ribbonPanel.AddItem(new PushButtonData("cmdCreateFamily", buttonText,
                Assembly.GetExecutingAssembly().Location, "FERCPlugin.Main.RibbonCommand")) is PushButton pushButton)
            {
                string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                string iconPath = Path.Combine(pluginDirectory, "Icon_32.png");

                if (File.Exists(iconPath))
                {
                    pushButton.LargeImage = new BitmapImage(new Uri($"file:///{iconPath}"));
                }
                else
                {
                    System.Windows.MessageBox.Show($"Icon not found at path:\n{iconPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void InitializeDependencies()
        {
            ServiceLocator = new StandardKernel();
            ServiceLocator.Load(new DependencyInjectionManager());
        }

        private static System.Windows.Media.Imaging.BitmapImage GetIcon(string iconPath)
        {
            var uri = new Uri($"pack://application:,,,/{iconPath}");
            return new System.Windows.Media.Imaging.BitmapImage(uri);
        }
    }
}
