using enviro.Models;
using enviro.Static;
using System.ComponentModel;

namespace enviro.Services;

/// <summary>
/// Service interface for the in-memory variable store: the two scopes plus the operations that
/// span both of them.
/// </summary>
internal interface IEnvService
{
    /// <summary>
    /// Gets the user-level scope.
    /// </summary>
    IEnvScope User { get; }

    /// <summary>
    /// Gets the machine-level scope.
    /// </summary>
    IEnvScope Machine { get; }

    /// <summary>
    /// Gets the scope that owns the rows of the given variable type.
    /// </summary>
    /// <param name="type">The type of environmental variable (User or Machine).</param>
    /// <returns>The matching scope.</returns>
    IEnvScope GetScope(EnvironmentalVariableType type);

    /// <summary>
    /// Determines whether any environmental variables have been modified.
    /// </summary>
    /// <returns>True if there are changes, false otherwise.</returns>
    bool HasChanges();

    /// <summary>
    /// Whether the workspace has a recorded change that <see cref="Undo"/> can restore.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether an undone change is waiting for <see cref="Redo"/>.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Restores the workspace to the state before the most recent recorded change. Only the
    /// in-memory rows are touched; the registry is never written.
    /// </summary>
    void Undo();

    /// <summary>
    /// Re-applies the most recently undone change to the in-memory rows.
    /// </summary>
    void Redo();

    /// <summary>
    /// Re-reads both scopes from the system, discarding every in-memory edit. The refreshed
    /// lists are in name order and every model starts as <c>Unchanged</c> with no corrupted overlay.
    /// A read that throws leaves the previous rows in place. The undo history is cleared, because
    /// no snapshot of the discarded rows is meaningful anymore.
    /// </summary>
    void Reload();
}

/// <summary>
/// Which case an <see cref="IEnvScope.Upsert"/> resolved to.
/// </summary>
internal enum UpsertOutcome
{
    /// <summary>
    /// The name was not in the scope; a new row was added.
    /// </summary>
    Added,

    /// <summary>
    /// The name already had a row; it now holds the new value (a pending deletion was undone).
    /// </summary>
    Overridden,

    /// <summary>
    /// The name already had a row that is not deleted and overwriting was not requested:
    /// nothing was mutated.
    /// </summary>
    Refused,
}

/// <summary>
/// What one <see cref="IEnvScope.Import"/> did, so the shell can report it without inspecting rows.
/// </summary>
/// <param name="Added">Entries that became new rows.</param>
/// <param name="Overwritten">Entries that updated an existing row (a pending deletion was restored).</param>
/// <param name="Unchanged">
/// Entries that left their row at its baseline: the identical value (row untouched), or the
/// baseline value after a pending edit or deletion was cancelled.
/// </param>
/// <param name="Skipped">Entries rejected by the input rules; nothing was created for them.</param>
internal sealed record ImportOutcome(int Added, int Overwritten, int Unchanged, int Skipped);

/// <summary>
/// One variable scope: the rows that belong to <see cref="Type"/> plus every operation bound to
/// that scope. The rows are handed out read-only, so nothing outside the service can add, remove
/// or reorder them. Every mutation is recorded on the undo history as a delta of the rows it
/// actually touched, so the whole workspace can be undone and redone step by step.
/// </summary>
internal interface IEnvScope
{
    /// <summary>
    /// Gets the variable type these rows belong to.
    /// </summary>
    EnvironmentalVariableType Type { get; }

    /// <summary>
    /// Gets the service-owned rows in display order. Grids bind to this list directly, so every
    /// change reaches them as <see cref="IBindingList.ListChanged"/>.
    /// </summary>
    IBindingList Rows { get; }

    /// <summary>
    /// Adds a new environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to add.</param>
    void Add(EnvModel pm);

    /// <summary>
    /// Merges externally created variables into the bound list: rows whose name (or original
    /// name) no row of this scope holds are appended without an undo delta — they are system
    /// facts, not user edits. Duplicates are skipped silently. One change notification for the
    /// whole batch.
    /// </summary>
    /// <param name="rows">The rows the live system holds but the workspace does not.</param>
    void AddExternal(IReadOnlyList<EnvModel> rows);

    /// <summary>
    /// Updates the path value of an environmental variable and marks it as modified.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="newValue">The new value.</param>
    void Update(EnvModel model, string newValue);

