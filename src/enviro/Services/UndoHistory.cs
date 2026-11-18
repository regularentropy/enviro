using enviro.Models;

namespace enviro.Services;

/// <summary>
/// The workspace undo/redo history: a bounded stack of delta entries. It stores plain records and
/// knows nothing about scopes, grids or the registry — the scopes push deltas in and the service
/// applies them back out — so no dependency can cycle back into the store or the UI.
/// </summary>
internal interface IUndoHistory
{
    /// <summary>
    /// Whether there is a recorded change that <see cref="Undo"/> can restore.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether an undone change is waiting for <see cref="Redo"/>.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Raised whenever the undo or redo stack changes, so command enablement can refresh.
    /// </summary>
    event EventHandler? StackChanged;

    /// <summary>
    /// Records one change. A new change clears the redo stack, ending the undone branch.
    /// </summary>
    /// <param name="entry">The change to record.</param>
    void Push(UndoEntry entry);

    /// <summary>
    /// Pops the most recent change and moves its swapped form to the redo stack.
    /// </summary>
    /// <returns>
    /// The entry whose before-side deltas restore the pre-change state,
    /// or null when there is nothing to undo.
    /// </returns>
    UndoEntry? Undo();

    /// <summary>
    /// Pops the most recently undone change and moves its swapped form back to the undo stack.
    /// </summary>
    /// <returns>
    /// The entry whose before-side deltas restore the post-change state,
    /// or null when there is nothing to redo.
    /// </returns>
    UndoEntry? Redo();

    /// <summary>
    /// Drops every recorded change, undo and redo alike; called when the workspace is reloaded,
    /// because no delta of the old rows is meaningful anymore.
    /// </summary>
    void Clear();

    /// <summary>
    /// Opens a batch: every <see cref="Push"/> until the returned handle is disposed merges into
    /// one entry, so a multi-scope action (an import or an apply run) is a single undo step.
    /// </summary>
    /// <returns>A handle that commits the merged entry on dispose.</returns>
    IDisposable BeginBatch();
}

/// <summary>
/// One undoable workspace change: the per-scope deltas of the rows it touched, so recording and
/// replaying a step costs O(changed rows), not O(scope size).
/// </summary>
/// <param name="Scopes">The per-scope parts of the change.</param>
internal sealed record UndoEntry(IReadOnlyList<ScopeDelta> Scopes)
{
    /// <summary>
    /// The same change seen from the other side: undo applies the before-side and moves this
    /// swapped form to the redo stack, where redo applies the swapped before-side — the original
    /// after-state.
    /// </summary>
    /// <returns>The entry with every row delta's before and after exchanged.</returns>
    public UndoEntry Swapped() =>
        new([.. Scopes.Select(scope => scope with
        {
            Rows = [.. scope.Rows.Select(row => row with { Before = row.After, After = row.Before })]
        })]);
}

/// <summary>
/// One scope's part of an <see cref="UndoEntry"/>.
/// </summary>
/// <param name="Type">The scope the deltas belong to.</param>
/// <param name="Rows">The row changes, in the order the mutation made them.</param>
internal sealed record ScopeDelta(EnvironmentalVariableType Type, IReadOnlyList<RowDelta> Rows);

/// <summary>
/// One row's change inside a <see cref="ScopeDelta"/>: <see cref="Before"/> null means the change
/// added the row, <see cref="After"/> null means it removed the row, otherwise both are set and
/// the row was modified in place.
/// </summary>
/// <param name="Model">The row instance; restores write back into it, so grid references stay valid.</param>
/// <param name="Index">The row's position in the scope's list at the time of the change; a fallback only.</param>
/// <param name="Anchor">
/// For a removal, the nearest preceding row that survives the same entry, resolved at push time.
/// Reinsertion happens after the anchor's current position, so it stays correct even when the
/// list was reordered afterwards (an unrecorded <c>Sort</c>), which would stale <paramref name="Index"/>.
/// </param>
/// <param name="Before">The row's fields before the change, or null when the change added it.</param>
/// <param name="After">The row's fields after the change, or null when the change removed it.</param>
internal sealed record RowDelta(EnvModel Model, int Index, EnvModel? Anchor, RowFields? Before, RowFields? After);

/// <summary>
/// One row's field values at one moment. Records compare by value, so a mutation that moved no
/// field records nothing.
/// </summary>
internal sealed record RowFields(
    string Name,
    string OriginalName,
    string Path,
    string OriginalPath,
    EnvironmentalVariableState State)
{
    /// <summary>Captures a model's current field values.</summary>
    /// <param name="model">The row to read.</param>
    /// <returns>Its fields as a comparable record.</returns>
    public static RowFields Of(EnvModel model) =>
        new(model.Name, model.OriginalName, model.Path, model.OriginalPath, model.State);
}

