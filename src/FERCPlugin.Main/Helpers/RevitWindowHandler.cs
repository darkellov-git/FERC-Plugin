using System.Windows;
using System.Windows.Interop;

namespace FERCPlugin.Main.Helpers
{
    /// <summary>
    /// The RevitWindowHandler class provides helper methods related to Revit's main window.
    /// </summary>
    public static class RevitWindowHandler
    {
        /// <summary>
        /// This method returns a current running Revit's <see cref="Window"/> object
        /// </summary>
        public static Window GetRevitWindow()
        {
            HwndSource hwndSource = HwndSource.FromHwnd(Autodesk.Windows.ComponentManager.ApplicationWindow);
            return hwndSource?.RootVisual as Window;
        }
    }
}