    /// <summary>
    /// Resolves the whole create path in one call: a new name becomes an <c>Added</c> row, a name
    /// whose deletion is still pending comes back with the new value, and a live row takes the new
    /// value. It neither prompts nor reports.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value to store under that name.</param>
    /// <param name="overwrite">
    /// False refuses to touch a row that is not deleted, so the caller's prompt cannot be bypassed.
    /// </param>
    /// <returns>Which case happened; <see cref="UpsertOutcome.Refused"/> mutates nothing.</returns>
    UpsertOutcome Upsert(string name, string value, bool overwrite);

    /// <summary>
    /// Merges a set of entries into this scope: new names become rows, existing names take the
    /// value (a pending deletion is restored), and entries the input rules reject are skipped. An
    /// entry that carries a row's baseline cancels that row's pending edit or deletion, and two
    /// identical values leave the row untouched. Raises one change notification for the whole batch.
    /// </summary>
    /// <param name="entries">The name/value pairs to merge, in file order.</param>
    /// <returns>What the merge did, per entry.</returns>
    ImportOutcome Import(IReadOnlyList<KeyValuePair<string, string>> entries);

    /// <summary>
    /// Resolves the whole edit path in one call: renames and revalues one row, or rejects the edit
    /// without touching any model. The rename is applied as part of it, never separately.
    /// </summary>
    /// <param name="model">The row to edit; it must be one of this scope's rows.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>
    /// The row that now holds the edit, or null and the user-facing reason it was rejected
    /// (blank name, collision, unknown row or a pending deletion).
    /// </returns>
    (EnvModel? Model, string? Rejection) ApplyEdit(EnvModel model, string newName, string newValue);

    /// <summary>
    /// Removes an environmental variable entry, or marks it as deleted when it was already written.
    /// </summary>
    /// <param name="pm">The model to remove.</param>
    void Delete(EnvModel pm);

    /// <summary>
    /// Checks if a variable with the same name exists in this scope.
    /// </summary>
    /// <param name="name">The name to check, compared case-insensitively.</param>
    /// <returns>True if the variable exists, false otherwise.</returns>
    bool Contains(string name);

    /// <summary>
    /// Finds an environmental variable by name in this scope.
    /// </summary>
    /// <param name="name">The name to search for, compared case-insensitively.</param>
    /// <returns>The model if found, null otherwise.</returns>
    EnvModel? GetByName(string name);

    /// <summary>
    /// Resets a modified environmental variable to its baseline: the original name and value.
    /// </summary>
    /// <param name="pm">The model to reset.</param>
    void Reset(EnvModel pm);

    /// <summary>
    /// Restores a deleted environmental variable by marking it as unchanged under its original name.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    void Restore(EnvModel pm);

    /// <summary>
    /// Cleans up a row the adapter just wrote: a written deletion leaves the list, anything else
    /// returns to <c>Unchanged</c> with its baseline synced.
    /// </summary>
    /// <param name="model">The row that was just written.</param>
    /// <param name="appliedState">The state the row had when it was written.</param>
    void MarkApplied(EnvModel model, EnvironmentalVariableState appliedState);

    /// <summary>
    /// Accepts one row's external change into the workspace, so the registry becomes the new
    /// baseline: the live value is taken over like any other edit (one undo step), and the drift
    /// overlay is cleared. A row whose variable was deleted externally becomes a pending deletion.
    /// </summary>
    /// <param name="pm">The drifted row to accept.</param>
    void AcceptDrift(EnvModel pm);

    /// <summary>
    /// Re-orders the rows by one model property. The list is cleared and refilled, so callers that
    /// care about the view capture and restore the grid position around this call.
    /// </summary>
    /// <param name="propertyName">The model property to order by.</param>
    /// <param name="direction">Ascending or descending.</param>
    void Sort(string propertyName, ListSortDirection direction);
}

/// <summary>
/// The service-owned rows of one scope and every operation bound to it. Every mutating operation
/// runs through <c>Mutate</c>, which records a delta of the rows the call touched on the undo
/// history, so one user action becomes one undo step.
/// </summary>
/// <param name="type">The scope these rows belong to.</param>
/// <param name="history">The undo history every mutation of this scope is recorded on.</param>
internal sealed class EnvScope(EnvironmentalVariableType type, IUndoHistory history) : IEnvScope
{
    private readonly BindingList<EnvModel> rows = [];

    // While a mutation runs inside another one (an upsert that restores and then updates a row),
    // both append to this one list: only the outermost level pushes, so nested calls stay one step.
    private List<RowDelta>? _currentDeltas;

    // Rows whose registry key an applied deletion removed and no later write re-created: their
    // system baseline is "absent", which a pending Deleted row's originals cannot express, so
    // undoing past that apply must not claim the row matches the system.
    private readonly HashSet<EnvModel> _appliedDeleted = [];

