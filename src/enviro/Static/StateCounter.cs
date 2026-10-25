using enviro.Models;
using enviro.Services;

namespace enviro.Static;

/// <summary>
/// Counts environmental variables by edit state and by the corrupted-path overlay.
/// </summary>
internal static class StateCounter
{
    /// <summary>
    /// Counts the rows of one scope in the given edit state.
    /// </summary>
    /// <param name="scope">The scope whose rows are counted.</param>
    /// <param name="state">The state to count.</param>
    /// <returns>The number of matching rows.</returns>
    public static int CountByState(IEnvScope scope, EnvironmentalVariableState state) =>
        scope.Rows.OfType<EnvModel>().Count(v => v.State == state);

    /// <summary>
    /// Counts the rows of one scope whose corrupted-path overlay is set.
    /// </summary>
    /// <param name="scope">The scope whose rows are counted.</param>
    /// <returns>The number of rows marked as corrupted.</returns>
    public static int CountCorrupted(IEnvScope scope) =>
        scope.Rows.OfType<EnvModel>().Count(v => v.IsCorrupted);
}
