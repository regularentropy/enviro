using enviro.Models;
using System.ComponentModel;

namespace enviro.Services;

/// <summary>
/// Service interface for managing environmental variables.
/// </summary>
internal interface IEnvService
{
    /// <summary>
    /// Gets the collection of user-level environmental variables.
    /// </summary>
    /// <returns>A binding list of user environmental variables.</returns>
    BindingList<EnvModel> GetUserVariables();
    
    /// <summary>
    /// Gets the collection of machine-level environmental variables.
    /// </summary>
    /// <returns>A binding list of machine environmental variables.</returns>
    BindingList<EnvModel> GetMachineVariables();
    
    /// <summary>
    /// Gets the bundle containing both user and machine environmental variables.
    /// </summary>
    /// <returns>An EnvModelBundle containing all variables.</returns>
    EnvModelBundle GetPathModelBundle();

    /// <summary>
    /// Determines whether any environmental variables have been modified.
    /// </summary>
    /// <returns>True if there are changes, false otherwise.</returns>
    bool HasChanges();

    /// <summary>
    /// Adds a new environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to add.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    void AddEntry(EnvModel pm, EnvironmentalVariableType t);
    
    /// <summary>
    /// Removes an environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to remove.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    void RemoveEntry(EnvModel pm, EnvironmentalVariableType t);
    
    /// <summary>
    /// Checks if a variable with the same name exists.
    /// </summary>
    /// <param name="pathModel">The model to check.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>True if the variable exists, false otherwise.</returns>
    bool Contains(EnvModel pathModel, EnvironmentalVariableType t);

    /// <summary>
    /// Restores a deleted environmental variable.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    void RestoreItem(EnvModel pm);
    
    /// <summary>
    /// Resets a modified environmental variable to its original value.
    /// </summary>
    /// <param name="pm">The model to reset.</param>
    void ResetItem(EnvModel pm);

    /// <summary>
    /// Finds an environmental variable by name.
    /// </summary>
    /// <param name="name">The name to search for.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>The model if found, null otherwise.</returns>
    EnvModel? GetModelByName(string name, EnvironmentalVariableType t);

    /// <summary>
    /// Updates the path value of an environmental variable.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="newPath">The new path value.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    void UpdatePath(EnvModel model, string newPath, EnvironmentalVariableType t);
    
    /// <summary>
    /// Renames an environmental variable.
    /// </summary>
    /// <param name="oldName">The current name.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    void Rename(string oldName, string newName, EnvironmentalVariableType t);
}

/// <summary>
/// Service for managing environmental variables, including adding, removing, updating, and tracking changes.
/// </summary>
internal sealed class EnvService : IEnvService
{
    private readonly EnvModelBundle variables = new();

    /// <summary>
    /// Determines whether any environmental variables have been modified.
    /// </summary>
    /// <returns>True if any user or machine variables are not in unchanged state.</returns>
    public bool HasChanges() =>
        variables.User.Any(v => v.State != EnvironmentalVariableState.Unchanged) ||
        variables.Machine.Any(v => v.State != EnvironmentalVariableState.Unchanged);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvService"/> class.
    /// Loads all environmental variables from the system.
    /// </summary>
    public EnvService()
    {
        FillPath();
    }

    /// <summary>
    /// Gets the collection of variables for the specified type.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>The binding list for the specified type.</returns>
    private BindingList<EnvModel> GetVariablesByType(EnvironmentalVariableType t) =>
        t == EnvironmentalVariableType.User ? variables.User : variables.Machine;

    /// <summary>
    /// Loads all environmental variables from the system.
    /// </summary>
    private void FillPath()
    {
        LoadVariables(EnvironmentVariableTarget.User, variables.User);
        LoadVariables(EnvironmentVariableTarget.Machine, variables.Machine);
    }