    /// <summary>
    /// Gets the variable type these rows belong to.
    /// </summary>
    public EnvironmentalVariableType Type => type;

    /// <summary>
    /// Gets the rows in display order.
    /// </summary>
    public IBindingList Rows => rows;

    /// <summary>
    /// Determines whether any row in this scope is not <c>Unchanged</c>.
    /// </summary>
    /// <returns>True if a row holds a pending change.</returns>
    public bool HasChanges() => rows.Any(v => v.State != EnvironmentalVariableState.Unchanged);

    /// <summary>
    /// Replaces the rows with a fresh snapshot, raising a single change notification instead of one
    /// per row.
    /// </summary>
    /// <param name="newRows">The new rows, in display order.</param>
    public void Replace(List<EnvModel> newRows)
    {
        // Fresh rows came from the system, so no old applied deletion still applies to them.
        _appliedDeleted.Clear();
        Refill(newRows);
    }

    /// <summary>
    /// Clears and refills the bound list with a single change notification: the only place that
    /// touches the rows in bulk. It stays private, so a refill is never reachable from outside the
    /// scope.
    /// </summary>
    /// <param name="newRows">The new rows, in display order.</param>
    /// <param name="beforeRefill">
    /// An optional action run while notifications are suppressed, e.g. restoring field values on
    /// the rows that are about to be refilled.
    /// </param>
    private void Refill(IReadOnlyList<EnvModel> newRows, Action? beforeRefill = null)
    {
        rows.RaiseListChangedEvents = false;
        try
        {
            beforeRefill?.Invoke();

            rows.Clear();

            foreach (var row in newRows)
            {
                rows.Add(row);
            }
        }
        finally
        {
            rows.RaiseListChangedEvents = true;
        }

        // Listeners do their O(n) work once (filter, action state, status strip) instead of per row.
        rows.ResetBindings();
    }

    /// <summary>
    /// Records one in-place row change on the running mutation's delta list, but only when it
    /// actually moved a field — a refused or no-op call records nothing and leaves no empty step.
    /// </summary>
    /// <param name="deltas">The delta list of the running mutation.</param>
    /// <param name="model">The row that changed.</param>
    /// <param name="before">The row's fields before the change.</param>
    private void Record(List<RowDelta> deltas, EnvModel model, RowFields before)
    {
        var after = RowFields.Of(model);

        if (before != after)
            deltas.Add(new(model, rows.IndexOf(model), null, before, after));
    }

    /// <summary>
    /// Runs one workspace mutation with undo recording: the mutation appends one
    /// <see cref="RowDelta"/> per row it touched, and the delta is pushed onto the undo history
    /// only when the list is non-empty. Nested calls append to the same list, so one user action
    /// stays one undo step.
    /// </summary>
    /// <param name="mutation">The mutation to run; it records its row changes on the given list.</param>
    private void Mutate(Action<List<RowDelta>> mutation) => Mutate<object?>(deltas =>
    {
        mutation(deltas);
        return null;
    });

    /// <summary>
    /// Runs one workspace mutation with undo recording; see <c>Mutate</c>.
    /// </summary>
    /// <typeparam name="T">The mutation's return type.</typeparam>
    /// <param name="mutation">The mutation to run; it records its row changes on the given list.</param>
    /// <returns>Whatever the mutation returns.</returns>
    private T Mutate<T>(Func<List<RowDelta>, T> mutation)
    {
        if (_currentDeltas is { } outer)
            return mutation(outer);

        var deltas = new List<RowDelta>();
        _currentDeltas = deltas;

        try
        {
            return mutation(deltas);
        }
        finally
        {
            _currentDeltas = null;

            if (deltas.Count > 0)
            {
                // Resolve each removal's anchor to the nearest preceding row that survives this
                // same entry, so reinsertion never lands inside a removed run.
                var removed = deltas
                    .Where(d => d.After is null)
                    .ToDictionary(d => d.Model, d => d.Anchor);

                var resolved = new List<RowDelta>(deltas.Count);

                foreach (var delta in deltas)
                {
                    if (delta.After is not null)
                    {
                        resolved.Add(delta);
                        continue;
                    }

                    var anchor = delta.Anchor;

                    while (anchor is not null && removed.TryGetValue(anchor, out var next))
                        anchor = next;

                    resolved.Add(delta with { Anchor = anchor });
                }

                history.Push(new UndoEntry([new ScopeDelta(type, resolved)]));
            }
        }
    }

