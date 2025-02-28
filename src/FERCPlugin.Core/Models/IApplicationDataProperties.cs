using Autodesk.Revit.DB;
using FERCPlugin.Core.Helpers;

namespace FERCPlugin.Core.Models;

/// <summary>
/// Interface of the object that is responsible for <see cref="RevitTaskRunner"/> handling and
/// active <see cref="Document"/> switching
/// </summary>
public interface IApplicationDataProperties
{
    /// <summary>
    /// Runs Revit related tasks
    /// </summary>
    RevitTaskRunner RevitTaskRunner { get; }

    /// <summary>
    /// Currently active Revit <see cref="Document"/>
    /// </summary>
    Document ActiveDocument { get; set; }

    event EventHandler OnDocumentSwitched;
    event EventHandler OnDocumentOpened;

    /// <summary>
    /// This method subscribes on document switched event to actualize active <see cref="Document"/>
    /// </summary>
    void OnViewActivatedSubscriber(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e);
}
