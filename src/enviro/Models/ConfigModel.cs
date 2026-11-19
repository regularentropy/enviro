using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace enviro.Models;

/// <summary>
/// The theme Enviro starts in: follow Windows, or force one of the two palettes.
/// </summary>
/// <remarks>
/// Persisted as a string. A config file without the key reads as <see cref="System"/>, so an older
/// file keeps working.
/// </remarks>
internal enum ThemePreference
{
    /// <summary>
    /// Follow the Windows light/dark setting when the app starts.
    /// </summary>
    System,

    /// <summary>
    /// Always start in the light palette.
    /// </summary>
    Light,

    /// <summary>
    /// Always start in the dark palette.
    /// </summary>
    Dark,
}

/// <summary>
/// The persisted window bounds, as plain numbers so the JSON stays readable and stable.
/// </summary>
internal sealed record WindowBounds(int X, int Y, int Width, int Height);

/// <summary>
/// The persisted sort of one scope's grid: the bound model property and the direction.
/// </summary>
internal sealed record SortPreference(string Property, bool Descending);

internal class ConfigModel : INotifyPropertyChanged
{
    /// <summary>
    /// The window bounds to restore on the next start; null centres the window.
    /// </summary>
    public WindowBounds? WindowBounds
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

    /// <summary>
    /// Whether the window was maximized on the last close.
    /// </summary>
    public bool WindowMaximized
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

    /// <summary>
    /// The index of the tab that was active on the last close (0 = User, 1 = Machine).
    /// </summary>
    public int ActiveTab
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

    /// <summary>
    /// The filter text the User grid had on the last close.
    /// </summary>
    public string UserFilter
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
    } = string.Empty;

    /// <summary>
    /// The filter text the Machine grid had on the last close.
    /// </summary>
    public string MachineFilter
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
    } = string.Empty;

    /// <summary>
    /// The sort the User grid had on the last close; null for the default order.
    /// </summary>
    public SortPreference? UserSort
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

    /// <summary>
    /// The sort the Machine grid had on the last close; null for the default order.
    /// </summary>
    public SortPreference? MachineSort
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

    /// <summary>
    /// The theme to apply at startup; a change takes effect on the next start.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ThemePreference>))]
    public ThemePreference Theme
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

    // New properties default sensibly, so an older config file without them still loads clean.
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