    /// <summary>
    /// Applies one scope delta's before-side to this scope: rows the change added leave, rows it
    /// removed come back at their recorded position, and every touched row gets its recorded
    /// fields written back into the same model instance, so grid references stay valid. Undo
    /// replays the deltas in reverse order, so the recorded positions stay meaningful; redo gets
    /// the swapped entry and replays its before-side in forward order, which is the original
    /// after-state in mutation order. One change notification is raised for the whole restore.
    /// The written fields are reconciled against the system baseline the row currently reflects
    /// (see <see cref="WriteFields"/>), so undoing past an apply leaves the row pending against
    /// the applied values instead of falsely <c>Unchanged</c>.
    /// Like <see cref="Replace"/>, it is reachable only from <see cref="EnvService"/>.
    /// </summary>
    /// <param name="delta">The scope delta whose before-side is applied.</param>
    /// <param name="forward">False for undo (reverse order), true for redo (mutation order).</param>
    public void ApplyDelta(ScopeDelta delta, bool forward)
    {
        rows.RaiseListChangedEvents = false;

        try
        {
            var count = delta.Rows.Count;

            for (var n = 0; n < count; n++)
            {
                var row = delta.Rows[forward ? n : count - 1 - n];

                if (row.Before is null)
                {
                    rows.Remove(row.Model);
                    continue;
                }

                if (row.After is null)
                {
                    // Reinsert after the anchor's current position; the recorded Index is only a
                    // fallback, because an unrecorded Sort may have moved the list since.
                    var at = row.Anchor is { } anchor && rows.Contains(anchor)
                        ? rows.IndexOf(anchor) + 1
                        : Math.Clamp(row.Index, 0, rows.Count);

                    rows.Insert(at, row.Model);
                }

                WriteFields(row.Model, row.Before);
            }
        }
        finally
        {
            rows.RaiseListChangedEvents = true;
        }

        rows.ResetBindings();
    }

    /// <summary>
    /// Writes a recorded field set back into a live model. The state is reconciled against the
    /// system baseline the row currently reflects — an apply moves that baseline, so a restore
    /// that claims <c>Unchanged</c> must still match what the system actually holds, otherwise
    /// the row is pending against it (<c>Modified</c>, or <c>Added</c> when the system no longer
    /// has the name at all) and Apply can write the rollback.
    /// </summary>
    /// <param name="model">The row instance to update.</param>
    /// <param name="fields">The recorded field values.</param>
    private void WriteFields(EnvModel model, RowFields fields)
    {
        var current = RowFields.Of(model);

        // What the system holds for this row right now: an Unchanged row matches it through its
        // live values, any other applied state through its originals; an Added row — or a row
        // whose applied deletion was undone — is not in the system at all.
        var absent = current.State == EnvironmentalVariableState.Added
            || (current.State == EnvironmentalVariableState.Deleted && _appliedDeleted.Contains(model));
        var baseName = absent ? null
            : current.State == EnvironmentalVariableState.Unchanged ? current.Name : current.OriginalName;
        var basePath = absent ? null
            : current.State == EnvironmentalVariableState.Unchanged ? current.Path : current.OriginalPath;

        model.Name = fields.Name;
        model.Path = fields.Path;

        if (fields.State == EnvironmentalVariableState.Deleted)
        {
            model.OriginalName = fields.OriginalName;
            model.OriginalPath = fields.OriginalPath;
            model.State = fields.State;
            return;
        }

        model.OriginalName = baseName ?? fields.OriginalName;
        model.OriginalPath = basePath ?? fields.OriginalPath;
        model.State = baseName is null
            ? EnvironmentalVariableState.Added
            : fields.Name == baseName && fields.Path == basePath
                ? EnvironmentalVariableState.Unchanged
                : fields.State == EnvironmentalVariableState.Added
                    ? EnvironmentalVariableState.Added
                    : EnvironmentalVariableState.Modified;
    }

    /// <summary>
    /// Adds a new environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to add.</param>
    public void Add(EnvModel pm) => Mutate(deltas =>
    {
        pm.State = EnvironmentalVariableState.Added;
        pm.OriginalName = pm.Name;
        pm.OriginalPath = pm.Path;

        rows.Add(pm);
        deltas.Add(new(pm, rows.Count - 1, null, null, RowFields.Of(pm)));
    });

    /// <summary>
    /// Merges externally created variables into the bound list: rows whose name (or original
    /// name) no row of this scope holds are appended without an undo delta — they are system
    /// facts the registry holds, not user edits, so there is nothing to undo. Duplicates are
    /// skipped silently; the batch raises one change notification, like Replace/Import.
    /// </summary>
    /// <param name="newRows">The rows the live system holds but the workspace does not.</param>
    public void AddExternal(IReadOnlyList<EnvModel> newRows)
    {
        var byName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            byName.Add(row.Name);
            byName.Add(row.OriginalName);
        }

