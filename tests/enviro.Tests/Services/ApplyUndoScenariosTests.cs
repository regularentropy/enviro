using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Long interleaved scenarios over <see cref="Workspace"/>: edits, applies (MarkApplied), imports,
/// deletes and undo/redo mixed in the nastiest orders. Every expected state below was traced through
/// EnvScope.WriteFields/ApplyDelta by hand, including the reconciliation against the live baseline.
/// </summary>
public class ApplyUndoScenariosTests
{
    [Fact]
    public void Edit_apply_edit_apply_then_full_undo_lands_pending_against_the_last_applied_value()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Update(a, "v2");
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // apply 1: system = v2
        ws.User.Update(a, "v3");
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // apply 2: system = v3

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v3", a.OriginalPath);

        for (var i = 0; i < 4; i++)
            ws.Undo();

        // The system holds v3, so rolling back to v is a pending change, not a clean Unchanged.
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v", a.Path);
        Assert.Equal("v3", a.OriginalPath);
        Assert.True(ws.User.HasChanges());

        for (var i = 0; i < 4; i++)
            ws.Redo();

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v3", a.Path);
        Assert.Equal("v3", a.OriginalPath);
        Assert.False(ws.User.HasChanges());
    }

    [Fact]
    public void Applied_deletion_undo_rejected_edit_restore_reapply_then_full_rollback()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Delete(a);                                       // step 1: pending Deleted
        ws.User.MarkApplied(a, EnvironmentalVariableState.Deleted); // step 2: written, row leaves
        Assert.Empty(ws.User.Rows);

        ws.Undo(); // undo the apply: row back, still Deleted, system does not have it
        Assert.Same(a, ws.User.GetByName("A"));
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);

        Assert.NotNull(ws.User.ApplyEdit(a, "X", "y").Rejection); // rejected: records no step

        ws.User.Restore(a);   // step 3: absent from the system -> Added
        Assert.Equal(EnvironmentalVariableState.Added, a.State);
        ws.User.MarkApplied(a, EnvironmentalVariableState.Added);  // step 4: system has A=v again
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);

        for (var i = 0; i < 4; i++)
            ws.Undo();

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("A", a.Name);
        Assert.Equal("v", a.Path);
        Assert.Single(ws.User.Rows);
        Assert.False(ws.History.CanUndo); // the rejected edit added no fifth step

        for (var i = 0; i < 4; i++)
            ws.Redo();

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Single(ws.User.Rows);
    }

    [Fact]
    public void Rename_applied_then_undone_leaves_a_pending_rollback_against_the_new_name()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.ApplyEdit(a, "B", "v2");                        // step 1: rename + revalue
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // step 2: system = B:v2, A gone
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("B", a.OriginalName);

        ws.Undo(); // undo the apply: row matches the system again
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("B", a.Name);

        ws.Undo(); // undo the rename: system has B:v2 and no A -> pending, not Unchanged
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("A", a.Name);
        Assert.Equal("v", a.Path);
        Assert.Equal("B", a.OriginalName);
        Assert.Equal("v2", a.OriginalPath);

        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("B", a.Name);
        Assert.Equal("v2", a.Path);
    }

    [Fact]
    public void Overwrite_apply_overwrite_undo_redo_chain_reconciles_each_step_against_the_live_baseline()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        Assert.Equal(UpsertOutcome.Overridden, ws.User.Upsert("A", "v2", overwrite: true)); // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified);                          // step 2
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.OriginalPath);

        Assert.Equal(UpsertOutcome.Overridden, ws.User.Upsert("A", "v3", overwrite: true));  // step 3
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v3", a.Path);
        Assert.Equal("v2", a.OriginalPath);

        ws.Undo(); // step 3: back to the applied v2, which the system holds -> genuinely Unchanged
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);

        ws.Undo(); // step 2: apply undone, still matches the system
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);

        ws.Undo(); // step 1: v against a system baseline of v2 -> pending
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v", a.Path);
        Assert.Equal("v2", a.OriginalPath);

        ws.Redo();
        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v3", a.Path);
        Assert.Equal("v2", a.OriginalPath);
    }

    [Fact]
    public void Import_after_apply_then_full_undo_keeps_the_applied_baseline()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Update(a, "v2");                                     // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // step 2: system = v2
        var outcome = ws.User.Import([                               // step 3: one batch
            new KeyValuePair<string, string>("A", "v3"),
            new KeyValuePair<string, string>("NEW", "n"),
        ]);

        Assert.Equal(new ImportOutcome(Added: 1, Overwritten: 1, Unchanged: 0, Skipped: 0), outcome);
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v3", a.Path);
        Assert.NotNull(ws.User.GetByName("NEW"));

        ws.Undo(); // the import batch: A back to the applied v2, NEW gone
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);
        Assert.Null(ws.User.GetByName("NEW"));

        ws.Undo(); // the apply
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.Path);

        ws.Undo(); // the edit: v against the system baseline v2 -> pending
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v", a.Path);
        Assert.Equal("v2", a.OriginalPath);
        Assert.False(ws.History.CanUndo);

        ws.Redo();
        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v3", a.Path);
        Assert.Equal("v2", a.OriginalPath);
        Assert.Equal("n", ws.User.GetByName("NEW")!.Path);
    }

    [Fact]
    public void Reset_right_after_an_apply_records_nothing_and_undo_still_reaches_the_edit()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.ApplyEdit(a, "B", "v2");                             // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Modified); // step 2
        ws.User.Reset(a); // applied row: reset moves no field -> no undo step

        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.True(ws.History.CanUndo);

        ws.Undo(); // step 2
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("B", a.Name);

        ws.Undo(); // step 1
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("A", a.Name);
        Assert.Equal("v", a.Path);
        Assert.Equal("B", a.OriginalName);
        Assert.False(ws.History.CanUndo); // exactly two steps existed; the reset added none
    }

    [Fact]
    public void The_corrupted_overlay_is_not_part_of_the_undo_snapshot()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;
        a.IsCorrupted = true;

        ws.User.Update(a, "v2"); // step 1
        ws.Undo();

        Assert.Equal("v", a.Path);
        Assert.True(a.IsCorrupted); // the overlay is independent of State and of undo
    }

    [Fact]
    public void An_apply_run_batch_of_a_deletion_and_an_edit_undoes_as_one_step()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("DEL", "x"), Workspace.Row("EDIT", "v")]);
        var del = ws.User.GetByName("DEL")!;
        var edit = ws.User.GetByName("EDIT")!;

        ws.User.Delete(del);        // step 1: pending Deleted
        ws.User.Update(edit, "v2"); // step 2: pending Modified

        using (ws.History.BeginBatch()) // the apply run writes both and cleans both rows
        {
            ws.User.MarkApplied(del, EnvironmentalVariableState.Deleted);
            ws.User.MarkApplied(edit, EnvironmentalVariableState.Modified);
        }

        Assert.Null(ws.User.GetByName("DEL"));
        Assert.Equal(EnvironmentalVariableState.Unchanged, edit.State);

        ws.Undo(); // the whole apply run in one step
        Assert.NotNull(ws.User.GetByName("DEL"));
        Assert.Equal(EnvironmentalVariableState.Deleted, del.State);
        Assert.Equal(EnvironmentalVariableState.Unchanged, edit.State); // system holds v2 now
        Assert.Equal("v2", edit.Path);
        Assert.True(ws.History.CanUndo);

        ws.Undo(); // the edit step: v against the system's v2 -> pending
        Assert.Equal(EnvironmentalVariableState.Modified, edit.State);
        Assert.Equal("v", edit.Path);
        Assert.Equal("v2", edit.OriginalPath);

        ws.Undo(); // the delete step: the applied deletion still stands (the system lacks DEL),
        Assert.Equal(EnvironmentalVariableState.Added, del.State); // so the row reads as a pending re-create
        Assert.Equal("DEL", del.Name);
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Add_apply_delete_then_undo_all_the_way_back()
    {
        var ws = new Workspace();

        ws.User.Upsert("NEW", "v", overwrite: true);                     // step 1: Added
        var row = ws.User.GetByName("NEW")!;
        ws.User.MarkApplied(row, EnvironmentalVariableState.Added);      // step 2: system has it
        ws.User.Delete(row);                                             // step 3: pending Deleted
        Assert.Equal(EnvironmentalVariableState.Deleted, row.State);

        ws.Undo(); // step 3: system holds the value -> genuinely Unchanged, not Deleted
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);

        ws.Undo(); // step 2: still Unchanged
        Assert.Equal(EnvironmentalVariableState.Unchanged, row.State);

        ws.Undo(); // step 1: the row leaves
        Assert.Empty(ws.User.Rows);
        Assert.False(ws.History.CanUndo);

        ws.Redo();
        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Deleted, row.State);
        Assert.Single(ws.User.Rows);
    }

    [Fact]
    public void An_unrecorded_sort_between_edits_does_not_disturb_the_undo_steps()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("B", "w"), Workspace.Row("A", "v")]);

        ws.User.Update(ws.User.GetByName("A")!, "v2"); // step 1
        ws.User.Sort(nameof(EnvModel.Name), System.ComponentModel.ListSortDirection.Ascending); // unrecorded
        ws.User.Update(ws.User.GetByName("B")!, "w2"); // step 2

        Assert.Equal(["A", "B"], Workspace.Names(ws.User));

        ws.Undo(); // step 2
        Assert.Equal(EnvironmentalVariableState.Unchanged, ws.User.GetByName("B")!.State);
        Assert.Equal("w", ws.User.GetByName("B")!.Path);
        Assert.Equal("v2", ws.User.GetByName("A")!.Path);

        ws.Undo(); // step 1
        Assert.Equal("v", ws.User.GetByName("A")!.Path);
        Assert.Equal(["A", "B"], Workspace.Names(ws.User)); // the sort is not undone
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Import_cancelling_a_pending_edit_then_full_undo_reaches_the_clean_baseline()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Update(a, "v2"); // step 1: pending Modified

        var outcome = ws.User.Import([new KeyValuePair<string, string>("A", "v")]); // step 2: baseline value cancels the edit
        Assert.Equal(1, outcome.Unchanged);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v", a.Path);

        ws.Undo(); // step 2: system still holds v -> the edit comes back as pending
        Assert.Equal(EnvironmentalVariableState.Modified, a.State);
        Assert.Equal("v2", a.Path);
        Assert.Equal("v", a.OriginalPath);

        ws.Undo(); // step 1
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v", a.Path);
        Assert.False(ws.History.CanUndo);

        ws.Redo();
        ws.Redo();
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State); // import's cancel reapplied
        Assert.Equal("v", a.Path);
    }

    [Fact]
    public void A_cross_scope_apply_batch_undoes_as_one_step_in_both_scopes()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("U", "v")]);
        ws.Machine.Replace([Workspace.Row("M", "v")]);
        var u = ws.User.GetByName("U")!;
        var m = ws.Machine.GetByName("M")!;

        ws.User.Update(u, "v2");   // step 1
        ws.Machine.Update(m, "v2"); // step 2

        using (ws.History.BeginBatch()) // one apply run over both scopes
        {
            ws.User.MarkApplied(u, EnvironmentalVariableState.Modified);
            ws.Machine.MarkApplied(m, EnvironmentalVariableState.Modified);
        }

        ws.Undo(); // the single batch step
        Assert.Equal(EnvironmentalVariableState.Unchanged, u.State); // system holds v2 now
        Assert.Equal(EnvironmentalVariableState.Unchanged, m.State);
        Assert.True(ws.History.CanUndo); // the two edit steps remain

        ws.Undo();
        ws.Undo();
        Assert.False(ws.History.CanUndo);
        Assert.Equal(EnvironmentalVariableState.Modified, u.State);
        Assert.Equal("v", u.Path);
        Assert.Equal("v2", u.OriginalPath);
        Assert.Equal(EnvironmentalVariableState.Modified, m.State);
        Assert.Equal("v", m.Path);
        Assert.Equal("v2", m.OriginalPath);
    }

    [Fact]
    public void An_import_on_both_scopes_inside_one_batch_is_a_single_undo_step()
    {
        var ws = new Workspace();

        using (ws.History.BeginBatch())
        {
            ws.User.Import([new KeyValuePair<string, string>("U", "1")]);
            ws.Machine.Import([new KeyValuePair<string, string>("M", "2")]);
        }

        Assert.NotNull(ws.User.GetByName("U"));
        Assert.NotNull(ws.Machine.GetByName("M"));
        Assert.True(ws.History.CanUndo);

        ws.Undo(); // one step empties both scopes

        Assert.Empty(ws.User.Rows);
        Assert.Empty(ws.Machine.Rows);
        Assert.False(ws.History.CanUndo);

        ws.Redo();
        Assert.Equal("1", ws.User.GetByName("U")!.Path);
        Assert.Equal("2", ws.Machine.GetByName("M")!.Path);
    }

    [Fact]
    public void A_rename_chain_frees_a_name_and_undo_restores_both_rows()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "1"), Workspace.Row("B", "2")]);
        var a = ws.User.GetByName("A")!;
        var b = ws.User.GetByName("B")!;

        Assert.Null(ws.User.ApplyEdit(a, "C", "1").Rejection); // step 1: A -> C
        Assert.Null(ws.User.ApplyEdit(b, "A", "2").Rejection); // step 2: B -> A (name now free)

        Assert.NotNull(ws.User.GetByName("C"));
        Assert.NotNull(ws.User.GetByName("A"));
        Assert.Null(ws.User.GetByName("B"));

        ws.Undo(); // step 2: the row is B again
        Assert.Equal("B", b.Name);
        Assert.Equal(EnvironmentalVariableState.Unchanged, b.State);
        Assert.Null(ws.User.GetByName("A"));

        ws.Undo(); // step 1: the row is A again
        Assert.Equal("A", a.Name);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Null(ws.User.GetByName("C"));
        Assert.False(ws.History.CanUndo);
    }

    [Fact]
    public void Upsert_after_undoing_past_an_applied_deletion_refuses_without_overwrite()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Delete(a);                                          // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Deleted); // step 2: system no longer has A
        ws.Undo(); // step 2 undone: row back as Deleted
        ws.Undo(); // step 1 undone: system lacks A -> the row reads Added
        Assert.Equal(EnvironmentalVariableState.Added, a.State);

        Assert.Equal(UpsertOutcome.Refused, ws.User.Upsert("A", "new", overwrite: false)); // Added row is "live"
        Assert.Equal("v", a.Path);

        Assert.Equal(UpsertOutcome.Overridden, ws.User.Upsert("A", "new", overwrite: true));
        Assert.Equal("new", a.Path);
        Assert.Equal(EnvironmentalVariableState.Added, a.State); // still a pending create against the system
    }

    [Fact]
    public void Applied_deletion_undone_then_redone_removes_the_row_again()
    {
        var ws = new Workspace();
        ws.User.Replace([Workspace.Row("A", "v")]);
        var a = ws.User.GetByName("A")!;

        ws.User.Delete(a);                                       // step 1
        ws.User.MarkApplied(a, EnvironmentalVariableState.Deleted); // step 2: row leaves the list
        Assert.Empty(ws.User.Rows);

        ws.Undo();
        Assert.Same(a, ws.User.GetByName("A"));
        Assert.Equal(EnvironmentalVariableState.Deleted, a.State);

        ws.Redo(); // the applied deletion replays: the row leaves again
        Assert.Empty(ws.User.Rows);
        Assert.True(ws.History.CanUndo);
        Assert.False(ws.History.CanRedo);
    }
}
