using enviro.Models;
using System.ComponentModel;
using System.Text.Json;

namespace enviro.Services;

internal interface IConfigService
{
    ConfigModel Config { get; }
    bool IsDirty { get; }

    /// <summary>
    /// Raised after a failed save, carrying the message to show; a surface reports it without a dialog.
    /// </summary>
    event EventHandler<string>? SaveFailed;

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

    /// <summary>Where settings are written: under <c>%APPDATA%</c>, so a read-only install folder can still save.</summary>
    private readonly string ConfigPath;

    /// <summary>The location used before DS-05: read once for migration, never written.</summary>
    private readonly string LegacyConfigPath;

    /// <inheritdoc />
    public event EventHandler<string>? SaveFailed;

    /// <param name="log">Where load and save failures are recorded.</param>
    public ConfigService(IAppLog log)
    {
        _log = log;

        ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Enviro", "config.json");
        LegacyConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        Init();
    }

    /// <summary>
    /// Loads config, falling back to defaults when the file is missing, unreadable or invalid.
    /// The defaults are marked dirty so the next save rewrites the broken file.
    /// </summary>
    public void Init()
    {
        var loaded = Load(ConfigPath);
        var migrated = false;

        // First run at the new location: adopt the old file once, and let the next save move it.
        if (loaded is null && !File.Exists(ConfigPath))
        {
            loaded = Load(LegacyConfigPath);
            migrated = loaded is not null;
        }

        IsDirty = loaded is null || migrated;
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
            // The folder need not exist yet, and the log must not be what creates it.
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            var tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Config, JsonOptions));
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Settings are not worth crashing the app on exit; the previous file stays intact.
            _log.Error($"Could not save settings to {ConfigPath}.", ex);

            // One non-modal notice, so a failed save is not silent; the surface decides how to show it.
            SaveFailed?.Invoke(this, "Settings could not be saved; changes apply to this session only.");
        }
    }

    /// <summary>
    /// Reads one config file, or returns null when it is absent, unreadable or invalid.
    /// </summary>
    /// <param name="path">The file to read.</param>
    private ConfigModel? Load(string path)
    {
        try
        {
            using var openStream = File.OpenRead(path);
            return JsonSerializer.Deserialize<ConfigModel>(openStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing file is the ordinary first run; anything else is worth an entry.
            if (ex is not FileNotFoundException)
                _log.Error($"Could not read settings from {path}; using defaults.", ex);

            return null;
        }
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
    }
}
