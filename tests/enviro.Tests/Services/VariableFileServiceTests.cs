using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class VariableFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "enviro-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeLog _log = new();
    private readonly VariableFileService _service;

    public VariableFileServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _service = new VariableFileService(_log);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempFile(string name) => Path.Combine(_dir, name);

    private static EnvService NewEnvService(out UndoHistory history)
    {
        history = new UndoHistory();
        return new EnvService(history);
    }

    private static EnvModel Row(string name, string value, EnvironmentalVariableState state = EnvironmentalVariableState.Unchanged) =>
        new() { Name = name, OriginalName = name, Path = value, OriginalPath = value, State = state };

    [Fact]
    public void Save_then_Read_round_trips_the_document()
    {
        var env = NewEnvService(out _);
        var name = "ENVIRO_TEST_" + Guid.NewGuid().ToString("N");
        env.User.Upsert(name, "user-value", overwrite: true);
        env.Machine.Upsert(name, "machine-value", overwrite: true);

        var path = TempFile("roundtrip.json");
        var document = _service.Snapshot(env);

        Assert.Null(_service.Save(path, document));
        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.NotNull(file);
        Assert.Equal(VariableFile.CurrentVersion, file!.Version);
        Assert.Equal("user-value", file.User![name]);
        Assert.Equal("machine-value", file.Machine![name]);
    }

    [Fact]
    public void Snapshot_skips_rows_with_a_pending_deletion()
    {
        var env = NewEnvService(out _);
        var keep = "ENVIRO_TEST_KEEP_" + Guid.NewGuid().ToString("N");
        var gone = "ENVIRO_TEST_GONE_" + Guid.NewGuid().ToString("N");
        env.User.Upsert(keep, "1", overwrite: true);
        env.User.Upsert(gone, "2", overwrite: true);
        env.User.Delete(env.User.GetByName(gone)!);

        var document = _service.Snapshot(env);

        Assert.True(document.User!.ContainsKey(keep));
        Assert.False(document.User!.ContainsKey(gone));
    }

    [Fact]
    public void Read_reports_a_missing_file()
    {
        var (file, error) = _service.Read(TempFile("missing.json"));

        Assert.Null(file);
        Assert.NotNull(error);
        Assert.Contains("missing.json", error);
    }

    [Fact]
    public void Read_reports_invalid_json_and_logs_it()
    {
        var path = TempFile("broken.json");
        File.WriteAllText(path, "{ not json");

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.NotNull(error);
        Assert.Single(_log.Entries);
    }

    [Fact]
    public void Read_reports_an_empty_file()
    {
        var path = TempFile("empty.json");
        File.WriteAllText(path, "");

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.Contains("empty", error);
    }

    [Fact]
    public void Read_rejects_an_unsupported_format_version()
    {
        var path = TempFile("v2.json");
        File.WriteAllText(path, """{"version": 2, "user": {"A": "1"}}""");

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.Contains("version 2", error);
    }

    [Fact]
    public void Read_refuses_a_file_over_the_64_mb_cap()
    {
        var path = TempFile("huge.json");
        using (var stream = File.Create(path))
            stream.SetLength(64L * 1024 * 1024 + 1);

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.Contains("64 MB", error);
    }

    [Fact]
    public void Read_normalises_null_values_and_case_duplicate_names()
    {
        var path = TempFile("normalise.json");
        File.WriteAllText(path, """{"version": 1, "user": {"A": "1", "a": "2", "B": null}}""");

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.NotNull(file!.User);
        var user = file.User!;
        Assert.Single(user);          // "A" and "a" merged, null value dropped
        Assert.Equal("2", user["a"]);
    }

    [Fact]
    public void Entries_groups_by_scope_in_name_order_without_a_comparison()
    {
        var file = new VariableFile
        {
            Version = VariableFile.CurrentVersion,
            User = new() { ["b"] = "2", ["A"] = "1" },
            Machine = new() { ["m"] = "3" },
        };

        var entries = _service.Entries(file);

        Assert.Equal(
            [EnvironmentalVariableType.User, EnvironmentalVariableType.User, EnvironmentalVariableType.Machine],
            entries.Select(e => e.Scope).ToArray());
        Assert.Equal(["A", "b"], entries.Where(e => e.Scope == EnvironmentalVariableType.User).Select(e => e.Name).ToArray());
        Assert.All(entries, e => Assert.Null(e.Status));
    }

    [Fact]
    public void Compare_resolves_each_status_against_the_workspace()
    {
        var env = NewEnvService(out _);
        var same = "ENVIRO_TEST_SAME_" + Guid.NewGuid().ToString("N");
        var diff = "ENVIRO_TEST_DIFF_" + Guid.NewGuid().ToString("N");
        env.User.Upsert(same, "same-value", overwrite: true);
        env.User.Upsert(diff, "old", overwrite: true);

        var file = new VariableFile
        {
            Version = VariableFile.CurrentVersion,
            User = new()
            {
                [same] = "same-value",                                   // Unchanged
                [diff] = "new",                                           // Overwrites
                ["ENVIRO_TEST_NEW_" + Guid.NewGuid().ToString("N")] = "n", // New
                ["BAD=NAME"] = "x",                                       // Skipped
            },
            Machine = new() { ["M"] = "m" },                              // MachineSkipped without elevation
        };

        var entries = _service.Compare(file, env, canImportMachine: false);

        var byName = entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(VariableFileEntryStatus.Unchanged, byName[same].Status);
        Assert.Equal(VariableFileEntryStatus.Overwrites, byName[diff].Status);
        Assert.Equal("old", byName[diff].CurrentValue);
        Assert.Equal(VariableFileEntryStatus.New, entries.Single(e => e.Name.StartsWith("ENVIRO_TEST_NEW_")).Status);
        Assert.Equal(VariableFileEntryStatus.Skipped, byName["BAD=NAME"].Status);
        Assert.Equal(VariableFileEntryStatus.MachineSkipped, byName["M"].Status);
    }

    [Fact]
    public void Compare_marks_machine_entries_normally_when_elevated()
    {
        var env = NewEnvService(out _);
        var file = new VariableFile
        {
            Version = VariableFile.CurrentVersion,
            Machine = new() { ["M"] = "m" },
        };

        var entries = _service.Compare(file, env, canImportMachine: true);

        Assert.Equal(VariableFileEntryStatus.New, Assert.Single(entries).Status);
    }
}
