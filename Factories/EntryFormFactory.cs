using enviro.Forms;
using enviro.Models;
using enviro.Services;

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
/// <param name="_env">The store whose scopes the dialogs are bound to.</param>
/// <param name="_corruption">The corrupted-path service the PATH editor asks for segment verdicts.</param>
internal class EntryFormFactory(IEnvService _env, ICorruptedPathService _corruption) : IActionFactory<EnvModel>
{
    /// <summary>
    /// Creates a new form for creating an environmental variable.
    /// </summary>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>A CreateForm instance.</returns>
    public Form Create(EnvironmentalVariableType t) =>
        new CreateForm(_env.GetScope(t));

    /// <summary>
    /// Creates a new form for editing an existing environmental variable.
    /// </summary>
    /// <param name="model">The environmental variable model to edit.</param>
    /// <param name="t">The type of environmental variable (User or Machine).</param>
    /// <returns>An EditForm instance.</returns>
    public Form Create(EnvModel model, EnvironmentalVariableType t) =>
        new EditForm(_env.GetScope(t), model, _corruption);
}