    /// <summary>
    /// Loads environmental variables from the specified target into the provided list.
    /// </summary>
    /// <param name="target">The environment variable target (User or Machine).</param>
    /// <param name="list">The list to populate with variables.</param>
    private static void LoadVariables(EnvironmentVariableTarget target, BindingList<EnvModel> list)
    {
        var variables = new List<EnvModel>();
        
        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables(target))
        {
            var path = de.Value?.ToString() ?? string.Empty;
            variables.Add(new EnvModel
            {
                Name = de.Key?.ToString(),
                Path = path,
                OrginalPath = path,
                State = EnvironmentalVariableState.Unchanged,
            });
        }
        
        foreach (var variable in variables.OrderBy(v => v.Name))
        {
            list.Add(variable);
        }
    }

    /// <summary>
    /// Adds a new environmental variable entry.
    /// </summary>
    /// <param name="pm">The model to add.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    public void AddEntry(EnvModel pm, EnvironmentalVariableType t)
    {

        pm.State = EnvironmentalVariableState.Added;
        pm.OrginalPath = pm.Path;

        GetVariablesByType(t).Add(pm);
    }

    /// <summary>
    /// Updates the path value of an environmental variable and marks it as modified.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="newPath">The new path value.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    public void UpdatePath(EnvModel model, string newPath, EnvironmentalVariableType t)
    {
        model.Path = newPath;

        if (model.State != EnvironmentalVariableState.Added)
            model.State = EnvironmentalVariableState.Modified;
    }

    /// <summary>
    /// Renames an environmental variable by marking the old one as deleted and creating a new one.
    /// </summary>
    /// <param name="oldName">The current name.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
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

    /// <summary>
    /// Removes an environmental variable entry or marks it as deleted.
    /// </summary>
    /// <param name="pm">The model to remove.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    public void RemoveEntry(EnvModel pm, EnvironmentalVariableType t)
    {
        if (pm.State == EnvironmentalVariableState.Added)
        {
            GetVariablesByType(t).Remove(pm);
            return;
        }

        pm.State = EnvironmentalVariableState.Deleted;
    }

    /// <summary>
    /// Checks if a variable with the same name exists in the specified collection.
    /// </summary>
    /// <param name="pathModel">The model to check.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>True if a variable with the same name exists.</returns>
    public bool Contains(EnvModel pathModel, EnvironmentalVariableType t)
    {
        var array = GetVariablesByType(t);
        return array.Any(c => c.Name == pathModel.Name);
    }

    /// <summary>
    /// Resets a modified environmental variable to its original value and state.
    /// </summary>
    /// <param name="pm">The model to reset.</param>
    public void ResetItem(EnvModel pm)
    {
        pm.State = EnvironmentalVariableState.Unchanged;
        pm.Path = pm.OrginalPath;
    }

    /// <summary>
    /// Restores a deleted environmental variable by marking it as unchanged.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    public void RestoreItem(EnvModel pm) => pm.State = EnvironmentalVariableState.Unchanged;

    /// <summary>
    /// Finds an environmental variable by name in the specified collection.
    /// </summary>
    /// <param name="name">The name to search for.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>The model if found, null otherwise.</returns>
    public EnvModel? GetModelByName(string name, EnvironmentalVariableType t)
    {
        var array = GetVariablesByType(t);
        return array.FirstOrDefault(s => s.Name == name);
    }

    /// <summary>
    /// Gets the collection of user-level environmental variables.
    /// </summary>
    /// <returns>A binding list of user environmental variables.</returns>
    public BindingList<EnvModel> GetUserVariables() => variables.User;

    /// <summary>
    /// Gets the collection of machine-level environmental variables.
    /// </summary>
    /// <returns>A binding list of machine environmental variables.</returns>
    public BindingList<EnvModel> GetMachineVariables() => variables.Machine;

    /// <summary>
    /// Gets the bundle containing both user and machine environmental variables.
    /// </summary>
    /// <returns>An EnvModelBundle containing all variables.</returns>
    public EnvModelBundle GetPathModelBundle() => variables;
}