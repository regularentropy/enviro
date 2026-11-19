using enviro.Models;
using enviro.Services;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Services;

public class DriftActionTests
{
    private static void FlagDrift(EnvModel row, string? liveValue = null)
    {
        row.IsDrifted = true;
        row.LiveValue = liveValue;
    }

    private static (DriftService Svc, List<(string Name, string? Value, EnvironmentVariableTarget Target)> Writes) WriterService(IEnvService env) 
    {
        var writes = new List<(string Name, string? Value, EnvironmentVariableTarget Target)>();
        var svc = new DriftService(env, writeVariable: (name, value, target) => writes.Add((name, value, target)));
        return (svc, writes);
    }

    // ---- EnvScope.AcceptDrift ----

    [Fact]
    public void AcceptDrift_takes_the_live_value_over_as_the_new_baseline()
    {
        var ws = new Workspace();
        var row = Workspace.Row("A", "v");
        ws.User.Replace([row]);
        FlagDrift(row, "setx wrote this");

        ws.User.AcceptDrift(row);

        Assert.Equal("setx wrote this", row.Path);
        Assert.Equal("setx wrote this", row.OriginalPath);
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);
        Assert.False(row.IsDrifted);
        Assert.Null(row.LiveValue);
    }

    [Fact]
    public void AcceptDrift_is_one_undo_step()
    {
        var ws = new Workspace();
        var row = Workspace.Row("A", "v");
        ws.User.Replace([row]);
        FlagDrift(row, "setx wrote this");

        ws.User.AcceptDrift(row);

        ws.Undo();

        // The workspace value comes back; the accepted live value stays as the baseline the row
        // is now pending against (see EnvScope.WriteFields), like undoing past an apply.
        Assert.Equal("v", row.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, row.State);

        ws.Redo();

        Assert.Equal("setx wrote this", row.Path);
        Assert.Equal("setx wrote this", row.OriginalPath);
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);
    }

    [Fact]
    public void AcceptDrift_on_an_externally_deleted_row_marks_a_pending_deletion()
    {
        var ws = new Workspace();
        var row = Workspace.Row("A", "v");
        ws.User.Replace([row]);
        FlagDrift(row); // live value null: the registry no longer has the variable

        ws.User.AcceptDrift(row);

        Assert.Equal(EnvironmentalVariableState.Deleted, row.State);
        Assert.Equal("A", row.OriginalName);
        Assert.Equal("v", row.OriginalPath);
        Assert.False(row.IsDrifted);
        Assert.Null(row.LiveValue);
    }

    [Fact]
    public void AcceptDrift_drops_a_pending_edit_of_a_drifted_row()
    {
        var ws = new Workspace();
        var row = Workspace.Row("A", "baseline");
        ws.User.Replace([row]);
        ws.User.Update(row, "edited in enviro");
        FlagDrift(row, "setx wrote a third value");

        ws.User.AcceptDrift(row);

        Assert.Equal("setx wrote a third value", row.Path);
        Assert.Equal("setx wrote a third value", row.OriginalPath);
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);
    }

    [Fact]
    public void AcceptDrift_on_a_non_drifted_row_is_a_no_op()
    {
        var ws = new Workspace();
        var row = Workspace.Row("A", "v");
        ws.User.Replace([row]);

        ws.User.AcceptDrift(row);

        Assert.Equal("v", row.Path);
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);
        Assert.False(ws.History.CanUndo); // nothing was recorded, so no empty undo step
    }

    // ---- DriftService.Revert ----

    [Fact]
    public void Revert_writes_the_baseline_back_under_the_original_name()
    {
        var env = new EnvService(new UndoHistory());
        var row = Workspace.Row("A", "baseline");
        ((EnvScope)env.User).Replace([row]);
        FlagDrift(row, "setx wrote this");
        var (svc, writes) = WriterService(env);

        var reverted = svc.Revert(env.User.GetByName("A")!);

        Assert.True(reverted);
        var write = Assert.Single(writes);
        Assert.Equal("A", write.Name);
        Assert.Equal("baseline", write.Value);
        Assert.Equal(EnvironmentVariableTarget.User, write.Target);
        Assert.False(row.IsDrifted);
        Assert.Null(row.LiveValue);
    }

    [Fact]
    public void Revert_of_an_Added_row_deletes_the_external_variable()
    {
        var env = new EnvService(new UndoHistory());
        var row = new EnvModel { Name = "NEW", OriginalName = "NEW", Path = "v", State = EnvironmentalVariableState.Added };
        ((EnvScope)env.User).Replace([row]);
        FlagDrift(row, "installer value");
        var (svc, writes) = WriterService(env);

        var reverted = svc.Revert(row);

        Assert.True(reverted);
        var write = Assert.Single(writes);
        Assert.Equal("NEW", write.Name);
        Assert.Null(write.Value);
        Assert.Equal(EnvironmentVariableTarget.User, write.Target);
        Assert.False(row.IsDrifted);
        Assert.Null(row.LiveValue);
    }

    [Fact]
    public void Revert_refuses_a_scope_this_session_must_not_mutate()
    {
        var env = new EnvService(new UndoHistory());
        var row = Workspace.Row("M", "baseline");
        ((EnvScope)env.Machine).Replace([row]);
        FlagDrift(row, "changed externally");
        var (svc, writes) = WriterService(env);

        var reverted = svc.Revert(row);

        // CanMutate is Windows machinery, so only a Windows runner can differentiate; there an
        // elevated runner lets the fake writer accept, a standard one is refused. Anywhere the
        // check cannot even evaluate, Revert refuses too.
        if (OperatingSystem.IsWindows() && AdminChecker.CanMutate(EnvironmentalVariableType.Machine))
        {
            Assert.True(reverted);
            Assert.Single(writes);
        }
        else
        {
            Assert.False(reverted);
            Assert.Empty(writes);
            Assert.True(row.IsDrifted);
            Assert.Equal("changed externally", row.LiveValue);
        }
    }

    [Fact]
    public void A_failing_write_keeps_the_overlay_and_returns_false()
    {
        var env = new EnvService(new UndoHistory());
        var row = Workspace.Row("A", "baseline");
        ((EnvScope)env.User).Replace([row]);
        FlagDrift(row, "setx wrote this");
        var log = new FakeLog();
        var svc = new DriftService(env, writeVariable: (_, _, _) => throw new IOException("registry is locked"), log: log);

        var reverted = svc.Revert(row);

        Assert.False(reverted);
        Assert.True(row.IsDrifted);
        Assert.Equal("setx wrote this", row.LiveValue);
        Assert.Single(log.Entries);
    }
}
