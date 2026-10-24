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
    private readonly IPathEditorFactory _pathEditorFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMenuFactory"/> class.
    /// </summary>
    /// <param name="envService">The service for managing environmental variables.</param>
    /// <param name="factory">The factory for creating action forms.</param>
    /// <param name="pathEditorFactory">The factory for the PATH segment editor.</param>
    public ContextMenuFactory(IEnvService envService, IActionFactory<EnvModel> factory, IPathEditorFactory pathEditorFactory)
    {
        _pathService = envService;
        _entryFormFactory = factory;
        _pathEditorFactory = pathEditorFactory;
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

        if (canMutate)
            cms.Items.Add("Create", null, (_, _) => RowActionHelper.Create(grid, _entryFormFactory));

        if (pm.State == EnvironmentalVariableState.Deleted)
            cms.Items.Add("Restore", null, (_, _) => RestoreItem(pm));
        else if (pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Add("Reset", null, (_, _) => ResetItem(pm));

        if (canMutate)
        {
            cms.Items.Add("Edit", null, (_, _) => RowActionHelper.Edit(grid, _entryFormFactory));

            // The segment editor is an edit too, so it sits next to Edit. Deleted rows cannot be
            // edited until they are restored.
            if (pm.State != EnvironmentalVariableState.Deleted && ValidationHelper.IsPathList(pm.Name, pm.Path))
                cms.Items.Add("Path entries…", null, (_, _) => EditPathEntries(grid, pm));

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

    /// <summary>
    /// Opens the shared edit window with the segment list already showing.
    /// </summary>
    /// <param name="grid">The grid that determines the scope.</param>
    /// <param name="pm">The model to edit.</param>
    private void EditPathEntries(DataGridView grid, EnvModel pm)
    {
        if (!AdminChecker.ValidateTab(grid))
            return;

        _pathEditorFactory.ShowInEditor(grid.FindForm(), pm, ControlHelper.GetTabType(grid));
    }

    /// <summary>
    /// Restores a deleted environmental variable.
    /// </summary>
    /// <param name="pm">The model to restore.</param>
    private void RestoreItem(EnvModel pm) => _pathService.RestoreItem(pm);

    /// <summary>
    /// Resets a modified environmental variable to its original state.
    /// </summary>
    /// <param name="pm">The model to reset.</param>
    private void ResetItem(EnvModel pm) => _pathService.ResetItem(pm);
}
