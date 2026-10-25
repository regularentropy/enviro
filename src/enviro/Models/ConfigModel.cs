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
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