/// <summary>
/// The undo/redo stacks for the whole workspace. There is one global stack, not one per scope:
/// a single action can touch both scopes (an import, an apply run), and one order keeps undo/redo
/// in the exact sequence the actions happened. The stacks are bounded and hold delta records only.
/// </summary>
internal sealed class UndoHistory : IUndoHistory
{
    /// <summary>
    /// How many changes the undo stack keeps; the oldest is dropped beyond it.
    /// </summary>
    private const int Limit = 100;

    private readonly Stack<UndoEntry> _undo = [];
    private readonly Stack<UndoEntry> _redo = [];

    // While a batch is open, the per-scope delta lists merge into the batch's single entry.
    private Dictionary<EnvironmentalVariableType, ScopeDelta>? _batch;
    private int _batchDepth;

    /// <summary>
    /// Whether there is a recorded change that <see cref="Undo"/> can restore.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// Whether an undone change is waiting for <see cref="Redo"/>.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public event EventHandler? StackChanged;

    /// <summary>
    /// Records one change and clears the redo stack, so a new action ends the undone branch.
    /// Inside a batch the change merges into the batch's pending entry instead.
    /// </summary>
    /// <param name="entry">The change to record.</param>
    public void Push(UndoEntry entry)
    {
        if (_batch is { } batch)
        {
            // First push per scope starts the delta list; every push appends, so the merged
            // entry spans the whole batch.
            foreach (var delta in entry.Scopes)
            {
                batch[delta.Type] = batch.TryGetValue(delta.Type, out var merged)
                    ? merged with { Rows = [.. merged.Rows, .. delta.Rows] }
                    : delta;
            }

            return;
        }

        _undo.Push(entry);

        if (_undo.Count > Limit)
            TrimOldest(_undo);

        _redo.Clear();
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pops the most recent change and moves its swapped form to the redo stack.
    /// </summary>
    /// <returns>The entry to apply, or null when there is nothing to undo.</returns>
    public UndoEntry? Undo()
    {
        if (!_undo.TryPop(out var entry))
            return null;

        _redo.Push(entry.Swapped());
        StackChanged?.Invoke(this, EventArgs.Empty);

        return entry;
    }

    /// <summary>
    /// Pops the most recently undone change and moves its swapped form back to the undo stack.
    /// </summary>
    /// <returns>The entry to apply, or null when there is nothing to redo.</returns>
    public UndoEntry? Redo()
    {
        if (!_redo.TryPop(out var entry))
            return null;

        _undo.Push(entry.Swapped());
        StackChanged?.Invoke(this, EventArgs.Empty);

        return entry;
    }

    /// <summary>
    /// Drops every recorded change; the workspace was reloaded, so old deltas are meaningless.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _batch?.Clear();
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens a batch: pushes until dispose merge into one entry, one undo step for a multi-scope action.
    /// </summary>
    /// <returns>A handle that commits the merged entry on dispose.</returns>
    public IDisposable BeginBatch()
    {
        _batchDepth++;
        _batch ??= [];

        return new Batch(this);
    }

    /// <summary>
    /// Drops the oldest entry of a stack that grew past the limit. A stack has no random access,
    /// so trimming the bottom rebuilds it; <see cref="Stack{T}.ToArray"/> copies newest first.
    /// </summary>
    /// <param name="stack">The over-limit stack.</param>
    private static void TrimOldest(Stack<UndoEntry> stack)
    {
        var entries = stack.ToArray();
        stack.Clear();

        for (var i = entries.Length - 2; i >= 0; i--)
            stack.Push(entries[i]);
    }

    /// <summary>
    /// Commits the batch's merged entry when the outermost batch is disposed.
    /// </summary>
    private void EndBatch()
    {
        if (--_batchDepth > 0)
            return;

        var merged = _batch is { Count: > 0 } batch
            ? new UndoEntry([.. batch.Values])
            : null;

        _batch = null;

        if (merged is not null)
            Push(merged);
    }

    /// <summary>
    /// Commits the owning history's batch on dispose.
    /// </summary>
    /// <param name="owner">The history whose batch this handle closes.</param>
    private sealed class Batch(UndoHistory owner) : IDisposable
    {
        public void Dispose() => owner.EndBatch();
    }
}
