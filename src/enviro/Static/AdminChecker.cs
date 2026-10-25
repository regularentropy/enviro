using enviro.Models;
using System.Security.Principal;

namespace enviro.Static;

/// <summary>
/// Static helper class for checking administrator privileges.
/// </summary>
internal static class AdminChecker
{
    // Elevation cannot change while the process runs, so the check is evaluated once.
    private static readonly Lazy<bool> IsElevated = new(() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator));

    /// <summary>
    /// Determines whether the current application is running with administrator privileges.
    /// The result is cached for the lifetime of the process.
    /// </summary>
    /// <returns>True if running as administrator, false otherwise.</returns>
    public static bool IsAdmin() => IsElevated.Value;

    /// <summary>
    /// Determines whether the given scope accepts mutations in this session.
    /// User scope is always mutable; Machine scope requires elevation.
    /// </summary>
    /// <param name="scope">The scope to check.</param>
    /// <returns>True if mutations are allowed for the scope.</returns>
    public static bool CanMutate(EnvironmentalVariableType scope) =>
        scope != EnvironmentalVariableType.Machine || IsAdmin();

    /// <summary>
    /// Determines whether the scope that owns the control accepts mutations in this session.
    /// A control with no scope-carrying ancestor denies mutations.
    /// </summary>
    /// <param name="c">The control to check (typically a DataGridView).</param>
    /// <returns>True if mutations are allowed for the control's scope.</returns>
    public static bool CanMutate(Control c) =>
        ControlHelper.GetTabType(c) is { } scope && CanMutate(scope);

    /// <summary>
    /// Validates that the user has administrator privileges when accessing the Machine tab.
    /// Shows an error dialog if the user tries to access Machine variables without admin rights.
    /// </summary>
    /// <param name="c">The control to check (typically a DataGridView).</param>
    /// <returns>True if the operation is allowed, false if access is denied.</returns>
    public static bool ValidateTab(Control c)
    {
        if (CanMutate(c))
            return true;

        DialogHelper.ShowError("Access Denied", "Please run as administrator");
        return false;
    }
}
