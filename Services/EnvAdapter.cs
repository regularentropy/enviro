using enviro.Models;
using enviro.Services;
using enviro.Static;

/// <summary>
/// Adapter interface for applying environmental variable changes to the system.
/// </summary>
internal interface IEnvAdapter
{
    /// <summary>
    /// Applies all pending environmental variable changes to the system.
    /// </summary>
    /// <returns>What the run managed to write, and which row stopped it.</returns>
    Task<ApplyResult> ApplyAsync();
}

/// <summary>
/// Outcome of one apply run. Rows are written and cleaned one at a time, so a failure
/// leaves the rows written before it clean and the failing row (and everything after it) dirty.
/// </summary>
/// <param name="UserApplied">Rows written in the user scope.</param>
/// <param name="MachineApplied">Rows written in the machine scope.</param>
/// <param name="FailedScope">Scope of the row that threw, or null when nothing failed.</param>
/// <param name="FailedModel">The row that threw, or null when nothing failed.</param>
/// <param name="Error">Message of the exception that stopped the run, or null when nothing failed.</param>
internal sealed record ApplyResult(
    int UserApplied,
    int MachineApplied,
    EnvironmentalVariableType? FailedScope,
    EnvModel? FailedModel,
    string? Error)
{
    /// <summary>
    /// Whether every pending change was written.
    /// </summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// Adapter for applying environmental variable changes to the Windows registry.
/// Handles creating, modifying, and deleting environmental variables.
/// </summary>
/// <param name="_es">The service managing environmental variables.</param>
/// <param name="_log">Where a failed write is recorded.</param>
internal sealed class EnvAdapter(IEnvService _es, IAppLog _log) : IEnvAdapter
{
    /// <summary>
    /// Applies all pending changes to the system environment variables.
    /// User variables are always applied; machine variables are applied only when the machine
    /// scope accepts mutations, i.e. when the process is elevated.
    /// </summary>
    /// <returns>The rows written per scope, and the row that stopped the run if one threw.</returns>
    public Task<ApplyResult> ApplyAsync()
    {
        var (userApplied, userFailed, userError) = ApplyAndClean(_es.User);

        if (userFailed is not null)
            return Task.FromResult(new ApplyResult(userApplied, 0, EnvironmentalVariableType.User, userFailed, userError));

        var machineApplied = 0;

        if (AdminChecker.CanMutate(_es.Machine.Type))
        {
            var machine = ApplyAndClean(_es.Machine);
            machineApplied = machine.Applied;

            if (machine.Failed is not null)
            {
                return Task.FromResult(
                    new ApplyResult(userApplied, machineApplied, EnvironmentalVariableType.Machine, machine.Failed, machine.Error));
            }
        }

        return Task.FromResult(new ApplyResult(userApplied, machineApplied, null, null, null));
    }

    /// <summary>
    /// Writes every pending row of one scope, cleaning each row as soon as it is written.
    /// Stops at the first row that throws.
    /// </summary>
    /// <param name="scope">The scope whose pending rows are written.</param>
    /// <returns>The number of rows written, plus the row and error message that stopped the run (if any).</returns>
    private (int Applied, EnvModel? Failed, string? Error) ApplyAndClean(IEnvScope scope)
    {
        var target = EnvTypeConverter.ToTarget(scope.Type);
        var applied = 0;

        // Deleted first, so a variable that is going away is removed before new names are written.
        EnvironmentalVariableState[] order =
        [
            EnvironmentalVariableState.Deleted,
            EnvironmentalVariableState.Modified,
            EnvironmentalVariableState.Added,
        ];

        foreach (var state in order)
        {
            // Snapshot: a written deleted row leaves the list while this loop runs.
            foreach (var model in scope.Rows.OfType<EnvModel>().Where(m => m.State == state).ToList())
            {
                var oldKeyRemoved = false;

                try
                {
                    // A renamed row was never written under its new name, so the key to remove is
                    // the original one. Windows keys are case-insensitive, hence the ordinal compare.
                    if (!string.Equals(model.OriginalName, model.Name, StringComparison.Ordinal))
                    {
                        Environment.SetEnvironmentVariable(model.OriginalName, null, target);
                        oldKeyRemoved = true;
                    }

                    Environment.SetEnvironmentVariable(
                        model.Name,
                        state == EnvironmentalVariableState.Deleted ? null : model.Path,
                        target);
                }
                catch (Exception ex)
                {
                    // The new name was never written, so the deleted old key held the only copy.
                    if (oldKeyRemoved)
                        RestoreOldKey(model, target);

                    _log.Error($"Could not write {scope.Type} variable \"{model.Name}\".", ex);
                    return (applied, model, ex.Message);
                }

                scope.MarkApplied(model, state);
                applied++;
            }
        }

        return (applied, null, null);
    }

    /// <summary>
    /// Writes a renamed row's original key back with its original value, after the write under the
    /// new name failed and the old key had already been removed.
    /// </summary>
    /// <remarks>
    /// Best effort: the run is already failing and the row stays dirty, so a failed restore is only
    /// logged and never replaces the original failure.
    /// </remarks>
    /// <param name="model">The renamed row whose old key is gone.</param>
    /// <param name="target">The hive the row belongs to.</param>
    private void RestoreOldKey(EnvModel model, EnvironmentVariableTarget target)
    {
        try
        {
            Environment.SetEnvironmentVariable(model.OriginalName, model.OriginalPath, target);
        }
        catch (Exception ex)
        {
            _log.Error($"Could not restore the original value of variable \"{model.OriginalName}\".", ex);
        }
    }
}
