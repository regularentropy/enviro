using enviro.Models;
using System.ComponentModel;
using System.Text.Json;

namespace enviro.Services;

internal interface IConfigService
{
    ConfigModel? Config { get; }
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

    public ConfigModel? Config { get; set; }

    private readonly string CurrentDirectory;

    private readonly string ConfigPath;

    public ConfigService()
    {
        CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ConfigPath = Path.Combine(CurrentDirectory, "config.json");

        Init();
    }

    /// <summary>
    /// Initializes config if path isn't exist
    /// </summary>
    public void Init()
    {
        if (!File.Exists(ConfigPath))
        {
            var cfg = new ConfigModel()
            {
                EnableCorruptedValidation = true
            };

            Config = cfg;

            using var createStream = File.Create(ConfigPath);
            JsonSerializer.Serialize(createStream, cfg, new JsonSerializerOptions { WriteIndented = true });
            return;
        }

        using var openStream = File.OpenRead(ConfigPath);
        Config = JsonSerializer.Deserialize<ConfigModel>(openStream);

        Config.PropertyChanged += OnConfigChanged;
    }

    /// <summary>
    /// Saves the config to the file
    /// </summary>
    public void Save()
    {
        using var createStream = File.Create(ConfigPath);
        JsonSerializer.Serialize(createStream, Config, new JsonSerializerOptions { WriteIndented = true });
        return;
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
    }
}
