using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// ConfigService takes an explicit config path through its internal constructor, so the tests
/// run against a temp file instead of the real %APPDATA% one.
/// </summary>
public class ConfigServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "enviro-cfg-" + Guid.NewGuid().ToString("N"));
    private readonly string _configPath;
    private readonly FakeLog _log = new();

    public ConfigServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "config.json");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ConfigService NewService() => new(_log, _configPath);

    [Fact]
    public void A_missing_file_loads_defaults_and_marks_them_dirty()
    {
        var service = NewService();

        Assert.True(service.Config.EnableCorruptedValidation);
        Assert.True(service.IsDirty);
        Assert.False(File.Exists(_configPath));
    }

    [Fact]
    public void Save_persists_the_config_and_a_new_instance_reads_it_back_clean()
    {
        var service = NewService();
        service.Config.Theme = ThemePreference.Dark;
        service.Config.EnableCorruptedValidation = false;

        service.Save();

        Assert.True(File.Exists(_configPath));
        Assert.Contains("\"Dark\"", File.ReadAllText(_configPath)); // theme persisted as a string

        var reloaded = NewService();
        Assert.False(reloaded.IsDirty);
        Assert.Equal(ThemePreference.Dark, reloaded.Config.Theme);
        Assert.False(reloaded.Config.EnableCorruptedValidation);
    }

    [Fact]
    public void Editing_a_property_marks_the_config_dirty()
    {
        NewService().Save();
        var clean = NewService();
        Assert.False(clean.IsDirty);

        clean.Config.Theme = ThemePreference.Light;

        Assert.True(clean.IsDirty);
    }

    [Fact]
    public void An_invalid_file_falls_back_to_defaults_and_stays_dirty()
    {
        File.WriteAllText(_configPath, "{ nope");

        var service = NewService();

        Assert.True(service.Config.EnableCorruptedValidation);
        Assert.True(service.IsDirty);
        Assert.Single(_log.Entries); // the broken file was logged
    }

    [Fact]
    public void A_legacy_config_beside_the_executable_is_adopted_once()
    {
        var legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        File.WriteAllText(legacy, """{"EnableCorruptedValidation": false, "Theme": "Light"}""");

        try
        {
            var service = NewService();

            Assert.False(service.Config.EnableCorruptedValidation);
            Assert.Equal(ThemePreference.Light, service.Config.Theme);
            Assert.True(service.IsDirty); // migrated: the next save moves it to the new location
            Assert.False(File.Exists(_configPath));
        }
        finally
        {
            File.Delete(legacy);
        }
    }

    [Fact]
    public void Window_and_view_state_round_trips_through_the_file()
    {
        var service = NewService();
        service.Config.WindowBounds = new WindowBounds(10, 20, 800, 600);
        service.Config.WindowMaximized = true;
        service.Config.ActiveTab = 1;
        service.Config.UserFilter = "path";
        service.Config.MachineFilter = "java";
        service.Config.UserSort = new SortPreference("Path", Descending: true);

        service.Save();

        var reloaded = NewService();
        Assert.Equal(new WindowBounds(10, 20, 800, 600), reloaded.Config.WindowBounds);
        Assert.True(reloaded.Config.WindowMaximized);
        Assert.Equal(1, reloaded.Config.ActiveTab);
        Assert.Equal("path", reloaded.Config.UserFilter);
        Assert.Equal("java", reloaded.Config.MachineFilter);
        Assert.Equal(new SortPreference("Path", true), reloaded.Config.UserSort);
        Assert.Null(reloaded.Config.MachineSort);
    }

    [Fact]
    public void An_older_config_without_view_state_loads_with_sensible_defaults()
    {
        File.WriteAllText(_configPath, """{"EnableCorruptedValidation": false, "Theme": "Light"}""");

        var service = NewService();

        Assert.Null(service.Config.WindowBounds);
        Assert.False(service.Config.WindowMaximized);
        Assert.Equal(0, service.Config.ActiveTab);
        Assert.Equal(string.Empty, service.Config.UserFilter);
        Assert.Null(service.Config.UserSort);
    }
}
