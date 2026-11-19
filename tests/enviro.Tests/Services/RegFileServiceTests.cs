using System.Text;
using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class RegFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "enviro-regtests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeLog _log = new();
    private readonly RegFileService _service;

    public RegFileServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _service = new RegFileService(_log, new VariableFileService(_log));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempFile(string name) => Path.Combine(_dir, name);

    private static EnvService NewEnvService(out UndoHistory history)
    {
        history = new UndoHistory();
        return new EnvService(history);
    }

    private static EnvModel Row(string name, string value) =>
        new() { Name = name, OriginalName = name, Path = value, OriginalPath = value, State = EnvironmentalVariableState.Unchanged };

    [Fact]
    public void Save_then_Read_round_trips_user_and_machine()
    {
        var env = NewEnvService(out _);
        env.User.Upsert("ENVIRO_TEST_USER", "user-value", overwrite: true);
        env.Machine.Upsert("ENVIRO_TEST_MACHINE", "machine-value", overwrite: true);

        var path = TempFile("roundtrip.reg");

        Assert.Null(_service.Save(path, _service.Snapshot(env)));

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.NotNull(file);
        Assert.Equal(VariableFile.CurrentVersion, file!.Version);
        Assert.Equal("user-value", file.User!["ENVIRO_TEST_USER"]);
        Assert.Equal("machine-value", file.Machine!["ENVIRO_TEST_MACHINE"]);
    }

    [Fact]
    public void Save_writes_utf16_bom_version_header_and_crlf()
    {
        var env = NewEnvService(out _);
        env.User.Upsert("ENVIRO_TEST_A", "b", overwrite: true);
        var path = TempFile("bom.reg");

        Assert.Null(_service.Save(path, _service.Snapshot(env)));

        var bytes = File.ReadAllBytes(path);

        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);
        Assert.StartsWith("Windows Registry Editor Version 5.00", Encoding.Unicode.GetString(bytes));
    }

    [Fact]
    public void Save_then_Read_round_trips_escaped_quotes_and_backslashes()
    {
        var env = NewEnvService(out _);
        var value = @"C:\a ""b""\c";
        env.User.Upsert("ENVIRO_TEST_PATH", value, overwrite: true);
        var path = TempFile("escaped.reg");

        Assert.Null(_service.Save(path, _service.Snapshot(env)));

        var raw = Encoding.Unicode.GetString(File.ReadAllBytes(path));
        Assert.Contains(@"""C:\\a \""b\""\\c""", raw);

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.Equal(value, file!.User!["ENVIRO_TEST_PATH"]);
    }

    [Fact]
    public void Save_writes_a_percent_value_as_hex2_and_it_round_trips()
    {
        var env = NewEnvService(out _);
        var value = "%SystemRoot%\\system32";
        env.User.Upsert("ENVIRO_TEST_PCT", value, overwrite: true);
        var path = TempFile("percent.reg");

        Assert.Null(_service.Save(path, _service.Snapshot(env)));

        var raw = Encoding.Unicode.GetString(File.ReadAllBytes(path));

        Assert.Contains("hex(2):", raw);

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.Equal(value, file!.User!["ENVIRO_TEST_PCT"]);
    }

    [Fact]
    public void Read_accepts_REGEDIT4_ansi_header()
    {
        var path = TempFile("ansi.reg");
        File.WriteAllText(path, "REGEDIT4\r\n\r\n[HKEY_CURRENT_USER\\Environment]\r\n\"ENVIRO_TEST_A\"=\"1\"\r\n");

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.NotNull(file);
        Assert.Equal(VariableFile.CurrentVersion, file!.Version);
        Assert.Equal("1", file.User!["ENVIRO_TEST_A"]);
    }

    [Fact]
    public void Read_rejects_a_missing_header()
    {
        var path = TempFile("noheader.reg");
        File.WriteAllText(path, "not a registry export\r\n");

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.Contains("not a registry export", error);
        Assert.Contains(TempFile("noheader.reg"), error);
        Assert.Single(_log.Entries);
    }

    [Fact]
    public void Read_reports_a_missing_file()
    {
        var (file, error) = _service.Read(TempFile("missing.reg"));

        Assert.Null(file);
        Assert.NotNull(error);
        Assert.Contains("missing.reg", error);
        Assert.Single(_log.Entries);
    }

    [Fact]
    public void Read_refuses_a_file_over_the_64_mb_cap()
    {
        var path = TempFile("huge.reg");
        using (var stream = File.Create(path))
            stream.SetLength(64L * 1024 * 1024 + 1);

        var (file, error) = _service.Read(path);

        Assert.Null(file);
        Assert.Contains("64 MB", error);
    }

    [Fact]
    public void Snapshot_and_Entries_delegate_to_the_json_service()
    {
        var env = NewEnvService(out _);
        var json = new VariableFileService(_log);
        env.User.Upsert("ENVIRO_TEST_B", "2", overwrite: true);
        env.User.Upsert("ENVIRO_TEST_A", "1", overwrite: true);

        var snapshot = _service.Snapshot(env);
        var expected = json.Snapshot(env);

        Assert.Equal(VariableFile.CurrentVersion, snapshot.Version);
        Assert.Equal(expected.User!.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            snapshot.User!.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        Assert.All(snapshot.User, pair => Assert.Equal(expected.User[pair.Key], pair.Value));

        var entries = _service.Entries(snapshot);

        Assert.Equal(2, entries.Count);
        Assert.Equal(["ENVIRO_TEST_A", "ENVIRO_TEST_B"], entries.Select(e => e.Name).ToArray());
        Assert.All(entries, e => Assert.Null(e.Status));
    }

    [Fact]
    public void Read_parses_a_handwritten_reg_file()
    {
        var path = TempFile("handwritten.reg");
        File.WriteAllText(
            path,
            "Windows Registry Editor Version 5.00\r\n"
            + "\r\n"
            + "[HKEY_CURRENT_USER\\Environment]\r\n"
            + "@=\"default\"\r\n"
            + "\"DEL\"=-\r\n"
            + "\"ONE\"=\"1\"\r\n"
            + "\"WRAP\"=hex(2):25,00,50,00,41,00,54,00,48,00,25,00,3B,00,25,00,54,00,45,00,4D,00,50,00,\\\r\n"
            + "    25,00,5C,00,62,00,69,00,6E,00,00,00\r\n"
            + "\r\n"
            + "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment]\r\n"
            + "\"M\"=\"m\"\r\n"
            + "\r\n"
            + "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Ignore]\r\n"
            + "\"IGNORED\"=\"yes\"\r\n");

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.NotNull(file);
        Assert.Equal(["ONE", "WRAP"], file.User!.Keys.ToArray());
        Assert.Equal(["M"], file.Machine!.Keys.ToArray());
        Assert.Equal("%PATH%;%TEMP%\\bin", file.User["WRAP"]);
        Assert.False(file.User.ContainsKey("@"));
        Assert.False(file.User.ContainsKey("DEL"));
        Assert.False(file.User.ContainsKey("IGNORED"));
    }

    [Fact]
    public void Read_strictly_skips_and_logs_unsupported_value_lines()
    {
        var path = TempFile("unsupported.reg");
        File.WriteAllText(
            path,
            "Windows Registry Editor Version 5.00\r\n"
            + "\r\n"
            + "[HKEY_CURRENT_USER\\Environment]\r\n"
            + "\"KEEP\"=\"1\"\r\n"
            + "\"GONE\"=hex(2):zz,00\r\n");

        var (file, error) = _service.Read(path);

        Assert.Null(error);
        Assert.Equal("1", file!.User!["KEEP"]);
        Assert.False(file.User.ContainsKey("GONE"));
        Assert.Single(_log.Entries);
    }

    // Row is exercised by the round-trip test through the workspace below.
    [Fact]
    public void Snapshot_skips_pending_deletions_like_the_json_service()
    {
        var env = NewEnvService(out _);
        var keep = Row("ENVIRO_TEST_KEEP", "keep");
        var gone = Row("ENVIRO_TEST_GONE", "gone");
        env.User.Upsert(keep.Name, keep.Path, overwrite: true);
        env.User.Upsert(gone.Name, gone.Path, overwrite: true);
        env.User.Delete(env.User.GetByName(gone.Name)!);

        var snapshot = _service.Snapshot(env);

        Assert.True(snapshot.User!.ContainsKey("ENVIRO_TEST_KEEP"));
        Assert.False(snapshot.User!.ContainsKey("ENVIRO_TEST_GONE"));
    }
}
