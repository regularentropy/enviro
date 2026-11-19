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

    /// <summary>
    /// Indicates whether the live registry value differs from the row's baseline, i.e. the
    /// variable changed outside of enviro. This is a diagnostic overlay and is independent of
    /// <see cref="State"/>.
    /// </summary>
    public bool IsDrifted
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsDrifted));
            }
        }
    }

    /// <summary>
    /// The value the registry currently holds for this row while <see cref="IsDrifted"/> is set;
    /// null otherwise.
    /// </summary>
    public string? LiveValue
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(LiveValue));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
