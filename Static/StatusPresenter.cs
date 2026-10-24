using enviro.Models;
using enviro.Services;

namespace enviro.Static;

/// <summary>
/// The content of the status strip: pending work, the corrupted-row count and the elevation state.
/// </summary>
/// <param name="Pending">The pending-changes summary.</param>
/// <param name="ShowCorrupted">Whether the corrupted-row count is shown, i.e. validation is enabled.</param>
/// <param name="Corrupted">The corrupted-row count text.</param>
/// <param name="Elevation">The elevation state.</param>
internal sealed record StatusSummary(string Pending, bool ShowCorrupted, string Corrupted, string Elevation);

/// <summary>
/// Computes what the status strip shows from the service state, the setting and the elevation.
/// It decides nothing about layout; the shell writes the strings onto its labels.
/// </summary>
internal static class StatusPresenter
{
    /// <summary>
    /// Summarises pending work, corrupted rows and the elevation state across both scopes.
    /// </summary>
    /// <param name="service">The service whose two scopes are counted.</param>
    /// <param name="enableCorruptedValidation">Whether the corrupted-row count is shown.</param>
    /// <param name="isAdmin">Whether the process is elevated.</param>
    /// <returns>The strings the status strip shows.</returns>
    public static StatusSummary Describe(IEnvService service, bool enableCorruptedValidation, bool isAdmin)
    {
        var added = CountByState(service, EnvironmentalVariableState.Added);
        var modified = CountByState(service, EnvironmentalVariableState.Modified);
        var deleted = CountByState(service, EnvironmentalVariableState.Deleted);
        var total = added + modified + deleted;

        var pending = total == 0
            ? "No pending changes"
            : $"{added} added · {modified} modified · {deleted} deleted";

        var corrupted = $"{StateCounter.CountCorrupted(service.User) + StateCounter.CountCorrupted(service.Machine)} corrupted";

        return new StatusSummary(
            pending,
            enableCorruptedValidation,
            corrupted,
            isAdmin ? "Administrator" : "Standard user");
    }

    /// <summary>
    /// Counts the rows in one edit state across both scopes.
    /// </summary>
    /// <param name="service">The service whose two scopes are counted.</param>
    /// <param name="state">The state to count.</param>
    /// <returns>The number of matching rows in the user and machine scopes.</returns>
    private static int CountByState(IEnvService service, EnvironmentalVariableState state) =>
        StateCounter.CountByState(service.User, state) + StateCounter.CountByState(service.Machine, state);
}
