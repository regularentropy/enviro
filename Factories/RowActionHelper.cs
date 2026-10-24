using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Factories;

/// <summary>
/// Scope-guarded create, edit and delete operations shared by the context menu, the toolbar
/// and the keyboard shortcuts.
/// </summary>
internal static class RowActionHelper
{
    /// <summary>
    /// Opens the create dialog for the grid's scope.
    /// </summary>
    /// <param name="grid">The grid that determines the scope.</param>
    /// <param name="forms">The factory that builds the entry dialog.</param>
    public static void Create(DataGridView grid, IActionFactory<EnvModel> forms)
    {
        if (ControlHelper.GetTabType(grid) is not { } scope || !AdminChecker.ValidateTab(grid))
            return;

        forms.Create(scope).ShowDialog();
    }

    /// <summary>
    /// Opens the edit dialog for the grid's current row.
    /// </summary>
    /// <param name="grid">The grid that holds the row and determines the scope.</param>
    /// <param name="forms">The factory that builds the entry dialog.</param>
    public static void Edit(DataGridView grid, IActionFactory<EnvModel> forms)
    {
        if (ControlHelper.GetTabType(grid) is not { } scope || !AdminChecker.ValidateTab(grid))
            return;

        var model = ControlHelper.GetCurrentModel(grid);

        if (model is not null)
            forms.Create(model, scope).ShowDialog();
    }

    /// <summary>
    /// Removes the grid's current row, through the service so state tracking stays intact.
    /// </summary>
    /// <param name="grid">The grid that holds the row and determines the scope.</param>
    /// <param name="envService">The store that owns the rows.</param>
    public static void Delete(DataGridView grid, IEnvService envService)
    {
        if (ControlHelper.GetTabType(grid) is not { } scope || !AdminChecker.ValidateTab(grid))
            return;

        var model = ControlHelper.GetCurrentModel(grid);

        if (model is not null)
            envService.RemoveEntry(model, scope);
    }
}
