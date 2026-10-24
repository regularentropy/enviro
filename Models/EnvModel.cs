using System.ComponentModel;

namespace enviro.Models;

internal class EnvModel : INotifyPropertyChanged
{
    public string Name
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// The name the variable is stored under in Windows: set at load time and after a successful
    /// apply, so a pending rename knows which key to remove.
    /// </summary>
    public string OriginalName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OriginalName));
            }
        }
    }

    public string Path
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Path));
            }
        }
    }

    public string OriginalPath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OriginalPath));
            }
        }
    }

    public EnvironmentalVariableState State
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(State));
            }
        }
    }

    /// <summary>
    /// Indicates whether the path points to a location that does not exist.
    /// This is a diagnostic overlay and is independent of <see cref="State"/>.
    /// </summary>
    public bool IsCorrupted
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsCorrupted));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
