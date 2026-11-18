using System.ComponentModel;
using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class EnvScopeTests
{
    private static EnvScope NewScope(out UndoHistory history)
    {
        history = new UndoHistory();
        return new EnvScope(EnvironmentalVariableType.User, history);
    }

    private static EnvModel Row(string name, string value, EnvironmentalVariableState state = EnvironmentalVariableState.Unchanged) =>
        new() { Name = name, OriginalName = name, Path = value, OriginalPath = value, State = state };

    private EnvScope ScopeWith(params EnvModel[] rows)
    {
        var scope = NewScope(out _);
        scope.Replace([.. rows]);
        return scope;
    }

    [Fact]
    public void Add_marks_the_row_added_and_records_an_undo_step()
    {
        var scope = NewScope(out var history);
        var row = Row("NEW", "v", EnvironmentalVariableState.Unchanged);

        scope.Add(row);

        var stored = Assert.Single(scope.Rows.Cast<EnvModel>());
        Assert.Same(row, stored);
        Assert.Equal(EnvironmentalVariableState.Added, stored.State);
        Assert.True(history.CanUndo);
    }

    [Theory]
    [InlineData("foo", "FOO", true)]
    [InlineData("FOO", "foo", true)]
    [InlineData("foo", "bar", false)]
    public void Contains_and_GetByName_are_case_insensitive(string stored, string query, bool expected)
    {
        var scope = ScopeWith(Row(stored, "v"));

        Assert.Equal(expected, scope.Contains(query));
        Assert.Equal(expected, scope.GetByName(query) is not null);
    }

    [Fact]
    public void Update_marks_an_unchanged_row_modified_but_keeps_an_added_row_added()
    {
        var scope = ScopeWith(Row("A", "old"));
        var added = Row("B", "x", EnvironmentalVariableState.Added);
        scope.Add(added);

        scope.Update(scope.GetByName("A")!, "new");
        scope.Update(added, "y");

        Assert.Equal("new", scope.GetByName("A")!.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, scope.GetByName("A")!.State);
        Assert.Equal("y", added.Path);
        Assert.Equal(EnvironmentalVariableState.Added, added.State);
    }

    [Fact]
    public void Update_on_a_deleted_row_is_a_no_op()
    {
        var scope = ScopeWith(Row("A", "v"));
        var row = scope.GetByName("A")!;
        scope.Delete(row);

        scope.Update(row, "changed");

        Assert.Equal("v", row.Path);
        Assert.Equal(EnvironmentalVariableState.Deleted, row.State);
    }

    [Fact]
    public void Upsert_adds_a_new_name()
    {
        var scope = NewScope(out _);

        var outcome = scope.Upsert("NEW", "v", overwrite: false);

        Assert.Equal(UpsertOutcome.Added, outcome);
        var row = scope.GetByName("NEW")!;
        Assert.Equal("v", row.Path);
        Assert.Equal(EnvironmentalVariableState.Added, row.State);
    }

    [Fact]
    public void Upsert_refuses_a_live_row_without_overwrite_and_mutates_nothing()
    {
        var scope = NewScope(out var history);
        scope.Replace([Row("A", "old")]);

        var outcome = scope.Upsert("A", "new", overwrite: false);

        Assert.Equal(UpsertOutcome.Refused, outcome);
        Assert.Equal("old", scope.GetByName("A")!.Path);
        Assert.False(history.CanUndo); // a refused call leaves no undo step
    }

    [Fact]
    public void Upsert_overwrites_a_live_row_when_asked()
    {
        var scope = ScopeWith(Row("A", "old"));

        var outcome = scope.Upsert("a", "new", overwrite: true); // case-insensitive hit

        Assert.Equal(UpsertOutcome.Overridden, outcome);
        Assert.Equal("new", scope.GetByName("A")!.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, scope.GetByName("A")!.State);
    }

    [Fact]
    public void Upsert_on_a_pending_deletion_restores_and_revalues_the_row()
    {
        var scope = ScopeWith(Row("A", "old"));
        var row = scope.GetByName("A")!;
        scope.Delete(row);
        Assert.Equal(EnvironmentalVariableState.Deleted, row.State);

        var outcome = scope.Upsert("A", "new", overwrite: false);

        Assert.Equal(UpsertOutcome.Overridden, outcome);
        Assert.Equal("new", row.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, row.State); // restore then update
    }

    [Fact]
    public void ApplyEdit_rejects_an_unknown_row_a_deleted_row_a_blank_name_and_a_collision()
    {
        var scope = ScopeWith(Row("A", "1"), Row("B", "2"));
        var a = scope.GetByName("A")!;
        var b = scope.GetByName("B")!;

        var stranger = Row("X", "9");
        Assert.Equal("The variable is no longer in this list.", scope.ApplyEdit(stranger, "Y", "9").Rejection);

        scope.Delete(a);
        Assert.Equal("Restore the variable before editing it.", scope.ApplyEdit(a, "C", "9").Rejection);
        scope.Restore(a);

        Assert.Equal("The name cannot be empty.", scope.ApplyEdit(a, "  ", "9").Rejection);
        Assert.Equal("Cannot rename to 'b': the name is already in use.", scope.ApplyEdit(a, "b", "9").Rejection); // case-insensitive collision, message keeps the typed spelling

        // Nothing was mutated by the rejections:
        Assert.Equal("A", a.Name);
        Assert.Equal("1", a.Path);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Same(b, scope.GetByName("B"));
    }

    [Fact]
    public void ApplyEdit_renames_and_revalues_in_one_step()
    {
        var scope = ScopeWith(Row("A", "1"));
        var a = scope.GetByName("A")!;

        var (model, rejection) = scope.ApplyEdit(a, "Renamed", "2");

        Assert.Null(rejection);
        Assert.Same(a, model);
        Assert.Equal("Renamed", a.Name);
        Assert.Equal("2", a.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("A", a.OriginalName); // baseline untouched, the rename still has to reach the registry
        Assert.Null(scope.GetByName("A"));
        Assert.Same(a, scope.GetByName("renamed")); // case-insensitive lookup after rename
    }

    [Fact]
    public void ApplyEdit_allows_renaming_to_the_same_name_in_another_case()
    {
        var scope = ScopeWith(Row("A", "1"));
        var a = scope.GetByName("A")!;

        var (model, rejection) = scope.ApplyEdit(a, "a", "1");

        Assert.Null(rejection);
        Assert.Equal("a", model!.Name);
    }

    [Fact]
    public void Delete_removes_an_added_row_but_only_marks_a_persisted_one()
    {
        var scope = ScopeWith(Row("A", "v"));
        var added = Row("B", "v", EnvironmentalVariableState.Added);
        scope.Add(added);
        var a = scope.GetByName("A")!;

        scope.Delete(added);
        scope.Delete(a);

        Assert.Null(scope.GetByName("B")); // gone from the list entirely
        Assert.Single(scope.Rows);
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);
        Assert.Equal("A", a.Name);
    }

    [Fact]
    public void Restore_returns_a_deleted_row_to_unchanged_under_its_original_name()
    {
        var scope = ScopeWith(Row("A", "v"));
        var a = scope.GetByName("A")!;
        scope.Delete(a);

        scope.Restore(a);

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("A", a.Name);
    }

    [Fact]
    public void Reset_returns_a_modified_row_to_its_baseline()
    {
        var scope = ScopeWith(Row("A", "v"));
        var a = scope.GetByName("A")!;
        scope.Update(a, "changed");
        scope.ApplyEdit(a, "Renamed", "changed");

        scope.Reset(a);

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("A", a.Name);
        Assert.Equal("v", a.Path);
    }

    [Fact]
    public void MarkApplied_cleans_a_written_row_and_drops_a_written_deletion()
    {
        var scope = ScopeWith(Row("A", "v"), Row("B", "w"));
        var a = scope.GetByName("A")!;
        var b = scope.GetByName("B")!;
        scope.Update(a, "v2");
        scope.Delete(b);

        scope.MarkApplied(a, EnvironmentalVariableState.Modified);
        scope.MarkApplied(b, EnvironmentalVariableState.Deleted);

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.OriginalPath); // baseline synced to what the system now holds
        Assert.Null(scope.GetByName("B"));   // the row left the list
        Assert.Single(scope.Rows);
    }

    [Fact]
    public void Sort_orders_by_name_in_both_directions()
    {
        var scope = ScopeWith(Row("b", "1"), Row("A", "2"), Row("c", "3"));

        scope.Sort(nameof(EnvModel.Name), ListSortDirection.Ascending);
        Assert.Equal(["A", "b", "c"], scope.Rows.Cast<EnvModel>().Select(r => r.Name).ToArray());

        scope.Sort(nameof(EnvModel.Name), ListSortDirection.Descending);
        Assert.Equal(["c", "b", "A"], scope.Rows.Cast<EnvModel>().Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Import_reports_added_overwritten_unchanged_and_skipped()
    {
        var scope = ScopeWith(Row("KEEP", "same"), Row("CHANGE", "old"));
        var entries = new List<KeyValuePair<string, string>>
        {
            new("NEW", "n"),          // -> Added
            new("change", "new"),     // -> Overwritten (case-insensitive)
            new("KEEP", "same"),      // -> Unchanged
            new("BAD=NAME", "x"),     // -> Skipped
            new("EMPTY", "  "),       // -> Skipped
        };

        var outcome = scope.Import(entries);

        Assert.Equal(new ImportOutcome(Added: 1, Overwritten: 1, Unchanged: 1, Skipped: 2), outcome);
        Assert.Equal("n", scope.GetByName("NEW")!.Path);
        Assert.Equal("new", scope.GetByName("CHANGE")!.Path);
        Assert.False(scope.Contains("BAD=NAME"));
        Assert.False(scope.Contains("EMPTY"));
    }

    [Fact]
    public void Import_with_the_baseline_value_cancels_a_pending_edit()
    {
        var scope = ScopeWith(Row("A", "baseline"));
        var a = scope.GetByName("A")!;
        scope.Update(a, "edited");
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);

        var outcome = scope.Import([new KeyValuePair<string, string>("A", "baseline")]);

        Assert.Equal(1, outcome.Unchanged);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("baseline", a.Path);
    }

    [Fact]
    public void Import_restores_a_pending_deletion()
    {
        var scope = ScopeWith(Row("A", "v"));
        var a = scope.GetByName("A")!;
        scope.Delete(a);

        var outcome = scope.Import([new KeyValuePair<string, string>("A", "v2")]);

        Assert.Equal(1, outcome.Overwritten);
        Assert.NotEqual(EnvironmentalVariableState.Deleted, a.State);
        Assert.Equal("v2", a.Path);
    }

    [Fact]
    public void Import_of_a_repeated_name_resolves_to_the_row_the_batch_created()
    {
        var scope = NewScope(out _);

        var outcome = scope.Import(
        [
            new KeyValuePair<string, string>("A", "1"),
            new KeyValuePair<string, string>("A", "2"),
        ]);

        Assert.Equal(new ImportOutcome(Added: 1, Overwritten: 1, Unchanged: 0, Skipped: 0), outcome);
        Assert.Equal("2", Assert.Single(scope.Rows.Cast<EnvModel>()).Path);
    }
}