        var added = false;
        rows.RaiseListChangedEvents = false;

        try
        {
            foreach (var row in newRows)
            {
                if (!byName.Add(row.Name))
                    continue;

                rows.Add(row);
                added = true;
            }
        }
        finally
        {
            rows.RaiseListChangedEvents = true;
        }

        if (added)
            rows.ResetBindings();
    }

    /// <summary>
    /// Updates the path value of an environmental variable and marks it as modified.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="newValue">The new value.</param>
    public void Update(EnvModel model, string newValue) => Mutate(deltas =>
    {
        if (model.State == EnvironmentalVariableState.Deleted)
            return;

        var before = RowFields.Of(model);

        model.Path = newValue;

        if (model.State != EnvironmentalVariableState.Added)
            model.State = EnvironmentalVariableState.Modified;

        Record(deltas, model, before);
    });

    /// <summary>
    /// Resolves the whole create path: adds a new row, restores a pending deletion or overwrites
    /// a live row.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value to store under that name.</param>
    /// <param name="overwrite">False refuses to touch a row that is not deleted.</param>
    /// <returns>Which case happened; a refused upsert mutates nothing.</returns>
    public UpsertOutcome Upsert(string name, string value, bool overwrite) =>
        Mutate(deltas => ResolveUpsert(GetByName(name), name, value, overwrite, out _));

    /// <summary>
    /// The create path once the row holding <paramref name="name"/> has been resolved: adds a new
    /// row, restores a pending deletion or overwrites a live row. Split out so a caller that already
    /// holds the row — the import batch, through its own name index — reaches the same rule without
    /// a second name scan.
    /// </summary>
    /// <param name="existing">The row holding the name, or null when the scope has none.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value to store under that name.</param>
    /// <param name="overwrite">False refuses to touch a row that is not deleted.</param>
    /// <param name="row">The row the call added or touched.</param>
    /// <returns>Which case happened; a refused upsert mutates nothing.</returns>
    private UpsertOutcome ResolveUpsert(
        EnvModel? existing, string name, string value, bool overwrite, out EnvModel row)
    {
        if (existing is null)
        {
            row = new EnvModel { Name = name, Path = value };
            Add(row);
            return UpsertOutcome.Added;
        }

        row = existing;

        if (existing.State == EnvironmentalVariableState.Deleted)
        {
            // Restore first: Update is a no-op on a deleted row, so the order carries the value.
            Restore(existing);
            Update(existing, value);
            return UpsertOutcome.Overridden;
        }

        if (!overwrite)
            return UpsertOutcome.Refused;

        Update(existing, value);
        return UpsertOutcome.Overridden;
    }

    /// <summary>
    /// Merges a set of entries into this scope, raising one change notification for the batch.
    /// </summary>
    /// <param name="entries">The name/value pairs to merge, in file order.</param>
    /// <returns>What the merge did, per entry.</returns>
    public ImportOutcome Import(IReadOnlyList<KeyValuePair<string, string>> entries) =>
        Mutate(_ => ImportCore(entries));

    /// <summary>
    /// The merge itself, without the undo recording; see <see cref="Import"/>.
    /// </summary>
    /// <param name="entries">The name/value pairs to merge, in file order.</param>
    /// <returns>What the merge did, per entry.</returns>
    private ImportOutcome ImportCore(IReadOnlyList<KeyValuePair<string, string>> entries)
    {
        var added = 0;
        var overwritten = 0;
        var unchanged = 0;
        var skipped = 0;

        // One refresh for the whole file, like Replace/Sort's refill: a 500-row file is one
        // notification instead of 500.
        rows.RaiseListChangedEvents = false;

        // One name index for the batch: GetByName is a linear scan, so resolving every entry through
        // it makes a large file quadratic (a 100k-entry import took 85 s on the UI thread). Rows
        // added below are put into the index too, so a name repeated later in the same file still
        // resolves to the row this batch created.
        var byName = new Dictionary<string, EnvModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
            byName[row.Name] = row;

        try
        {
            foreach (var (name, value) in entries)
            {
                if (!ValidationHelper.IsValidEnvironmentVariableInput(name, value))
                {
                    skipped++;
                    continue;
                }

                byName.TryGetValue(name, out var existing);

                if (existing is not null)
                {
                    // The entry carries the row's baseline — the name and value the registry holds.
                    // Importing it cancels a pending edit or deletion, so the row returns to
                    // Unchanged instead of staying dirty with a value that would change nothing.
                    if (existing.State != EnvironmentalVariableState.Added
                        && string.Equals(existing.OriginalName, name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.OriginalPath, value, StringComparison.Ordinal))
                    {
                        if (existing.State != EnvironmentalVariableState.Unchanged)
                            Reset(existing);

                        unchanged++;
                        continue;
                    }

                    // The file already says what the row says: leave the row (and its state) alone.
                    if (existing.State != EnvironmentalVariableState.Deleted
                        && string.Equals(existing.Path, value, StringComparison.Ordinal))
                    {
                        unchanged++;
                        continue;
                    }
                }

                // Import merges and overwrites; it never deletes, so an upsert cannot refuse.
                var outcome = ResolveUpsert(existing, name, value, overwrite: true, out var resolved);

                // That call can restore a deleted row under its baseline name, so the index follows
                // the name the row holds now instead of the entry's spelling.
                byName.Remove(name);
                byName[resolved.Name] = resolved;

                if (outcome == UpsertOutcome.Added)
                    added++;
                else
                    overwritten++;
            }
        }
        finally
        {
            rows.RaiseListChangedEvents = true;
        }

        rows.ResetBindings();

        return new ImportOutcome(added, overwritten, unchanged, skipped);
    }

    /// <summary>
    /// Resolves the whole edit path: renames and revalues one row, or rejects the edit without
    /// touching any model.
    /// </summary>
    /// <param name="model">The row to edit.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>The row that now holds the edit, or null and the reason it was rejected.</returns>
    public (EnvModel? Model, string? Rejection) ApplyEdit(EnvModel model, string newName, string newValue) =>
        Mutate(deltas => ApplyEditCore(deltas, model, newName, newValue));

    /// <summary>
    /// The edit rules themselves, without the undo recording; see <see cref="ApplyEdit"/>.
    /// </summary>
    /// <param name="deltas">The delta list of the running mutation, for the rename's row change.</param>
    /// <param name="model">The row to edit.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>The row that now holds the edit, or null and the reason it was rejected.</returns>
    private (EnvModel? Model, string? Rejection) ApplyEditCore(List<RowDelta> deltas, EnvModel model, string newName, string newValue)
    {
        // Reference check: a model another scope (or a stale snapshot) holds is not editable here.
        if (!rows.Contains(model))
            return (null, "The variable is no longer in this list.");

        if (model.State == EnvironmentalVariableState.Deleted)
            return (null, "Restore the variable before editing it.");

        // A blank name has its own reason, so a caller can tell it apart from a collision.
        if (string.IsNullOrWhiteSpace(newName))
            return (null, "The name cannot be empty.");

        // Rename rejects an unknown row and a collision with another variable.
        var beforeRename = RowFields.Of(model);
        var target = Rename(model.Name, newName);

        if (target is null)
            return (null, $"Cannot rename to '{newName}': the name is already in use.");

        Record(deltas, model, beforeRename);

        Update(target, newValue);

        return (target, null);
    }

    /// <summary>
    /// Renames an environmental variable in place; the primitive inside <see cref="ApplyEdit"/>.
    /// </summary>
    /// <param name="oldName">The current name.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>The model that now holds the new name, or null when the rename was rejected.</returns>
    private EnvModel? Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return null;

        var foundModel = GetByName(oldName);

        if (foundModel == null)
            return null;

        var target = GetByName(newName);

        if (target != null && target != foundModel)
            return null;

        foundModel.Name = newName;

        // A persisted row has to reach the registry, so the rename makes it Modified; an addition
        // that has not been written yet stays Added.
        if (foundModel.State != EnvironmentalVariableState.Added)
            foundModel.State = EnvironmentalVariableState.Modified;

        return foundModel;
    }

    /// <summary>
    /// Removes an environmental variable entry or marks it as deleted.
    /// </summary>
    /// <param name="pm">The model to remove.</param>
    public void Delete(EnvModel pm) => Mutate(deltas =>
    {
        if (pm.State == EnvironmentalVariableState.Added)
        {
            var index = rows.IndexOf(pm);
            var anchor = index > 0 ? rows[index - 1] : null;
            rows.Remove(pm);
            deltas.Add(new(pm, index, anchor, RowFields.Of(pm), null));
            return;
        }

        var before = RowFields.Of(pm);
        pm.State = EnvironmentalVariableState.Deleted;
        Record(deltas, pm, before);
    });

    /// <summary>
    /// Checks if a variable with the same name exists in this scope.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if a variable with the same name exists.</returns>
    public bool Contains(string name) =>
        rows.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds an environmental variable by name in this scope.
    /// </summary>
    /// <param name="name">The name to search for.</param>
    /// <returns>The model if found, null otherwise.</returns>
    public EnvModel? GetByName(string name) =>
        rows.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resets a modified environmental variable to its baseline: the original name and value.
    /// </summary>
    /// <param name="pm">The model to reset.</param>
    public void Reset(EnvModel pm) => Mutate(deltas =>
    {
        var before = RowFields.Of(pm);
        pm.Name = pm.OriginalName;
        pm.Path = pm.OriginalPath;
        pm.State = _appliedDeleted.Remove(pm)
            ? EnvironmentalVariableState.Added
            : EnvironmentalVariableState.Unchanged;
        Record(deltas, pm, before);
    });

    /// <summary>
    /// Restores a deleted environmental variable by marking it as unchanged under its original name.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    public void Restore(EnvModel pm) => Mutate(deltas =>
    {
        var before = RowFields.Of(pm);
        pm.Name = pm.OriginalName;
        // A row whose applied deletion was undone is absent from the system, so restoring it
        // means creating it again, not claiming it is unchanged.
        pm.State = _appliedDeleted.Remove(pm)
            ? EnvironmentalVariableState.Added
            : EnvironmentalVariableState.Unchanged;
        Record(deltas, pm, before);
    });

    /// <summary>
    /// Sets a written row back to <c>Unchanged</c> and syncs its baseline; a deleted row leaves the list.
    /// </summary>
    /// <param name="model">The row that was just written.</param>
    /// <param name="appliedState">The state the row had when it was written.</param>
    public void MarkApplied(EnvModel model, EnvironmentalVariableState appliedState) => Mutate(deltas =>
    {
        if (appliedState == EnvironmentalVariableState.Deleted)
        {
            var index = rows.IndexOf(model);
            var anchor = index > 0 ? rows[index - 1] : null;
            rows.Remove(model);
            _appliedDeleted.Add(model);
            deltas.Add(new(model, index, anchor, RowFields.Of(model), null));
            return;
        }

        var before = RowFields.Of(model);
        model.State = EnvironmentalVariableState.Unchanged;
        model.OriginalName = model.Name;
        model.OriginalPath = model.Path;
        // The row now matches the system again, so any drift flag from before the write is stale.
        model.IsDrifted = false;
        model.LiveValue = null;
        _appliedDeleted.Remove(model);
        Record(deltas, model, before);
    });

    /// <summary>
    /// Accepts one row's external change into the workspace through <see cref="Mutate"/>, so it is
    /// one undo step like every other mutation. A live value becomes the row's new value and
    /// baseline (a pending edit or rename of that row is dropped); a live deletion becomes a
    /// pending deletion, because the row the workspace holds then matches nothing the system has.
    /// A row that is not drifted is a no-op.
    /// </summary>
    /// <param name="pm">The drifted row to accept.</param>
    public void AcceptDrift(EnvModel pm) => Mutate(deltas =>
    {
        if (!pm.IsDrifted)
            return;

        var before = RowFields.Of(pm);

        if (pm.LiveValue is { } live)
        {
            pm.Path = live;
            pm.OriginalPath = live;
            pm.State = EnvironmentalVariableState.Unchanged;
        }
        else
        {
            pm.State = EnvironmentalVariableState.Deleted;
        }

        // The workspace now agrees with the registry again, so the overlay is stale.
        pm.IsDrifted = false;
        pm.LiveValue = null;

        Record(deltas, pm, before);
    });

    /// <summary>
    /// Re-orders the rows by one model property, clearing and refilling the list.
    /// </summary>
    /// <param name="propertyName">The model property to order by.</param>
    /// <param name="direction">Ascending or descending.</param>
    public void Sort(string propertyName, ListSortDirection direction)
    {
        var property = typeof(EnvModel).GetProperty(propertyName);

        var sortedRows = direction == ListSortDirection.Ascending
            ? rows.OrderBy(m => property?.GetValue(m)).ToList()
            : rows.OrderByDescending(m => property?.GetValue(m)).ToList();

        Refill(sortedRows);
    }
}

