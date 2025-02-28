using Autodesk.Revit.DB;
using FERCPlugin.Core.Helpers;

namespace FERCPlugin.Core.Models;

/// <summary>
/// Singleton instance that stores <see cref="RevitTaskExecutor"/> and
/// active <see cref="Document"/>
/// </summary>
public class ApplicationDataProperties : IApplicationDataProperties
{
    /// <inheritdoc />
    public RevitTaskRunner RevitTaskRunner { get; }

    /// <inheritdoc />
    public Document ActiveDocument { get; set; }

    public event EventHandler OnDocumentSwitched;
    public event EventHandler OnDocumentOpened;

    public ApplicationDataProperties(RevitTaskRunner revitTaskRunner)
    {
        RevitTaskRunner = revitTaskRunner;
    }

    /// <inheritdoc />
    public void OnViewActivatedSubscriber(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
    {
        ActiveDocument = e.Document;

        if (e.PreviousActiveView?.Document != null)
        {
            if (!e.PreviousActiveView.Document.Equals(e.CurrentActiveView.Document))
            {
                OnDocumentSwitched?.Invoke(this, e);
            }
        }
        else
        {
            OnDocumentOpened?.Invoke(this, e);
        }
    }
}
