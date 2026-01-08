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
    /// Determines the environmental variable type based on the parent TabPage's text.
    /// </summary>
    /// <param name="control">The control whose parent tab will be checked.</param>
    /// <returns>Machine if the parent tab is named "Machine", User otherwise.</returns>
    public static EnvironmentalVariableType GetTabType(Control control) =>
        control.Parent?.Text switch
        {
            "Machine" => EnvironmentalVariableType.Machine,
            "User" => EnvironmentalVariableType.User,
            _ => EnvironmentalVariableType.User
        };
}
