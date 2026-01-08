using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Static helper class for applying visual styles to DataGridView cells based on environmental variable state.
/// </summary>
internal static class GridStyleHelper
{
    /// <summary>
    /// Applies color and font formatting to a DataGridView cell based on the environmental variable's state.
    /// Added: Light green background
    /// Modified: Light yellow background
    /// Deleted: Light coral background with strikeout text
    /// Unchanged: White background
    /// </summary>
    /// <param name="e">The cell formatting event arguments.</param>
    /// <param name="model">The environmental variable model associated with the cell.</param>
    public static void ApplyStateFormatting(DataGridViewCellFormattingEventArgs e, EnvModel model)
    {
        switch (model.State)
        {
            case EnvironmentalVariableState.Added:
                e.CellStyle.BackColor = Color.LightGreen;
                break;
            case EnvironmentalVariableState.Modified:
                e.CellStyle.BackColor = Color.LightYellow;
                break;
            case EnvironmentalVariableState.Deleted:
                e.CellStyle.BackColor = Color.LightCoral;
                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Strikeout);
                break;
            case EnvironmentalVariableState.Unchanged:
                e.CellStyle.BackColor = Color.White;
                break;
        }
    }
}
