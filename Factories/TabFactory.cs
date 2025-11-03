using enviro.Models;

namespace enviro.Factories;

public interface ITabFactory
{
    TabPage Create(EnvironmentalVariableType tab);
}

internal sealed class TabFactory : ITabFactory
{
    private readonly IPathGridFactory gridFactory;

    public TabFactory(IPathGridFactory grid)
    {
        gridFactory = grid;
    }

    public TabPage Create(EnvironmentalVariableType currentTab)
    {
        TabPage tab = new(currentTab.ToString());

        DataGridView grid = gridFactory.Create(currentTab);
        tab.Controls.Add(grid);
        return tab;
    }
}
