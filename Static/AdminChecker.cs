using System.Security.Principal;

namespace enviro.Static;

/// <summary>
/// Static helper class for checking administrator privileges.
/// </summary>
internal static class AdminChecker
{
    /// <summary>
    /// Determines whether the current application is running with administrator privileges.
    /// </summary>
    /// <returns>True if running as administrator, false otherwise.</returns>
    public static bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Validates that the user has administrator privileges when accessing the Machine tab.
    /// Shows an error dialog if the user tries to access Machine variables without admin rights.
    /// </summary>
    /// <param name="c">The control to check (typically a DataGridView).</param>
    /// <returns>True if the operation is allowed, false if access is denied.</returns>
    public static bool ValidateTab(Control c)
    {
        var tab = (TabPage)c.Parent!;
        if (tab?.Text == "Machine" && !IsAdmin())
        {
            DialogHelper.ShowError("Access Denied", "Please run as administrator");
            return false;
        }
        return true;
    }
}
