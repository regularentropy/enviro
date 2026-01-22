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
    /// <returns>A task that returns true if successful, false otherwise.</returns>
    Task<bool> ApplyAsync();
}

/// <summary>
/// Adapter for applying environmental variable changes to the Windows registry.
/// Handles creating, modifying, and deleting environmental variables.
/// </summary>
/// <param name="_es">The service managing environmental variables.</param>
internal sealed class EnvAdapter(IEnvService _es) : IPathAdapter
{
    /// <summary>
    /// Applies all pending changes to the system environment variables.
    /// User variables are always applied; machine variables are only applied if running as administrator.
    /// </summary>
    /// <returns>A task that returns true if all changes were applied successfully, false if an error occurred.</returns>
    public Task<bool> ApplyAsync()
    {
        try
        {
            bool isAdmin = AdminChecker.IsAdmin();

            var userVars = _es.GetUserVariables();
            ApplyAndClean(EnvironmentalVariableType.User, userVars);

            if (isAdmin)
            {
                var machineVars = _es.GetMachineVariables();
                ApplyAndClean(EnvironmentalVariableType.Machine, machineVars);
            }
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Applies changes and cleans up the model collection for the specified variable type.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <param name="models">The collection of models to process.</param>
    private void ApplyAndClean(EnvironmentalVariableType t, BindingList<EnvModel> models)
    {
        Apply(t, models);
        Clean(models);
    }

    /// <summary>
    /// Applies state changes (Added, Modified, Deleted) to the system environment.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <param name="models">The collection of models to process.</param>
    private void Apply(EnvironmentalVariableType t, BindingList<EnvModel> models)
    {
        var envVarType = EnvTypeConverter.ToTarget(t);

        ApplyStateChanges(models, EnvironmentalVariableState.Deleted, envVarType, (v, target) =>
            Environment.SetEnvironmentVariable(v.Name, null, target));

        ApplyStateChanges(models, EnvironmentalVariableState.Modified, envVarType, (v, target) =>
            Environment.SetEnvironmentVariable(v.Name, v.Path, target));

        ApplyStateChanges(models, EnvironmentalVariableState.Added, envVarType, (v, target) =>
            Environment.SetEnvironmentVariable(v.Name, v.Path, target));
    }

    /// <summary>
    /// Applies a specific state change to all models with the specified state.
    /// </summary>
    /// <param name="models">The collection of models to process.</param>
    /// <param name="state">The state to filter by.</param>
    /// <param name="target">The environment variable target (User or Machine).</param>
    /// <param name="action">The action to perform on each matching model.</param>
    private void ApplyStateChanges(BindingList<EnvModel> models, EnvironmentalVariableState state,
        EnvironmentVariableTarget target, Action<EnvModel, EnvironmentVariableTarget> action)
    {
        foreach (var v in models.Where(s => s.State == state))
            action(v, target);
    }

    /// <summary>
    /// Removes deleted items from the collection and resets all remaining items to unchanged state.
    /// Updates original paths to match current paths.
    /// </summary>
    /// <param name="models">The collection of models to clean.</param>
    private void Clean(BindingList<EnvModel> models)
    {
        var toRemove = models.Where(v => v.State == EnvironmentalVariableState.Deleted).ToList();
        foreach (var item in toRemove)
            models.Remove(item);

        foreach (var v in models)
        {
            v.State = EnvironmentalVariableState.Unchanged;
            v.OriginalPath = v.Path;
        }
    }
}
