using System.Collections;
using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class DriftServiceTests
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

    /// <summary>A fake registry: one dictionary per target, handed out by the injected reader.</summary>
    private static DriftService NewService(IEnvService env, IDictionary user, IDictionary machine) =>
        new(env, target => target == EnvironmentVariableTarget.User ? user : machine);

    private static IDictionary Live(params (string Name, string? Value)[] entries)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in entries)
            dict[name] = value;
        return dict;
    }

    [Fact]
    public void An_unchanged_row_that_matches_live_is_not_drifted()
    {
        var env = EnvWith([Row("A", "v")], []);
        var svc = NewService(env, Live(("A", "v")), Live());

        svc.CheckScope(env.User);

        Assert.False(env.User.GetByName("A")!.IsDrifted);
        Assert.Null(env.User.GetByName("A")!.LiveValue);
    }

    [Fact]
    public void A_value_changed_externaly_flags_the_row_and_exposes_the_live_value()
    {
        var env = EnvWith([Row("A", "v")], []);
        var svc = NewService(env, Live(("A", "setx wrote this")), Live());

        svc.CheckScope(env.User);

        var row = env.User.GetByName("A")!;
        Assert.True(row.IsDrifted);
        Assert.Equal("setx wrote this", row.LiveValue);
    }

    [Fact]
    public void An_external_delete_flags_the_row_without_a_live_value()
    {
        var env = EnvWith([Row("A", "v")], []);
        var svc = NewService(env, Live(), Live()); // "A" is gone from the registry

        svc.CheckScope(env.User);

        var row = env.User.GetByName("A")!;
        Assert.True(row.IsDrifted);
        Assert.Null(row.LiveValue);
    }

    [Fact]
    public void An_Added_row_absent_from_the_registry_is_not_drifted()
    {
        var env = EnvWith([Row("NEW", "v", EnvironmentalVariableState.Added)], []);
        var svc = NewService(env, Live(), Live());

        svc.CheckScope(env.User);

        Assert.False(env.User.GetByName("NEW")!.IsDrifted);
    }

    [Fact]
    public void An_Added_row_created_externaly_is_drifted()
    {
        var env = EnvWith([Row("NEW", "v", EnvironmentalVariableState.Added)], []);
        var svc = NewService(env, Live(("NEW", "installer value")), Live());

        svc.CheckScope(env.User);

        var row = env.User.GetByName("NEW")!;
        Assert.True(row.IsDrifted);
        Assert.Equal("installer value", row.LiveValue);
    }

    [Fact]
    public void A_pending_Modified_row_compares_against_OriginalPath_not_Path()
    {
        var env = EnvWith([Row("A", "baseline")], []);
        env.User.Update(env.User.GetByName("A")!, "edited in enviro"); // Path differs, baseline does not
        var svc = NewService(env, Live(("A", "baseline")), Live());

        svc.CheckScope(env.User);

        // The registry still holds the baseline, so the pending edit is no drift.
        Assert.False(env.User.GetByName("A")!.IsDrifted);
    }

    [Fact]
    public void The_name_lookup_against_live_is_case_insensitive()
    {
        var env = EnvWith([Row("Path", "v")], []);
        var svc = NewService(env, Live(("PATH", "v")), Live());

        svc.CheckScope(env.User);

        Assert.False(env.User.GetByName("Path")!.IsDrifted);
    }

    [Fact]
    public void ClearOverlay_resets_every_row()
    {
        var env = EnvWith([Row("A", "v")], [Row("M", "w")]);
        var svc = NewService(env, Live(("A", "changed")), Live(("M", "changed")));
        svc.CheckAll();
        Assert.True(env.User.GetByName("A")!.IsDrifted);
        Assert.True(env.Machine.GetByName("M")!.IsDrifted);

        svc.ClearOverlay();

        Assert.False(env.User.GetByName("A")!.IsDrifted);
        Assert.Null(env.User.GetByName("A")!.LiveValue);
        Assert.False(env.Machine.GetByName("M")!.IsDrifted);
        Assert.Null(env.Machine.GetByName("M")!.LiveValue);
    }

    [Fact]
    public void CheckAll_covers_both_scopes()
    {
        var env = EnvWith([Row("A", "v")], [Row("M", "w")]);
        var svc = NewService(env, Live(("A", "changed")), Live(("M", "changed")));

        svc.CheckAll();

        Assert.True(env.User.GetByName("A")!.IsDrifted);
        Assert.True(env.Machine.GetByName("M")!.IsDrifted);
        Assert.Equal("changed", env.User.GetByName("A")!.LiveValue);
        Assert.Equal("changed", env.Machine.GetByName("M")!.LiveValue);
    }
}
