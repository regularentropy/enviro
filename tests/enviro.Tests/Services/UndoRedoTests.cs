using System.ComponentModel;
using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Undo/redo over two scopes sharing one history, replayed through <see cref="EnvScope.ApplyDelta"/>
/// exactly the way the private <c>EnvService.ApplyEntry</c> does — the harness mirrors those three lines.
/// </summary>
public class UndoRedoTests
{
    private static EnvModel Row(string name, string value) => Workspace.Row(name, value);

    private static List<string> Names(EnvScope scope) => Workspace.Names(scope);

    [Fact]
    public void Import_of_many_entries_is_a_single_undo_step()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("KEEP", "same")]);

        var outcome = ws.User.Import(
        [
            new KeyValuePair<string, string>("NEW", "n"),
            new KeyValuePair<string, string>("KEEP", "changed"),
            new KeyValuePair<string, string>("BAD=NAME", "x"),
        ]);

        Assert.Equal(new ImportOutcome(Added: 1, Overwritten: 1, Unchanged: 0, Skipped: 1), outcome);

        ws.Undo(); // one step reverts the whole batch

        Assert.False(ws.History.CanUndo);
        Assert.Equal(["KEEP"], Names(ws.User));
        Assert.Equal("same", ws.User.GetByName("KEEP")!.Path);
        Assert.Equal(EnvironmentalVariableState.Unchanged, ws.User.GetByName("KEEP")!.State);

        ws.Redo();

        Assert.Equal("n", ws.User.GetByName("NEW")!.Path);
        Assert.Equal("changed", ws.User.GetByName("KEEP")!.Path);
        Assert.False(ws.User.Contains("BAD=NAME"));
    }

    [Fact]
    public void Upsert_restoring_a_pending_deletion_is_one_undo_step()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "original")]);
        var a = ws.User.GetByName("A")!;
        ws.User.Delete(a);                                // step 1
        ws.User.Upsert("A", "new", overwrite: false);     // step 2: nested restore + update

        Assert.Equal("new", a.Path);

        ws.Undo(); // one step back to the pending deletion, not to an intermediate state
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);
        Assert.Equal("original", a.Path);
        Assert.True(ws.History.CanUndo);

        ws.Undo(); // step 1
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Update_with_the_same_value_still_marks_a_persisted_row_modified_and_records_one_step()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Update(a, "v"); // same value, but State moves Unchanged -> Modified

        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.True(ws.History.CanUndo);

        ws.Undo();
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void A_no_op_update_on_an_added_row_records_no_undo_step()
    {
        var ws = new Workspace();
        var row = Row("NEW", "v");
        row.State = EnvironmentalVariableState.Added; // seeded straight from the system, no history
        ws.User.Replace([row]);

        ws.User.Update(row, "v"); // Added row, same value: no field moves, nothing recorded

        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Update_on_a_deleted_row_adds_no_undo_step()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "v")]);
        var a = ws.User.GetByName("A")!;
        ws.User.Delete(a);      // the only recorded step
        ws.User.Update(a, "x"); // no-op on a deleted row

        ws.Undo(); // the single step undoes the deletion

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v", a.Path);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Undoing_a_delete_of_an_added_row_twice_removes_it_again()
    {
        var ws = new Workspace();
        ws.User.Upsert("NEW", "v", overwrite: true); // step 1
        var row = ws.User.GetByName("NEW")!;
        ws.User.Delete(row);                          // step 2: an Added row leaves the list
        Assert.Empty(ws.User.Rows);

        ws.Undo(); // step 2 (the delete) undone: the row is back
        Assert.Same(row, ws.User.GetByName("NEW"));
        Assert.Equal(EnvironmentalVariableState.Added, row.State);

        ws.Undo(); // step 1 (the add) undone: the list is empty again
        Assert.Empty(ws.User.Rows);
        Assert.False(ws.History.CanUndo);

        ws.Redo(); // step 1 redone
        Assert.Equal("v", ws.User.GetByName("NEW")!.Path);
        Assert.Equal(EnvironmentalVariableState.Added, ws.User.GetByName("NEW")!.State);

        ws.Redo(); // step 2 redone: the row leaves the list again
        Assert.Empty(ws.User.Rows);
    }

    [Fact]
    public void Undo_after_an_unrecorded_sort_reinserts_after_the_anchor_not_the_stale_index()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "1"), Row("B", "2")]);
        ws.User.Upsert("C", "3", overwrite: true); // step 1: rows A B C
        var c = ws.User.GetByName("C")!;
        ws.User.Delete(c);                          // step 2: removal, anchor = B
        ws.User.Sort(nameof(EnvModel.Name), ListSortDirection.Descending); // unrecorded reorder: B A

        Assert.Equal(["B", "A"], Names(ws.User));

        ws.Undo(); // C must come back right after B's *current* position

        Assert.Equal(["B", "C", "A"], Names(ws.User));
        Assert.Equal(EnvironmentalVariableState.Added, c.State);

        ws.Redo();
        Assert.Equal(["B", "A"], Names(ws.User));
    }

    [Fact]
    public void A_batch_across_both_scopes_is_one_undo_step()
    {
        var ws = new Workspace();

        using (ws.History.BeginBatch())
        {
            ws.User.Upsert("U", "1", overwrite: true);
            ws.Machine.Upsert("M", "2", overwrite: true);
        }

        Assert.True(ws.History.CanUndo);

        ws.Undo();

        Assert.False(ws.History.CanUndo); // both scopes reverted by the single step
        Assert.Empty(ws.User.Rows);
        Assert.Empty(ws.Machine.Rows);

        ws.Redo();

        Assert.Equal("1", ws.User.GetByName("U")!.Path);
        Assert.Equal("2", ws.Machine.GetByName("M")!.Path);
    }

    [Fact]
    public void Undoing_past_an_applied_edit_leaves_the_row_pending_against_the_new_baseline()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "v")]);
        var a = ws.User.GetByName("A")!;
        ws.User.Update(a, "v2");                                    // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // step 2: baseline moves to v2

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.OriginalPath);

        ws.Undo(); // undo the apply: the system holds v2, so the row genuinely matches it
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);

        ws.Undo(); // undo the edit itself: v against a system baseline of v2 is pending, not Unchanged
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v", a.Path);
        Assert.Equal("v2", a.OriginalPath);

        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);
    }

    [Fact]
    public void Undoing_past_an_applied_add_leaves_the_row_unchanged()
    {
        var ws = new Workspace();
        ws.User.Upsert("NEW", "v", overwrite: true);                     // step 1
        var row = ws.User.GetByName("NEW")!;
        ws.User.MarkApplied(row, EnvironmentalVariableState.Added);      // step 2: system now has it
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);

        ws.Undo(); // the system holds the value, so the row is genuinely Unchanged, not Added
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);
        Assert.Equal("v", row.Path);

        ws.Undo(); // undo the add: the row leaves the list
        Assert.Empty(ws.User.Rows);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void An_applied_deletion_undone_then_restored_reads_as_added_not_unchanged()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "v")]);
        var a = ws.User.GetByName("A")!;
        ws.User.Delete(a);                                          // step 1: pending Deleted
        ws.User.MarkApplied(a, EnvironmentalVariableState.Deleted); // step 2: row leaves the list
        Assert.Empty(ws.User.Rows);

        ws.Undo(); // undo the apply: the row is back but the system no longer has it
        Assert.Same(a, ws.User.GetByName("A"));
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);

        ws.User.Restore(a); // restoring against an absent system entry means creating it again
        Assert.Equal(EnvironmentalVariableState.Added, a.State);

        ws.Undo(); // undo the restore
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);
    }

    [Fact]
    public void An_applied_deletion_undone_then_reset_reads_as_added()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "v")]);
        var a = ws.User.GetByName("A")!;
        ws.User.Delete(a);
        ws.User.MarkApplied(a, EnvironmentalVariableState.Deleted);
        ws.Undo();

        ws.User.Reset(a);

        Assert.Equal(EnvironmentalVariableState.Added, a.State);
        Assert.Equal("A", a.Name);
        Assert.Equal("v", a.Path);
    }

    [Fact]
    public void Rename_undo_restores_the_old_name_and_a_clean_state()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("A", "1")]);
        var a = ws.User.GetByName("A")!;

        var (model, rejection) = ws.User.ApplyEdit(a, "B", "2");
        Assert.Null(rejection);
        Assert.Equal("B", model!.Name);

        ws.Undo();
        Assert.Equal("A", a.Name);
        Assert.Equal("1", a.Path);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("A", a.OriginalName);

        ws.Redo();
        Assert.Equal("B", a.Name);
        Assert.Equal("2", a.Path);
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("A", a.OriginalName); // baseline keeps the registry key the rename must remove
    }

    [Fact]
    public void Undo_steps_touch_only_their_own_scope()
    {
        var ws = new Workspace();
        ws.User.Replace([Row("U", "1")]);
        ws.Machine.Replace([Row("M", "1")]);

        ws.User.Update(ws.User.GetByName("U")!, "2");       // step 1
        ws.Machine.Update(ws.Machine.GetByName("M")!, "2"); // step 2

        ws.Undo(); // the machine step
        Assert.Equal("1", ws.Machine.GetByName("M")!.Path);
        Assert.Equal("2", ws.User.GetByName("U")!.Path);

        ws.Undo(); // the user step
        Assert.Equal("1", ws.User.GetByName("U")!.Path);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Undo_walks_the_whole_history_back_and_redo_forward()
    {
        var ws = new Workspace();
        ws.User.Upsert("A", "1", overwrite: true);            // step 1
        ws.User.Upsert("B", "2", overwrite: true);            // step 2
        ws.User.Delete(ws.User.GetByName("A")!);              // step 3: Added row leaves the list

        ws.Undo();
        Assert.Equal("1", ws.User.GetByName("A")!.Path);
        Assert.NotNull(ws.User.GetByName("B"));

        ws.Undo();
        Assert.Null(ws.User.GetByName("B"));
        Assert.NotNull(ws.User.GetByName("A"));

        ws.Undo();
        Assert.Empty(ws.User.Rows);
        Assert.False(ws.History.CanUndo);

        ws.Undo(); // a no-op on an empty history, must not throw
        Assert.Empty(ws.User.Rows);

        ws.Redo();
        ws.Redo();
        ws.Redo();
        Assert.Null(ws.User.GetByName("A"));
        Assert.Equal("2", ws.User.GetByName("B")!.Path);
        Assert.True(ws.History.CanUndo);
        Assert.False(ws.History.CanRedo);
    }

}
