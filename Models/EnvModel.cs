using System.ComponentModel;

namespace enviro.Models;

internal class EnvModel : INotifyPropertyChanged
{
    private string _name;
    private string _path;
    private string _originalPath;
    private EnvironmentalVariableState _state = EnvironmentalVariableState.Unchanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged(nameof(Path));
            }
        }
    }

    public string OrginalPath
    {
        get => _originalPath;
        set
        {
            if (_originalPath != value)
            {
                _originalPath = value;
                OnPropertyChanged(nameof(OrginalPath));
            }
        }
    }

    public EnvironmentalVariableState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged(nameof(State));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
