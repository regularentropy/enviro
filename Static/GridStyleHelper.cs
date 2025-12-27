using enviro.Models;

namespace enviro.Static;

internal static class GridStyleHelper
{
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
