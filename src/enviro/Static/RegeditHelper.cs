using System.Diagnostics;
using enviro.Models;
using enviro.Services;
using Microsoft.Win32;

namespace enviro.Static;

/// <summary>
/// Jumps the Registry Editor to one of the app's scope keys. regedit's jump is a contract on
/// its own persisted state: writing <c>LastKey</c> before starting the process makes it open
/// with that key selected.
/// </summary>
internal static class RegeditHelper
{
    /// <summary>
    /// The registry key path (regedit export notation) the grid's scope lives under.
    /// Pure string mapping so it stays unit-testable off Windows.
    /// </summary>
    public static string KeyPath(EnvironmentalVariableType type) => type switch
    {
        EnvironmentalVariableType.User => RegFileService.UserSection,
        EnvironmentalVariableType.Machine => RegFileService.MachineSection,
        _ => string.Empty,
    };

    /// <summary>
    /// Opens regedit focused on the scope key. Launching an external editor is best effort;
    /// a failure (missing regedit, denied registry write, non-Windows platform) is reported
    /// by the return value instead of an exception.
    /// </summary>
    public static bool OpenInRegedit(EnvironmentalVariableType type)
    {
        try
        {
            Registry.SetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
                "LastKey",
                KeyPath(type));
            Process.Start(new ProcessStartInfo { FileName = "regedit.exe", UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
