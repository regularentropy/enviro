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
    /// Creates the context menu for a row of a grid.
    /// </summary>
    /// <param name="grid">The DataGridView to attach the context menu to.</param>
    /// <param name="pm">The environmental variable model associated with the selected row.</param>
    /// <returns>A configured ContextMenuStrip.</returns>
    ContextMenuStrip CreateForRow(DataGridView grid, EnvModel pm);

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
    private readonly IEnvService _env;
    private readonly IActionFactory<EnvModel> _entryFormFactory;
    private readonly IDriftService _drift;
    private readonly IUndoHistory _undo;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMenuFactory"/> class.
    /// </summary>
    /// <param name="envService">The service for managing environmental variables.</param>
    /// <param name="factory">The factory for creating action forms.</param>
    /// <param name="drift">The service that reverts drifted rows to the baseline.</param>
    /// <param name="undoHistory">The history a bulk delete is batched on.</param>
    public ContextMenuFactory(IEnvService envService, IActionFactory<EnvModel> factory, IDriftService drift, IUndoHistory undoHistory)
    {
        _env = envService;
        _entryFormFactory = factory;
        _drift = drift;
        _undo = undoHistory;
    }

    /// <summary>
    /// Creates the context menu for a row of a grid, with operations like Create, Edit, Delete, and Copy.
    /// </summary>
    /// <param name="grid">The DataGridView to attach the context menu to.</param>
    /// <param name="pm">The environmental variable model associated with the selected row.</param>
    /// <returns>A configured ContextMenuStrip with menu items based on the model's state.</returns>
    public ContextMenuStrip CreateForRow(DataGridView grid, EnvModel pm)
    {
        var cms = new ContextMenuStrip();

        // Without elevation the Machine scope is read-only, so don't offer mutating actions
        // that would only be rejected on click.
        bool canMutate = AdminChecker.CanMutate(grid);

        // A right-click inside a multi-selection keeps it, so the menu can tell: single-row
        // actions (Edit, Restore, Reset) hide when several rows are selected, bulk ones
        // (Delete, Copy) stay and act on all of them.
        var selectedCount = grid.SelectedRows.Count;

        // Restore/Reset are ungated in-memory operations, but they still belong to the grid's scope;
        // a control without one denies them like every other mutation.
        var scope = ControlHelper.GetTabType(grid) is { } type ? _env.GetScope(type) : null;

        if (scope is not null && selectedCount <= 1)
        {
            if (pm.State == EnvironmentalVariableState.Deleted)
                cms.Items.Add("Restore", null, (_, _) => scope.Restore(pm));
            else if (pm.State == EnvironmentalVariableState.Modified)
                cms.Items.Add("Reset", null, (_, _) => scope.Reset(pm));
        }

        if (canMutate)
        {
            if (selectedCount <= 1)
                cms.Items.Add("Edit", null, (_, _) => RowActionHelper.Edit(grid, _entryFormFactory));

            cms.Items.Add(
                selectedCount > 1 ? $"Delete {selectedCount} variables" : "Delete",
                null,
                (_, _) => RowActionHelper.Delete(grid, _env, _undo));
            cms.Items.Add(new ToolStripSeparator());
        }

        // Copy items: name and value copy one property per line; "Copy variables" copies the
        // whole selection as NAME=value lines, the same text Ctrl+C puts on the clipboard for
        // a multi-selection.
        cms.Items.Add("Copy name", null, (_, _) => CopyToClipboard(grid, ClipboardHelper.FormatNameLines));
        cms.Items.Add("Copy value", null, (_, _) => CopyToClipboard(grid, ClipboardHelper.FormatValueLines));
        cms.Items.Add("Copy variables (name=value)", null, (_, _) => CopyToClipboard(grid, ClipboardHelper.FormatNameValueLines));

        // The remaining items act on the current row only, so like Edit/Restore/Reset they
        // hide in a multi-selection; the bulk equivalents live in the View menu.
        if (selectedCount <= 1 && pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Add("Copy original path", null, (_, _) => ClipboardHelper.CopyText(pm.OriginalPath));

        // The Registry Editor is scope-level, not row-level, so it stays in a multi-selection.
        if (ControlHelper.GetTabType(grid) is { } regeditScope)
        {
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("Open in Registry Editor", null, (_, _) =>
            {
                if (!RegeditHelper.OpenInRegedit(regeditScope))
                    DialogHelper.ShowError($"Could not open the registry key {RegeditHelper.KeyPath(regeditScope)} in Registry Editor.", "Registry Editor");
            });
        }

        // Only a drifted row carries the live value the registry holds instead of the baseline.
        if (selectedCount <= 1 && pm.IsDrifted)
        {
            // Accepting the live value is an in-memory edit like Reset, so it is ungated.
            cms.Items.Add("Use live value", null, (_, _) => scope?.AcceptDrift(pm));

            // Reverting writes the baseline back to the registry, so it needs the permission.
            if (canMutate)
                cms.Items.Add("Revert to enviro value", null, (_, _) =>
                {
                    if (!_drift.Revert(pm))
                        DialogHelper.ShowError($"Could not write the original value of \"{pm.Name}\" back to the registry.", "Revert");
                });

            cms.Items.Add("Copy live value", null, (_, _) => ClipboardHelper.CopyText(pm.LiveValue!));
        }

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

        if (ControlHelper.GetTabType(grid) is { } regeditScope)
            cms.Items.Add("Open in Registry Editor", null, (_, _) =>
            {
                if (!RegeditHelper.OpenInRegedit(regeditScope))
                    DialogHelper.ShowError($"Could not open the registry key {RegeditHelper.KeyPath(regeditScope)} in Registry Editor.", "Registry Editor");
            });

        return cms;
    }

    /// <summary>
    /// Copies the formatted selection to the clipboard.
    /// </summary>
    /// <param name="g">The DataGridView whose selection to copy.</param>
    /// <param name="format">Formats the selected rows (see <see cref="ClipboardHelper"/>).</param>
    private void CopyToClipboard(DataGridView g, Func<IReadOnlyList<EnvModel>, string> format)
    {
        var models = ControlHelper.GetSelectedModels(g);

        if (models.Count > 0)
            ClipboardHelper.CopyText(format(models));
    }
}
