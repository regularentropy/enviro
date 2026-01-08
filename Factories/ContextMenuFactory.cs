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
        cms.Items.Add("Create", null, (_, _) => AppendToPath(grid));
        cms.Items.Add("Edit", null, (_, _) => CreateEdit(grid));
        cms.Items.Add("Delete", null, (_, _) => DeleteCurrentRow(grid));
        cms.Items.Add(new ToolStripSeparator());
        cms.Items.Add("Copy value", null, (_, _) => CopyToClipboard(grid, m => m.Name));
        cms.Items.Add("Copy path", null, (_, _) => CopyToClipboard(grid, m => m.Path));
        cms.Items.Add("Copy original path", null, (_, _) => CopyToClipboard(grid, m => m.OrginalPath));

        if (pm.State == EnvironmentalVariableState.Deleted)
            cms.Items.Insert(1, new ToolStripMenuItem("Restore", null, (_, _) => RestoreItem(pm)));
        else if (pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Insert(1, new ToolStripMenuItem("Reset", null, (_, _) => ResetItem(pm)));

        return cms;
    }

    /// <summary>
    /// Deletes the currently selected row from the grid.
    /// </summary>
    /// <param name="g">The DataGridView containing the row to delete.</param>
    private void DeleteCurrentRow(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        var model = ControlHelper.GetCurrentModel(g);
        if (model is not null)
            _pathService.RemoveEntry(model, ControlHelper.GetTabType(g));
    }

    /// <summary>
    /// Opens a form to create a new environmental variable.
    /// </summary>
    /// <param name="g">The DataGridView to add the new variable to.</param>
    private void AppendToPath(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        _entryFormFactory.Create(ControlHelper.GetTabType(g)).ShowDialog();
    }

    /// <summary>
    /// Opens a form to edit the currently selected environmental variable.
    /// </summary>
    /// <param name="g">The DataGridView containing the variable to edit.</param>
    private void CreateEdit(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        var model = ControlHelper.GetCurrentModel(g);
        if (model is not null)
            _entryFormFactory.Create(model, ControlHelper.GetTabType(g)).ShowDialog();
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
