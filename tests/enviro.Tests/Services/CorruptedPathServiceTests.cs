using enviro.Models;
using enviro.Services;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Services;

public class CorruptedPathServiceTests
{
    private sealed class FakeConfig(bool enabled) : IConfigService
    {
        public ConfigModel Config { get; } = new() { EnableCorruptedValidation = enabled };
        public bool IsDirty => false;
        public event EventHandler<string>? SaveFailed { add { } remove { } }
        public void Init() { }
        public void Save() { }
    }

    private static CorruptedPathService NewService(IEnvService env, bool enabled = true) =>
        new(env, new FakeConfig(enabled), new FakeLog());

    [Fact]
    public void ValidateSegment_classifies_existing_missing_text_and_unc()
    {
        var svc = NewService(new EnvService(new UndoHistory()));
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var missing = Path.Combine(dir.FullName, "nope");

        try
        {
            Assert.Equal(PathSegmentState.Exists, svc.ValidateSegment(dir.FullName));
            Assert.Equal(PathSegmentState.Missing, svc.ValidateSegment(missing));
            Assert.Equal(PathSegmentState.NotPath, svc.ValidateSegment("plain text"));
            Assert.Equal(PathSegmentState.NotPath, svc.ValidateSegment(@"\\host\share")); // never probed
            Assert.Equal(PathSegmentState.NotPath, svc.ValidateSegment("//host/share"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ValidateSegment_expands_environment_references_first()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Environment.SetEnvironmentVariable("ENVIRO_TEST_SEG", dir.FullName);

        try
        {
            var svc = NewService(new EnvService(new UndoHistory()));

            Assert.Equal(PathSegmentState.Exists, svc.ValidateSegment("%ENVIRO_TEST_SEG%"));
            Assert.Equal(PathSegmentState.Missing, svc.ValidateSegment("%ENVIRO_TEST_SEG%\\missing-sub"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENVIRO_TEST_SEG", null);
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsCorrupted_flags_only_missing_path_segments()
    {
        var svc = NewService(new EnvService(new UndoHistory()));
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.True(svc.IsCorrupted(missing));
        Assert.True(svc.IsCorrupted($"some text;{missing};more text")); // one bad segment is enough
        Assert.False(svc.IsCorrupted("plain;text;only"));
        Assert.False(svc.IsCorrupted(@"\\host\share;also fine"));       // UNC is never probed
        Assert.False(svc.IsCorrupted(""));
        Assert.False(svc.IsCorrupted(";;;"));
        Assert.False(svc.IsCorrupted(Path.GetTempPath()));               // existing dir
    }

    [Fact]
    public async Task RequestScan_applies_the_overlay_after_the_debounce()
    {
        var env = new EnvService(new UndoHistory());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        env.User.Upsert("ENVIRO_TEST_BAD", missing, overwrite: true);
        env.User.Upsert("ENVIRO_TEST_OK", Path.GetTempPath(), overwrite: true);
        var svc = NewService(env);

        svc.RequestScan(env.User);

        var bad = env.User.GetByName("ENVIRO_TEST_BAD")!;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!bad.IsCorrupted && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        Assert.True(bad.IsCorrupted);
        Assert.False(env.User.GetByName("ENVIRO_TEST_OK")!.IsCorrupted);
    }

    [Fact]
    public async Task RequestScan_is_a_noop_while_validation_is_disabled()
    {
        var env = new EnvService(new UndoHistory());
        env.User.Upsert("ENVIRO_TEST_BAD", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), overwrite: true);
        var svc = NewService(env, enabled: false);

        svc.RequestScan(env.User);
        await Task.Delay(500); // well past the 150 ms debounce

        Assert.False(env.User.GetByName("ENVIRO_TEST_BAD")!.IsCorrupted);
    }

    [Fact]
    public async Task A_verdict_is_dropped_when_the_row_was_edited_while_the_scan_ran()
    {
        var env = new EnvService(new UndoHistory());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        env.User.Upsert("ENVIRO_TEST_BAD", missing, overwrite: true);
        var row = env.User.GetByName("ENVIRO_TEST_BAD")!;
        var svc = NewService(env);

        svc.RequestScan(env.User);
        env.User.Update(row, Path.GetTempPath()); // edit lands before the 150 ms debounce elapses

        await Task.Delay(600);

        Assert.False(row.IsCorrupted); // the scanned value no longer matches the row
    }

    [Fact]
    public async Task Cancel_suppresses_a_pending_scan()
    {
        var env = new EnvService(new UndoHistory());
        env.User.Upsert("ENVIRO_TEST_BAD", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), overwrite: true);
        var row = env.User.GetByName("ENVIRO_TEST_BAD")!;
        var svc = NewService(env);

        svc.RequestScan(env.User);
        svc.Cancel();

        await Task.Delay(500);

        Assert.False(row.IsCorrupted);
    }

    [Fact]
    public void ClearOverlay_resets_the_flags_on_both_scopes()
    {
        var env = new EnvService(new UndoHistory());
        env.User.Upsert("ENVIRO_TEST_A", "x", overwrite: true);
        env.Machine.Upsert("ENVIRO_TEST_B", "y", overwrite: true);
        env.User.GetByName("ENVIRO_TEST_A")!.IsCorrupted = true;
        env.Machine.GetByName("ENVIRO_TEST_B")!.IsCorrupted = true;
        var svc = NewService(env);

        svc.ClearOverlay();

        Assert.False(env.User.GetByName("ENVIRO_TEST_A")!.IsCorrupted);
        Assert.False(env.Machine.GetByName("ENVIRO_TEST_B")!.IsCorrupted);
    }
}
