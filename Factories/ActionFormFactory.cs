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

internal class EntryFormFactory : IActionFactory<EnvModel>
{
    private readonly IServiceProvider _serviceProvider;

    public EntryFormFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Form Create(EnvironmentalVariableType t)
    {
        var pathService = _serviceProvider.GetRequiredService<IEnvService>();
        return new CreateForm(pathService, t);
    }

    public Form Create(EnvModel model, EnvironmentalVariableType t)
    {
        var pathService = _serviceProvider.GetRequiredService<IEnvService>();
        return new EditForm(pathService, model, t);
    }
}
