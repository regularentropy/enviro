using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Counts environmental variables by edit state and by the corrupted-path overlay.
/// </summary>
internal static class StateCounter
{
    /// <summary>
    /// Counts the rows in the given edit state across both scopes.
    /// </summary>
    /// <param name="bundle">The user + machine lists to inspect.</param>
    /// <param name="state">The state to count.</param>
    /// <returns>The number of matching rows.</returns>
    public static int CountByState(EnvModelBundle bundle, EnvironmentalVariableState state) =>
        bundle.User.Concat(bundle.Machine).Count(v => v.State == state);

    /// <summary>
    /// Counts the rows whose corrupted-path overlay is set across both scopes.
    /// </summary>
    /// <param name="bundle">The user + machine lists to inspect.</param>
    /// <returns>The number of rows marked as corrupted.</returns>
    public static int CountCorrupted(EnvModelBundle bundle) =>
        bundle.User.Concat(bundle.Machine).Count(v => v.IsCorrupted);
}
