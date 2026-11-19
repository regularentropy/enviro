using enviro.Models;
using enviro.Services;
using enviro.Static;
using enviro.Tests.Services;
using Xunit;

namespace enviro.Tests.Static;

public class StatusPresenterTests
{
    [Fact]
    public void An_empty_workspace_reads_no_pending_changes()
    {
        var env = new EnvService(new UndoHistory());

        var summary = StatusPresenter.Describe(env, enableCorruptedValidation: true);

        Assert.Equal("No pending changes", summary.Pending);
        Assert.True(summary.ShowCorrupted);
        Assert.Equal("0 corrupted", summary.Corrupted);
        Assert.Equal("0 drifted", summary.Drifted);
    }

    [Fact]
    public void Pending_counts_all_three_states_across_both_scopes()
    {
        var env = new EnvService(new UndoHistory());
        ((EnvScope)env.User).Replace([Workspace.Row("A", "v"), Workspace.Row("DEL", "x")]);
        ((EnvScope)env.Machine).Replace([Workspace.Row("M", "v")]);
        env.User.Update(env.User.GetByName("A")!, "v2");   // 1 modified
        env.User.Upsert("NEW", "1", overwrite: true);       // 1 added
        env.User.Delete(env.User.GetByName("DEL")!);        // 1 deleted (user)
        env.Machine.Delete(env.Machine.GetByName("M")!);    // 1 deleted (machine)

        var summary = StatusPresenter.Describe(env, enableCorruptedValidation: false);

        Assert.Equal("1 added · 1 modified · 2 deleted", summary.Pending);
        Assert.False(summary.ShowCorrupted); // the flag is passed through, not computed
    }

    [Fact]
    public void The_corrupted_count_sums_both_scopes()
    {
        var env = new EnvService(new UndoHistory());
        env.User.Upsert("ENVIRO_TEST_A", "x", overwrite: true);
        env.Machine.Upsert("ENVIRO_TEST_B", "y", overwrite: true);
        env.User.GetByName("ENVIRO_TEST_A")!.IsCorrupted = true;
        env.Machine.GetByName("ENVIRO_TEST_B")!.IsCorrupted = true;

        Assert.Equal("2 corrupted", StatusPresenter.Describe(env, true).Corrupted);
    }

    [Fact]
    public void The_drifted_count_sums_both_scopes()
    {
        var env = new EnvService(new UndoHistory());
        ((EnvScope)env.User).Replace([Workspace.Row("A", "v")]);
        ((EnvScope)env.Machine).Replace([Workspace.Row("M", "w")]);
        env.User.GetByName("A")!.IsDrifted = true;
        env.Machine.GetByName("M")!.IsDrifted = true;

        var summary = StatusPresenter.Describe(env, enableCorruptedValidation: true);

        Assert.Equal("2 drifted", summary.Drifted);
    }
}