/// <summary>
/// Service for managing environmental variables, including adding, removing, updating, and tracking changes.
/// </summary>
internal sealed class EnvService : IEnvService
{
    private readonly IUndoHistory _undoHistory;
    private readonly EnvScope user;
    private readonly EnvScope machine;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvService"/> class.
    /// Loads all environmental variables from the system.
    /// </summary>
    /// <param name="undoHistory">The history every workspace mutation is recorded on.</param>
    public EnvService(IUndoHistory undoHistory)
    {
        _undoHistory = undoHistory;
        user = new EnvScope(EnvironmentalVariableType.User, undoHistory);
        machine = new EnvScope(EnvironmentalVariableType.Machine, undoHistory);

        Reload();
    }

    /// <summary>
    /// Gets the user-level scope.
    /// </summary>
    public IEnvScope User => user;

    /// <summary>
    /// Gets the machine-level scope.
    /// </summary>
    public IEnvScope Machine => machine;

    /// <summary>
    /// Gets the scope that owns the rows of the given variable type.
    /// </summary>
    /// <param name="type">The type of environmental variable (User or Machine).</param>
    /// <returns>The matching scope.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type is neither <see cref="EnvironmentalVariableType.User"/> nor
    /// <see cref="EnvironmentalVariableType.Machine"/>.
    /// </exception>
    public IEnvScope GetScope(EnvironmentalVariableType type) => ScopeOf(type);

