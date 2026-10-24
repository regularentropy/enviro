using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;

/// <summary>
/// Adapter interface for applying environmental variable changes to the system.
/// </summary>
internal interface IPathAdapter
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
internal sealed class EnvAdapter(IEnvService _es, IAppLog _log) : IPathAdapter
{
    /// <summary>
    /// Applies all pending changes to the system environment variables.
    /// User variables are always applied; machine variables are only applied if running as administrator.
    /// </summary>
    /// <returns>The rows written per scope, and the row that stopped the run if one threw.</returns>
    public Task<ApplyResult> ApplyAsync()
    {
        var (userApplied, userFailed, userError) = ApplyAndClean(EnvironmentalVariableType.User, _es.GetUserVariables());

        if (userFailed is not null)
            return Task.FromResult(new ApplyResult(userApplied, 0, EnvironmentalVariableType.User, userFailed, userError));

        var machineApplied = 0;

        if (AdminChecker.IsAdmin())
        {
            var machine = ApplyAndClean(EnvironmentalVariableType.Machine, _es.GetMachineVariables());
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
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <param name="models">The collection of models to process.</param>
    /// <returns>The number of rows written, plus the row and error message that stopped the run (if any).</returns>
    private (int Applied, EnvModel? Failed, string? Error) ApplyAndClean(
        EnvironmentalVariableType t, BindingList<EnvModel> models)
    {
        var target = EnvTypeConverter.ToTarget(t);
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
            foreach (var model in models.Where(m => m.State == state).ToList())
            {
                try
                {
                    // A renamed row was never written under its new name, so the key to remove is
                    // the original one. Windows keys are case-insensitive, hence the ordinal compare.
                    if (!string.Equals(model.OriginalName, model.Name, StringComparison.Ordinal))
                        Environment.SetEnvironmentVariable(model.OriginalName, null, target);

                    Environment.SetEnvironmentVariable(
                        model.Name,
                        state == EnvironmentalVariableState.Deleted ? null : model.Path,
                        target);
                }
                catch (Exception ex)
                {
                    _log.Error($"Could not write {t} variable \"{model.Name}\".", ex);
                    return (applied, model, ex.Message);
                }

                MarkApplied(models, model, state);
                applied++;
            }
        }

        return (applied, null, null);
    }

    /// <summary>
    /// Sets a written row back to <c>Unchanged</c> and syncs its baseline; a deleted row leaves the list.
    /// </summary>
    /// <param name="models">The collection the row belongs to.</param>
    /// <param name="model">The row that was just written.</param>
    /// <param name="appliedState">The state the row had when it was written.</param>
    private static void MarkApplied(BindingList<EnvModel> models, EnvModel model, EnvironmentalVariableState appliedState)
    {
        if (appliedState == EnvironmentalVariableState.Deleted)
        {
            models.Remove(model);
            return;
        }

        model.State = EnvironmentalVariableState.Unchanged;
        model.OriginalName = model.Name;
        model.OriginalPath = model.Path;
    }
}
