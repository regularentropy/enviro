using enviro.Models;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating TabPage controls.
/// </summary>
public interface ITabFactory
{
    /// <summary>
    /// Creates a TabPage for the specified environmental variable type.
    /// </summary>
    /// <param name="tab">The type of environmental variable (User or Machine).</param>
    /// <returns>A configured TabPage containing a DataGridView.</returns>
    TabPage Create(EnvironmentalVariableType tab);
}

/// <summary>
/// Factory for creating TabPage controls with DataGridView for environmental variables.
/// </summary>
/// <param name="gridFactory">The factory for creating DataGridView instances.</param>
internal sealed class TabFactory(IPathGridFactory gridFactory) : ITabFactory
{
    /// <summary>
    /// Creates a TabPage with a DataGridView for displaying environmental variables.
    /// </summary>
    /// <param name="currentTab">The type of environmental variable (User or Machine).</param>
    /// <returns>A TabPage with the tab name set to the variable type and containing a grid.</returns>
    public TabPage Create(EnvironmentalVariableType currentTab)
    {
        TabPage tab = new(currentTab.ToString());

        DataGridView grid = gridFactory.Create(currentTab);
        tab.Controls.Add(grid);
        return tab;
    }
}
