using enviro.Forms;
using enviro.Models;
using enviro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace enviro.Factories;

internal interface IActionFactory
{
    Form Create(EnvironmentalVariableType t);
}

internal interface IActionFactory<TModel> : IActionFactory
    where TModel : class
{
    Form Create(TModel model, EnvironmentalVariableType t);
}

internal class EntryFormFactory (IServiceProvider _sp) : IActionFactory<EnvModel>
{
    public Form Create(EnvironmentalVariableType t)
    {
        var envService = _sp.GetRequiredService<IEnvService>();
        return new CreateForm(envService, t);
    }

    public Form Create(EnvModel model, EnvironmentalVariableType t)
    {
        var envService = _sp.GetRequiredService<IEnvService>();
        return new EditForm(envService, model, t);
    }
}
