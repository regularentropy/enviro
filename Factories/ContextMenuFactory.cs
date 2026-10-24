using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating context menus for grids.
/// </summary>
internal interface IContextMenuFactory
{
    /// <summary>
    /// Creates a context menu for the path grid.
    /// </summary>
    /// <param name="grid">The DataGridView to attach the context menu to.</param>
    /// <param name="pm">The environmental variable model associated with the selected row.</param>
    /// <returns>A configured ContextMenuStrip.</returns>
    ContextMenuStrip CreateForPathGrid(DataGridView grid, EnvModel pm);

    /// <summary>
    /// Creates the context menu for a click on the grid's empty area.
    /// </summary>
    /// <param name="grid">The DataGridView the click landed on.</param>
    /// <returns>A menu with Create, or null when the scope is read-only.</returns>
    ContextMenuStrip? CreateForEmptyArea(DataGridView grid);
}

/// <summary>
/// Factory for creating context menus with environmental variable operations.
/// </summary>
internal sealed class ContextMenuFactory : IContextMenuFactory
{
    private readonly IEnvService _pathService;
    private readonly IActionFactory<EnvModel> _entryFormFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMenuFactory"/> class.
    /// </summary>
    /// <param name="envService">The service for managing environmental variables.</param>
    /// <param name="factory">The factory for creating action forms.</param>
    public ContextMenuFactory(IEnvService envService, IActionFactory<EnvModel> factory)
    {
        _pathService = envService;
        _entryFormFactory = factory;
    }

    /// <summary>
    /// Creates a context menu for the path grid with operations like Create, Edit, Delete, and Copy.
    /// </summary>
    /// <param name="grid">The DataGridView to attach the context menu to.</param>
    /// <param name="pm">The environmental variable model associated with the selected row.</param>
    /// <returns>A configured ContextMenuStrip with menu items based on the model's state.</returns>
    public ContextMenuStrip CreateForPathGrid(DataGridView grid, EnvModel pm)
    {
        var cms = new ContextMenuStrip();

        // Without elevation the Machine scope is read-only, so don't offer mutating actions
        // that would only be rejected on click.
        bool canMutate = AdminChecker.CanMutate(grid);

        // Restore/Reset are ungated in-memory operations, but they still belong to the grid's scope;
        // a control without one denies them like every other mutation.
        var scope = ControlHelper.GetTabType(grid) is { } type ? _pathService.GetScope(type) : null;

        if (scope is not null)
        {
            if (pm.State == EnvironmentalVariableState.Deleted)
                cms.Items.Add("Restore", null, (_, _) => scope.Restore(pm));
            else if (pm.State == EnvironmentalVariableState.Modified)
                cms.Items.Add("Reset", null, (_, _) => scope.Reset(pm));
        }

        if (canMutate)
        {
            cms.Items.Add("Edit", null, (_, _) => RowActionHelper.Edit(grid, _entryFormFactory));
            cms.Items.Add("Delete", null, (_, _) => RowActionHelper.Delete(grid, _pathService));
            cms.Items.Add(new ToolStripSeparator());
        }

        cms.Items.Add("Copy name", null, (_, _) => CopyToClipboard(grid, m => m.Name));
        cms.Items.Add("Copy value", null, (_, _) => CopyToClipboard(grid, m => m.Path));

        // Only a modified row has an original value that differs from the current one.
        if (pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Add("Copy original path", null, (_, _) => CopyToClipboard(grid, m => m.OriginalPath));

        return cms;
    }

    /// <summary>
    /// Creates the context menu for a click on the grid's empty area.
    /// Read-only scopes get no menu at all.
    /// </summary>
    /// <param name="grid">The DataGridView the click landed on.</param>
    /// <returns>A menu with Create, or null when the scope is read-only.</returns>
    public ContextMenuStrip? CreateForEmptyArea(DataGridView grid)
    {
        if (!AdminChecker.CanMutate(grid))
            return null;

        var cms = new ContextMenuStrip();
        cms.Items.Add("Create", null, (_, _) => RowActionHelper.Create(grid, _entryFormFactory));
        return cms;
    }

    /// <summary>
    /// Copies a property of the current model to the clipboard.
    /// </summary>
    /// <param name="g">The DataGridView containing the model.</param>
    /// <param name="selector">A function to select the property to copy.</param>
    private void CopyToClipboard(DataGridView g, Func<EnvModel, string> selector)
    {
        var model = ControlHelper.GetCurrentModel(g);
        ClipboardHelper.CopyModelProperty(model, selector);
    }
}
