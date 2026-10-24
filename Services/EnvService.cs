using enviro.Models;
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
    /// Re-reads both scopes from the system, discarding every in-memory edit. The refreshed
    /// lists are in name order and every model starts as <c>Unchanged</c> with no corrupted overlay.
    /// A read that throws leaves the previous rows in place.
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
/// One variable scope: the rows that belong to <see cref="Type"/> plus every operation bound to
/// that scope. The rows are handed out read-only, so nothing outside the service can add, remove
/// or reorder them.
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
    /// Re-orders the rows by one model property. The list is cleared and refilled, so callers that
    /// care about the view capture and restore the grid position around this call.
    /// </summary>
    /// <param name="propertyName">The model property to order by.</param>
    /// <param name="direction">Ascending or descending.</param>
    void Sort(string propertyName, ListSortDirection direction);
}

/// <summary>
/// The service-owned rows of one scope and every operation bound to it.
/// </summary>
/// <param name="type">The scope these rows belong to.</param>
internal sealed class EnvScope(EnvironmentalVariableType type) : IEnvScope
{
    private readonly BindingList<EnvModel> rows = [];

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
        rows.RaiseListChangedEvents = false;
        try
        {
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
    /// Adds a new environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to add.</param>
    public void Add(EnvModel pm)
    {
        pm.State = EnvironmentalVariableState.Added;
        pm.OriginalName = pm.Name;
        pm.OriginalPath = pm.Path;

        rows.Add(pm);
    }

    /// <summary>
    /// Updates the path value of an environmental variable and marks it as modified.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="newValue">The new value.</param>
    public void Update(EnvModel model, string newValue)
    {
        if (model.State == EnvironmentalVariableState.Deleted)
            return;

        model.Path = newValue;

        if (model.State != EnvironmentalVariableState.Added)
            model.State = EnvironmentalVariableState.Modified;
    }

    /// <summary>
    /// Resolves the whole create path: adds a new row, restores a pending deletion or overwrites
    /// a live row.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value to store under that name.</param>
    /// <param name="overwrite">False refuses to touch a row that is not deleted.</param>
    /// <returns>Which case happened; a refused upsert mutates nothing.</returns>
    public UpsertOutcome Upsert(string name, string value, bool overwrite)
    {
        var existing = GetByName(name);

        if (existing is null)
        {
            Add(new EnvModel { Name = name, Path = value });
            return UpsertOutcome.Added;
        }

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
    /// Resolves the whole edit path: renames and revalues one row, or rejects the edit without
    /// touching any model.
    /// </summary>
    /// <param name="model">The row to edit.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>The row that now holds the edit, or null and the reason it was rejected.</returns>
    public (EnvModel? Model, string? Rejection) ApplyEdit(EnvModel model, string newName, string newValue)
    {
        // Reference check: a model another scope (or a stale snapshot) holds is not editable here.
        if (!rows.Contains(model))
            return (null, "The variable is no longer in this list.");

        if (model.State == EnvironmentalVariableState.Deleted)
            return (null, "Restore the variable before editing it.");

        // Rename rejects a blank name, an unknown row and a collision with another variable.
        var target = Rename(model.Name, newName);

        if (target is null)
            return (null, $"Cannot rename to '{newName}': the name is already in use.");

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
    public void Delete(EnvModel pm)
    {
        if (pm.State == EnvironmentalVariableState.Added)
        {
            rows.Remove(pm);
            return;
        }

        pm.State = EnvironmentalVariableState.Deleted;
    }

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
    public void Reset(EnvModel pm)
    {
        pm.Name = pm.OriginalName;
        pm.Path = pm.OriginalPath;
        pm.State = EnvironmentalVariableState.Unchanged;
    }

    /// <summary>
    /// Restores a deleted environmental variable by marking it as unchanged under its original name.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    public void Restore(EnvModel pm)
    {
        pm.Name = pm.OriginalName;
        pm.State = EnvironmentalVariableState.Unchanged;
    }

    /// <summary>
    /// Sets a written row back to <c>Unchanged</c> and syncs its baseline; a deleted row leaves the list.
    /// </summary>
    /// <param name="model">The row that was just written.</param>
    /// <param name="appliedState">The state the row had when it was written.</param>
    public void MarkApplied(EnvModel model, EnvironmentalVariableState appliedState)
    {
        if (appliedState == EnvironmentalVariableState.Deleted)
        {
            rows.Remove(model);
            return;
        }

        model.State = EnvironmentalVariableState.Unchanged;
        model.OriginalName = model.Name;
        model.OriginalPath = model.Path;
    }

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

        rows.Clear();

        foreach (var row in sortedRows)
        {
            rows.Add(row);
        }
    }
}

/// <summary>
/// Service for managing environmental variables, including adding, removing, updating, and tracking changes.
/// </summary>
internal sealed class EnvService : IEnvService
{
    private readonly EnvScope user = new(EnvironmentalVariableType.User);
    private readonly EnvScope machine = new(EnvironmentalVariableType.Machine);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvService"/> class.
    /// Loads all environmental variables from the system.
    /// </summary>
    public EnvService() => Reload();

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
    public IEnvScope GetScope(EnvironmentalVariableType type) =>
        type == EnvironmentalVariableType.User ? user : machine;

    /// <summary>
    /// Determines whether any environmental variables have been modified.
    /// </summary>
    /// <returns>True if any user or machine variables are not in unchanged state.</returns>
    public bool HasChanges() => user.HasChanges() || machine.HasChanges();

    /// <summary>
    /// Re-reads both scopes from the system, discarding every in-memory edit.
    /// </summary>
    public void Reload()
    {
        // Read both scopes before touching the lists: a throw while reading must leave the
        // rows the user is looking at untouched.
        var userRows = LoadVariables(EnvironmentVariableTarget.User);
        var machineRows = LoadVariables(EnvironmentVariableTarget.Machine);

        user.Replace(userRows);
        machine.Replace(machineRows);
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
