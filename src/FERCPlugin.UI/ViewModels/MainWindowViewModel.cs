using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FERCPlugin.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _someText;
    public string SomeText
    {
        get => _someText;
        set
        {
            if (_someText == value)
                return;

            _someText = value;
            OnPropertyChanged();
        }
    }

    public MainWindowViewModel()
    {
        SomeText = "Hello!";
    }

    #region PropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
