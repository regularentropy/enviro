using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Import/export shapes a user can realistically produce by hand-editing the JSON file:
/// wrong version, duplicate keys, unknown fields, case collisions, unicode, broken paths.
/// </summary>
public class ImportEdgeCaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "enviro-imp-" + Guid.NewGuid().ToString("N"));
    private readonly FakeLog _log = new();
    private readonly VariableFileService _service;

    public ImportEdgeCaseTests()
    {
        Directory.CreateDirectory(_dir);
        _service = new VariableFileService(_log);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempFile(string name) => Path.Combine(_dir, name);

    private string WriteJson(string name, string json)
    {
        var path = TempFile(name);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void A_document_without_a_version_is_rejected_not_read_as_current()
    {
        var (file, error) = _service.Read(WriteJson("noversion.json", """{"user": {"A": "1"}}"""));

        Assert.Null(file);
        Assert.Contains("Format version 0 is not supported", error);
    }

    [Fact]
    public void Unknown_extra_properties_are_ignored()
    {
        var (file, error) = _service.Read(WriteJson("extra.json",
            """{"version": 1, "futureSetting": true, "user": {"A": "1"}}"""));

        Assert.Null(error);
        Assert.Equal("1", file!.User!["A"]);
    }

    [Fact]
    public void A_duplicate_key_in_one_section_lets_the_last_value_win()
    {
        var (file, error) = _service.Read(WriteJson("dupe.json",
            """{"version": 1, "user": {"A": "1", "A": "2"}}"""));

        Assert.Null(error);
        Assert.Equal("2", file!.User!["A"]);
    }

    [Fact]
    public void Unicode_names_and_values_round_trip()
    {
        var env = new EnvService(new UndoHistory());
        env.User.Upsert("ПЕРЕМЕННАЯ", "значение 🎉 café", overwrite: true);

        var path = TempFile("unicode.json");
        Assert.Null(_service.Save(path, _service.Snapshot(env)));

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.Equal("значение 🎉 café", file!.User!["ПЕРЕМЕННАЯ"]);
    }

    [Fact]
    public void Save_into_a_missing_directory_returns_an_error_instead_of_throwing()
    {
        var path = Path.Combine(_dir, "no-such-dir", "out.json");

        var error = _service.Save(path, new VariableFile { Version = VariableFile.CurrentVersion });

        Assert.NotNull(error);
        Assert.Contains("no-such-dir", error);
        Assert.False(File.Exists(path));
        Assert.Single(_log.Entries); // and it was logged
    }

    [Fact]
    public void Read_of_a_directory_returns_an_error()
    {
        var (file, error) = _service.Read(_dir);

        Assert.Null(file);
        Assert.NotNull(error);
    }

    [Fact]
    public void Entries_of_a_document_without_sections_is_empty()
    {
        Assert.Empty(_service.Entries(new VariableFile { Version = VariableFile.CurrentVersion }));
    }

    [Fact]
    public void Snapshot_orders_section_keys_case_insensitively()
    {
        var env = new EnvService(new UndoHistory());
        ((EnvScope)env.User).Replace([Workspace.Row("b", "1"), Workspace.Row("A", "2"), Workspace.Row("C", "3")]);

        var keys = _service.Snapshot(env).User!.Keys.ToArray();

        Assert.Equal(["A", "b", "C"], keys);
    }

    [Fact]
    public void Compare_marks_a_pending_deleted_row_as_overwrites()
    {
        var env = new EnvService(new UndoHistory());
        ((EnvScope)env.User).Replace([Workspace.Row("A", "v")]);
        env.User.Delete(env.User.GetByName("A")!);

        var file = new VariableFile { Version = VariableFile.CurrentVersion, User = new() { ["A"] = "v" } };
        var entry = Assert.Single(_service.Compare(file, env, canImportMachine: true));

        Assert.Equal(VariableFileEntryStatus.Overwrites, entry.Status);
        Assert.Equal("v", entry.CurrentValue);
    }

    [Fact]
    public void Importing_the_baseline_value_of_a_pending_deletion_restores_the_row()
    {
        var scope = new EnvScope(EnvironmentalVariableType.User, new UndoHistory());
        scope.Replace([Workspace.Row("A", "v")]);
        var a = scope.GetByName("A")!;
        scope.Delete(a);
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);

        var outcome = scope.Import([new KeyValuePair<string, string>("A", "v")]);

        Assert.Equal(1, outcome.Unchanged);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
    }

    [Fact]
    public void Import_with_a_case_variant_name_overwrites_the_same_row()
    {
        var scope = new EnvScope(EnvironmentalVariableType.User, new UndoHistory());
        scope.Replace([Workspace.Row("PATH", "old")]);

        var outcome = scope.Import([new KeyValuePair<string, string>("Path", "new")]);

        Assert.Equal(1, outcome.Overwritten);
        var row = Assert.Single(scope.Rows.Cast<EnvModel>());
        Assert.Equal("PATH", row.Name); // the row keeps its spelling
        Assert.Equal("new", row.Path);
    }

    [Fact]
    public void A_name_with_surrounding_spaces_is_a_distinct_valid_name()
    {
        var scope = new EnvScope(EnvironmentalVariableType.User, new UndoHistory());
        scope.Replace([Workspace.Row("A", "1")]);

        var outcome = scope.Import([new KeyValuePair<string, string>(" A ", "2")]);

        Assert.Equal(1, outcome.Added); // names are not trimmed
        Assert.True(scope.Contains(" A "));
        Assert.False(scope.Contains("A ") || scope.GetByName("A")!.Name == " A ");
        Assert.Equal(2, scope.Rows.Count);
    }

    [Fact]
    public void A_value_may_contain_equals_and_semicolons()
    {
        var scope = new EnvScope(EnvironmentalVariableType.User, new UndoHistory());

        var outcome = scope.Import([new KeyValuePair<string, string>("A", "x=y;z")]);

        Assert.Equal(1, outcome.Added);
        Assert.Equal("x=y;z", scope.GetByName("A")!.Path);
    }
}
