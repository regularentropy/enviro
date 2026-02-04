using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace enviro.Models;

internal class ConfigModel : INotifyPropertyChanged
{
    public bool EnableCorruptedValidation
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
