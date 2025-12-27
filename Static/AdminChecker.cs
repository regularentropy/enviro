using System.Security.Principal;

namespace enviro.Static;

internal static class AdminChecker
{
    public static bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

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