    /// <summary>
    /// The concrete scope behind a variable type, for callers that need the scope's own members
    /// (like the snapshot restore). Exhaustive over <c>User</c>/<c>Machine</c> and fail-closed for
    /// anything else, exactly like <see cref="GetScope"/>.
    /// </summary>
    /// <param name="type">The type of environmental variable (User or Machine).</param>
    /// <returns>The matching scope.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type is neither <see cref="EnvironmentalVariableType.User"/> nor
    /// <see cref="EnvironmentalVariableType.Machine"/>.
    /// </exception>
    private EnvScope ScopeOf(EnvironmentalVariableType type) =>
        type switch
        {
            EnvironmentalVariableType.User => user,
            EnvironmentalVariableType.Machine => machine,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown environmental variable type."),
        };

    /// <summary>
    /// Determines whether any environmental variables have been modified.
    /// </summary>
    /// <returns>True if any user or machine variables are not in unchanged state.</returns>
    public bool HasChanges() => user.HasChanges() || machine.HasChanges();

    /// <summary>
    /// Whether the workspace has a recorded change that <see cref="Undo"/> can restore.
    /// </summary>
    public bool CanUndo => _undoHistory.CanUndo;

    /// <summary>
    /// Whether an undone change is waiting for <see cref="Redo"/>.
    /// </summary>
    public bool CanRedo => _undoHistory.CanRedo;

