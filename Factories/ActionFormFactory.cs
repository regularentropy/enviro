using enviro.Forms;
using enviro.Models;
using enviro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating action forms.
/// </summary>
internal interface IActionFactory
{
    /// <summary>
    /// Creates a form for the specified environmental variable type.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>A new form instance.</returns>
    Form Create(EnvironmentalVariableType t);
}

/// <summary>
/// Generic factory interface for creating action forms with model support.
/// </summary>
/// <typeparam name="TModel">The type of model used by the forms.</typeparam>
internal interface IActionFactory<TModel> : IActionFactory
    where TModel : class
{
    /// <summary>
    /// Creates a form for editing the specified model.
    /// </summary>
    /// <param name="model">The model to edit.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>A new form instance configured for editing.</returns>
    Form Create(TModel model, EnvironmentalVariableType t);
}

/// <summary>
/// Factory for creating entry forms (Create and Edit forms) for environmental variables.
/// </summary>
/// <param name="_sp">The service provider for dependency injection.</param>
internal class EntryFormFactory(IServiceProvider _sp) : IActionFactory<EnvModel>
{
    /// <summary>
    /// Creates a new form for creating an environmental variable.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>A CreateForm instance.</returns>
    public Form Create(EnvironmentalVariableType t)
    {
        var envService = _sp.GetRequiredService<IEnvService>();
        return new CreateForm(envService, t);
    }

    /// <summary>
    /// Creates a new form for editing an existing environmental variable.
    /// </summary>
    /// <param name="model">The environmental variable model to edit.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>An EditForm instance.</returns>
    public Form Create(EnvModel model, EnvironmentalVariableType t)
    {
        var envService = _sp.GetRequiredService<IEnvService>();
        return new EditForm(envService, model, t);
    }
}

/// <summary>
/// Factory interface for opening the editor with the PATH segment view active.
/// </summary>
internal interface IPathEditorFactory
{
    /// <summary>
    /// Opens the edit window for the model, focused on the segment list.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or null to use the active window.</param>
    /// <param name="model">The model to edit.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    void ShowInEditor(IWin32Window? owner, EnvModel model, EnvironmentalVariableType t);
}

/// <summary>
/// Opens the shared edit window with the PATH segment view selected.
/// </summary>
/// <param name="_sp">The service provider for dependency injection.</param>
internal sealed class PathEditorFactory(IServiceProvider _sp) : IPathEditorFactory
{
    /// <summary>
    /// Opens the edit window for the model, focused on the segment list.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or null to use the active window.</param>
    /// <param name="model">The model to edit.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    public void ShowInEditor(IWin32Window? owner, EnvModel model, EnvironmentalVariableType t)
    {
        var envService = _sp.GetRequiredService<IEnvService>();

        using var form = new EditForm(envService, model, t, startInEntries: true);

        if (owner is null)
            form.ShowDialog();
        else
            form.ShowDialog(owner);
    }
}
