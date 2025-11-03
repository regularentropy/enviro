using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;

internal interface IPathAdapter
{
    Task<bool> ApplyAsync();
}

internal sealed class EnvAdapter : IPathAdapter
{
    private readonly IEnvService _ps;
    public EnvAdapter(IEnvService ps) => _ps = ps;

    public Task<bool> ApplyAsync()
    {
        try
        {
            bool isAdmin = AdminChecker.IsAdmin();

            var userVars = _ps.GetUserVariables();
            Apply(EnvironmentalVariableType.User, userVars);
            Clean(userVars);

            if (isAdmin)
            {
                var machineVars = _ps.GetMachineVariables();
                Apply(EnvironmentalVariableType.Machine, machineVars);
                Clean(machineVars);
            }
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private EnvironmentVariableTarget Convert(EnvironmentalVariableType t)
    {
        return t switch
        {
            EnvironmentalVariableType.User => EnvironmentVariableTarget.User,
            EnvironmentalVariableType.Machine => EnvironmentVariableTarget.Machine
        };
    }

    private void Apply(EnvironmentalVariableType t, BindingList<EnvModel> models)
    {
        var envVarType = Convert(t);
        foreach (var v in models.Where(s => s.State == EnvironmentalVariableState.Deleted))
            Environment.SetEnvironmentVariable(v.Name, null, envVarType);

        foreach (var v in models.Where(s => s.State == EnvironmentalVariableState.Modified))
            Environment.SetEnvironmentVariable(v.Name, v.Path, envVarType);

        foreach (var v in models.Where(s => s.State == EnvironmentalVariableState.Added))
            Environment.SetEnvironmentVariable(v.Name, v.Path, envVarType);
    }

    private void Clean(BindingList<EnvModel> models)
    {
        foreach (var item in models.Where(v => v.State == EnvironmentalVariableState.Deleted).ToList())
            models.Remove(item);

        foreach (var v in models) { v.State = EnvironmentalVariableState.Unchanged; v.OrginalPath = v.Path; }
    }
}
