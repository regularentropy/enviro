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
internal class EntryFormFactory (IServiceProvider _sp) : IActionFactory<EnvModel>
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
