using System.Collections;
using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class DriftAuditTests
{
    private static EnvModel Row(string name, string value, EnvironmentalVariableState state = EnvironmentalVariableState.Unchanged) =>
        new() { Name = name, OriginalName = name, Path = value, OriginalPath = value, State = state };

    private static IEnvService EnvWith(List<EnvModel> user, List<EnvModel> machine)
    {
        var env = new EnvService(new UndoHistory());
        ((EnvScope)env.User).Replace(user);
        ((EnvScope)env.Machine).Replace(machine);
        return env;
    }

    private static (DriftService Svc, FakeLog Log) Audited(IEnvService env, IDictionary user, IDictionary machine)
    {
        var log = new FakeLog();
        var svc = new DriftService(env, target => target == EnvironmentVariableTarget.User ? user : machine, log: log);
        return (svc, log);
    }

    private static IDictionary Live(params (string Name, string? Value)[] entries)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in entries)
            dict[name] = value;
        return dict;
    }

    [Fact]
    public void An_external_variable_becomes_an_Added_drifted_row_once()
    {
        var env = EnvWith([Row("A", "v")], []);
        var (svc, _) = Audited(env, Live(("A", "v"), ("EXT", "setx wrote it")), Live());

        svc.CheckScope(env.User);

        var row = env.User.GetByName("EXT");
        Assert.NotNull(row);
        Assert.Equal(EnvironmentalVariableState.Added, row!.State);
        Assert.True(row.IsDrifted);
        Assert.Equal("setx wrote it", row.LiveValue);
        Assert.Equal("setx wrote it", row.OriginalPath);

        // Idempotent: the second check must not re-add the variable.
        svc.CheckScope(env.User);

        Assert.Equal(2, env.User.Rows.Count);
    }

    [Fact]
    public void A_newly_drifted_row_logs_Info_once_not_again_on_the_next_check()
    {
        var env = EnvWith([Row("A", "v")], []);
        var (svc, log) = Audited(env, Live(("A", "setx wrote this")), Live());

        svc.CheckScope(env.User);
        var entry = Assert.Single(log.Infos);
        Assert.Contains("A", entry);

        svc.CheckScope(env.User);
        Assert.Single(log.Infos); // the flag stayed true: no second entry
    }

    [Fact]
    public void An_external_variable_logs_one_Info_line_per_name()
    {
        var env = EnvWith([Row("A", "v")], []);
        var (svc, log) = Audited(env, Live(("A", "v"), ("EXT", "installer value")), Live());

        svc.CheckScope(env.User);

        var entry = Assert.Single(log.Infos);
        Assert.Contains("EXT", entry);
    }

    [Fact]
    public void No_Info_when_nothing_changed()
    {
        var env = EnvWith([Row("A", "v")], []);
        var (svc, log) = Audited(env, Live(("A", "v")), Live());

        svc.CheckScope(env.User);
        svc.CheckScope(env.User);

        Assert.Empty(log.Infos);
    }

    [Fact]
    public void A_successful_Revert_logs_Info()
    {
        var env = EnvWith([Row("A", "baseline")], []);
        var row = env.User.GetByName("A")!;
        row.IsDrifted = true;
        row.LiveValue = "setx wrote this";
        var log = new FakeLog();
        var svc = new DriftService(env, writeVariable: (_, _, _) => { }, log: log);

        var reverted = svc.Revert(row);

        Assert.True(reverted);
        var entry = Assert.Single(log.Infos);
        Assert.Contains("Reverted", entry);
        Assert.Contains("A", entry);
    }
}
