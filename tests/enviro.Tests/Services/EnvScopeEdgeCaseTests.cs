using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Scope operations in the orders a user can reach them from the UI: double actions, no-op
/// actions, case tricks. Each test pins whether the call records an undo step.
/// </summary>
public class EnvScopeEdgeCaseTests
{
    private static (EnvScope Scope, UndoHistory History) NewScope()
    {
        var history = new UndoHistory();
        return (new EnvScope(EnvironmentalVariableType.User, history), history);
    }

    [Fact]
    public void Deleting_the_same_row_twice_records_only_the_first_delete()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Delete(a);
        ws.User.Delete(a); // already Deleted: no field moves

        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);
        Assert.True(ws.History.CanUndo);

        ws.Undo(); // the single recorded step

        Assert.False(ws.History.CanUndo); // the second delete added no step
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
    }

    [Fact]
    public void Restoring_an_unchanged_row_records_nothing()
    {
        var (scope, history) = NewScope();
        scope.Replace([Workspace.Row("A", "v")]);

        scope.Restore(scope.GetByName("A")!);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Reset_of_an_unchanged_row_records_nothing()
    {
        var (scope, history) = NewScope();
        scope.Replace([Workspace.Row("A", "v")]);

        scope.Reset(scope.GetByName("A")!);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Sort_by_an_unknown_property_does_not_throw_and_keeps_the_rows()
    {
        var (scope, _) = NewScope();
        scope.Replace([Workspace.Row("B", "1"), Workspace.Row("A", "2")]);

        scope.Sort("NoSuchProperty", System.ComponentModel.ListSortDirection.Ascending);

        Assert.Equal(2, scope.Rows.Count);
    }

    [Fact]
    public void ApplyEdit_to_the_same_name_still_marks_a_persisted_row_modified()
    {
        var (scope, history) = NewScope();
        scope.Replace([Workspace.Row("A", "v")]);
        var a = scope.GetByName("A")!;

        var (model, rejection) = scope.ApplyEdit(a, "A", "v");

        Assert.Null(rejection);
        Assert.Same(a, model);
        Assert.Equal(EnvironmentalVariableState.Modified, a.State); // the rename rule fires even for a no-op name
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void Update_accepts_semicolons_and_equals_in_the_value()
    {
        var (scope, _) = NewScope();
        scope.Replace([Workspace.Row("A", "v")]);
        var a = scope.GetByName("A")!;

        scope.Update(a, "x=y;a;b");

        Assert.Equal("x=y;a;b", a.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
    }

    [Fact]
    public void Contains_is_case_insensitive_but_does_not_trim()
    {
        var (scope, _) = NewScope();
        scope.Replace([Workspace.Row("FOO", "v")]);

        Assert.True(scope.Contains("foo"));
        Assert.False(scope.Contains(" FOO"));
        Assert.False(scope.Contains("FOO "));
    }
}