    /// <summary>
    /// Restores the workspace to the state before the most recent recorded change, through the
    /// scopes so the service-owned rows stay the only writers. Only in-memory rows are touched;
    /// the registry is never written.
    /// </summary>
    public void Undo()
    {
        if (_undoHistory.Undo() is { } entry)
            ApplyEntry(entry, forward: false);
    }

    /// <summary>
    /// Re-applies the most recently undone change to the in-memory rows.
    /// </summary>
    public void Redo()
    {
        if (_undoHistory.Redo() is { } entry)
            ApplyEntry(entry, forward: true);
    }

    /// <summary>
    /// Applies an entry's deltas to the scopes they were recorded on. Undo applies the popped
    /// entry's before-side in reverse; redo applies the swapped entry's before-side — the original
    /// post-change state — in mutation order.
    /// </summary>
    /// <param name="entry">The entry popped from the history.</param>
    /// <param name="forward">False for undo, true for redo.</param>
    private void ApplyEntry(UndoEntry entry, bool forward)
    {
        foreach (var delta in entry.Scopes)
            ScopeOf(delta.Type).ApplyDelta(delta, forward);
    }

    /// <summary>
    /// Re-reads both scopes from the system, discarding every in-memory edit; the undo history is
    /// cleared, because the discarded rows can no longer be restored.
    /// </summary>
    public void Reload()
    {
        // Read both scopes before touching the lists: a throw while reading must leave the
        // rows the user is looking at untouched.
        var userRows = LoadVariables(EnvironmentVariableTarget.User);
        var machineRows = LoadVariables(EnvironmentVariableTarget.Machine);

        user.Replace(userRows);
        machine.Replace(machineRows);

        // The old rows are gone, so no recorded snapshot of them is meaningful anymore.
        _undoHistory.Clear();
    }

    /// <summary>
    /// Reads environmental variables from the specified target into a fresh, name-ordered list.
    /// </summary>
    /// <param name="target">The environment variable target (User or Machine).</param>
    private static List<EnvModel> LoadVariables(EnvironmentVariableTarget target)
    {
        var variables = new List<EnvModel>();

        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables(target))
        {
            var name = de.Key?.ToString() ?? string.Empty;
            var path = de.Value?.ToString() ?? string.Empty;
            variables.Add(new EnvModel
            {
                Name = name,
                OriginalName = name,
                Path = path,
                OriginalPath = path,
                State = EnvironmentalVariableState.Unchanged,
            });
        }

        return [.. variables.OrderBy(v => v.Name)];
    }
}
