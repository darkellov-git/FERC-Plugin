using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Ninject;
using FERCPlugin.Core.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace FERCPlugin.Main
{
    /// <summary>
    /// The main application defined in this add-in
    /// </summary>
    /// <seealso cref="IExternalApplication" />
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        private UIControlledApplication _uiControlledApplication;

        /// <summary>
        /// Represents the singleton instance of the dependency injection container.
        /// </summary>
        public static IKernel ServiceLocator { get; private set; }

        /// <summary>
        /// Called when [startup].
        /// </summary>
        /// <param name="application">The UI control application.</param>
        /// <returns></returns>
        /// ReSharper disable once ParameterHidesMember
        public Result OnStartup(UIControlledApplication application)
        {
            this._uiControlledApplication = application;
            _uiControlledApplication.ControlledApplication.ApplicationInitialized +=
                ControlledApplicationOnApplicationInitialized;

            InitializeDependencies();
            InitializeRibbon();

            try
            {
                // TODO: add you code here
            }
            catch (Exception ex)
            {
                TaskDialog.Show($"Error in {nameof(OnStartup)} method", ex.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Called when [shutdown].
        /// </summary>
        /// <param name="application">The application.</param>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // TODO: add you code here
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
            // TODO: Here you can activate your Dockable Pane. (If applicable)
            var appDataProperties = ServiceLocator.Get<IApplicationDataProperties>();
            _uiControlledApplication.ViewActivated += appDataProperties.OnViewActivatedSubscriber;
        }

        private void InitializeRibbon()
        {
            // TODO declare your ribbon items here
            var ribbonItems = new List<RibbonHelper.RibbonButton>
            {
                new RibbonHelper.RibbonButton<RibbonCommand>
                {
                    Text     = Resources.MainButtonName,
                    Tooltip  = Resources.MainButtonTooltip,
                    IconName = "Resources.Icons.testCommand.png",
                },
            };

            RibbonHelper.AddButtons(_uiControlledApplication,
                                    ribbonItems,
                                    ribbonPanelName: Resources.RibbonPanelName,
                                    ribbonTabName: Resources.RibbonTabName);
        }

        private void InitializeDependencies()
        {
            ServiceLocator = new StandardKernel();
            ServiceLocator.Load(new DependencyInjectionManager());
        }
    }
}
