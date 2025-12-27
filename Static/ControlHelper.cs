using enviro.Models;

namespace enviro.Static;

internal static class ControlHelper
{
    public static EnvModel? GetCurrentModel(DataGridView grid) =>
        grid.CurrentRow?.DataBoundItem as EnvModel;

    public static EnvironmentalVariableType GetTabType(Control control) =>
        control.Parent?.Text switch
        {
            "Machine" => EnvironmentalVariableType.Machine,
            "User" => EnvironmentalVariableType.User,
            _ => EnvironmentalVariableType.User
        };
}
