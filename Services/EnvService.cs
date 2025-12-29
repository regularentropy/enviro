using enviro.Models;
using System.ComponentModel;

namespace enviro.Services;

internal interface IEnvService
{
    BindingList<EnvModel> GetUserVariables();
    BindingList<EnvModel> GetMachineVariables();
    EnvModelBundle GetPathModelBundle();

    bool HasChanges();

    void AddEntry(EnvModel pm, EnvironmentalVariableType t);
    void RemoveEntry(EnvModel pm, EnvironmentalVariableType t);
    bool Contains(EnvModel pathModel, EnvironmentalVariableType t);

    void RestoreItem(EnvModel pm);
    void ResetItem(EnvModel pm);

    EnvModel? GetModelByName(string name, EnvironmentalVariableType t);

    void UpdatePath(EnvModel model, string newPath, EnvironmentalVariableType t);
    void Rename(string oldName, string newName, EnvironmentalVariableType t);
}

internal sealed class EnvService : IEnvService
{
    private readonly EnvModelBundle variables = new();

    public bool HasChanges() =>
        variables.User.Any(v => v.State != EnvironmentalVariableState.Unchanged) ||
        variables.Machine.Any(v => v.State != EnvironmentalVariableState.Unchanged);

    public EnvService()
    {
        FillPath();
    }

    private BindingList<EnvModel> GetVariablesByType(EnvironmentalVariableType t) =>
        t == EnvironmentalVariableType.User ? variables.User : variables.Machine;

    private void FillPath()
    {
        LoadVariables(EnvironmentVariableTarget.User, variables.User);
        LoadVariables(EnvironmentVariableTarget.Machine, variables.Machine);
    }

    private static void LoadVariables(EnvironmentVariableTarget target, BindingList<EnvModel> list)
    {
        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables(target))
        {
            var path = de.Value?.ToString() ?? string.Empty;
            list.Add(new EnvModel
            {
                Name = de.Key?.ToString(),
                Path = path,
                OrginalPath = path,
                State = EnvironmentalVariableState.Unchanged,
            });
        }
    }

    public void AddEntry(EnvModel pm, EnvironmentalVariableType t)
    {

        pm.State = EnvironmentalVariableState.Added;
        pm.OrginalPath = pm.Path;

        GetVariablesByType(t).Add(pm);
    }

    public void UpdatePath(EnvModel model, string newPath, EnvironmentalVariableType t)
    {
        model.Path = newPath;

        if (model.State != EnvironmentalVariableState.Added)
            model.State = EnvironmentalVariableState.Modified;
    }

    public void Rename(string oldName, string newName, EnvironmentalVariableType t)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return;

        var foundModel = GetModelByName(oldName ?? string.Empty, t);

        if (foundModel == null)
            return;

        if (GetModelByName(newName, t) != null && GetModelByName(newName, t) != foundModel)
            return;

        if (foundModel.State == EnvironmentalVariableState.Added)
        {
            foundModel.Name = newName;
            return;
        }

        foundModel.State = EnvironmentalVariableState.Deleted;

        var newModel = new EnvModel
        {
            Name = newName,
            Path = foundModel.Path,
            OrginalPath = string.Empty,
            State = EnvironmentalVariableState.Added
        };

        GetVariablesByType(t).Add(newModel);
    }

    public void RemoveEntry(EnvModel pm, EnvironmentalVariableType t)
    {
        if (pm.State == EnvironmentalVariableState.Added)
        {
            GetVariablesByType(t).Remove(pm);
            return;
        }

        pm.State = EnvironmentalVariableState.Deleted;
    }

    public bool Contains(EnvModel pathModel, EnvironmentalVariableType t)
    {
        var array = GetVariablesByType(t);
        return array.Any(c => c.Name == pathModel.Name);
    }

    public void ResetItem(EnvModel pm)
    {
        pm.State = EnvironmentalVariableState.Unchanged;
        pm.Path = pm.OrginalPath;
    }

    public void RestoreItem(EnvModel pm) => pm.State = EnvironmentalVariableState.Unchanged;

    public EnvModel? GetModelByName(string name, EnvironmentalVariableType t)
    {
        var array = GetVariablesByType(t);
        return array.FirstOrDefault(s => s.Name == name);
    }

    public BindingList<EnvModel> GetUserVariables() => variables.User;

    public BindingList<EnvModel> GetMachineVariables() => variables.Machine;

    public EnvModelBundle GetPathModelBundle() => variables;
}