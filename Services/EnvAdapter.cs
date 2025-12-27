using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;

internal interface IPathAdapter
{
    Task<bool> ApplyAsync();
}

internal sealed class EnvAdapter(IEnvService _es) : IPathAdapter
{
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

    private void ApplyAndClean(EnvironmentalVariableType t, BindingList<EnvModel> models)
    {
        Apply(t, models);
        Clean(models);
    }

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

    private void ApplyStateChanges(BindingList<EnvModel> models, EnvironmentalVariableState state,
        EnvironmentVariableTarget target, Action<EnvModel, EnvironmentVariableTarget> action)
    {
        foreach (var v in models.Where(s => s.State == state))
            action(v, target);
    }

    private void Clean(BindingList<EnvModel> models)
    {
        var toRemove = models.Where(v => v.State == EnvironmentalVariableState.Deleted).ToList();
        foreach (var item in toRemove)
            models.Remove(item);

        foreach (var v in models)
        {
            v.State = EnvironmentalVariableState.Unchanged;
            v.OrginalPath = v.Path;
        }
    }
}
