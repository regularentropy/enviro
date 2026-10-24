using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Static helper class for extracting information from controls.
/// </summary>
internal static class ControlHelper
{
    /// <summary>
    /// Gets the currently selected environmental variable model from a DataGridView.
    /// </summary>
    /// <param name="grid">The DataGridView to extract the model from.</param>
    /// <returns>The EnvModel bound to the current row, or null if no row is selected.</returns>
    public static EnvModel? GetCurrentModel(DataGridView grid) =>
        grid.CurrentRow?.DataBoundItem as EnvModel;

    /// <summary>
    /// Determines the environmental variable type carried by the nearest ancestor control's
    /// <see cref="Control.Tag"/>; <c>TabFactory</c> tags each tab page with its scope.
    /// </summary>
    /// <param name="control">The control whose ancestor chain will be walked, starting with the control itself.</param>
    /// <returns>The scope of the nearest scope-carrying ancestor, or null when no ancestor carries one.</returns>
    public static EnvironmentalVariableType? GetTabType(Control control)
    {
        for (var c = control; c is not null; c = c.Parent)
        {
            if (c.Tag is EnvironmentalVariableType scope)
                return scope;
        }

        return null;
    }
}
