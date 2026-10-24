using enviro.Models;
using System.ComponentModel;
using System.Text.Json;

namespace enviro.Services;

internal interface IConfigService
{
    ConfigModel Config { get; }
    bool IsDirty { get; }
    void Init();
    void Save();
}

/// <summary>
/// Config service responsible for loading and saving config
/// </summary>
internal class ConfigService : IConfigService
{
    public bool IsDirty { get; private set; } = false;

    public ConfigModel Config { get; private set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppLog _log;

    private readonly string CurrentDirectory;

    private readonly string ConfigPath;

    /// <param name="log">Where load and save failures are recorded.</param>
    public ConfigService(IAppLog log)
    {
        _log = log;

        CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ConfigPath = Path.Combine(CurrentDirectory, "config.json");

        Init();
    }

    /// <summary>
    /// Loads config, falling back to defaults when the file is missing, unreadable or invalid.
    /// The defaults are marked dirty so the next save rewrites the broken file.
    /// </summary>
    public void Init()
    {
        var loaded = Load();

        IsDirty = loaded is null;
        Config = loaded ?? new ConfigModel { EnableCorruptedValidation = true };

        Config.PropertyChanged += OnConfigChanged;
    }

    /// <summary>
    /// Saves the config to the file
    /// </summary>
    public void Save()
    {
        try
        {
            var tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Config, JsonOptions));
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Settings are not worth crashing the app on exit; the previous file stays intact.
            _log.Error($"Could not save settings to {ConfigPath}.", ex);
        }
    }

    /// <summary>
    /// Reads the config file, or returns null when it is absent, unreadable or invalid.
    /// </summary>
    private ConfigModel? Load()
    {
        try
        {
            using var openStream = File.OpenRead(ConfigPath);
            return JsonSerializer.Deserialize<ConfigModel>(openStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing file is the ordinary first run; anything else is worth an entry.
            if (ex is not FileNotFoundException)
                _log.Error($"Could not read settings from {ConfigPath}; using defaults.", ex);

            return null;
        }
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
    }
}
