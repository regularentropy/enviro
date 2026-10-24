using enviro.Models;

namespace enviro.Services;

/// <summary>
/// What one apply run produced, phrased for the shell to show.
/// </summary>
/// <param name="Succeeded">Whether every pending change was written.</param>
/// <param name="Message">The transient success summary, or the failure report.</param>
internal sealed record ApplyOutcome(bool Succeeded, string Message);

/// <summary>
/// Orchestrates an apply run — the pending count, the adapter call and the outcome text — so the
/// shell only has to show it. Nothing here touches a dialog, a timer or a control.
/// </summary>
/// <param name="_es">The store whose pending rows are applied and counted.</param>
/// <param name="_adapter">Where the tracked states are written to Windows.</param>
internal sealed class ApplyCoordinator(IEnvService _es, IPathAdapter _adapter)
{
    /// <summary>
    /// The number of rows pending across both scopes.
    /// </summary>
    /// <returns>The number of rows not in <c>Unchanged</c> in the user and machine scopes.</returns>
    public int PendingChanges() => CountPending(_es.User) + CountPending(_es.Machine);

    /// <summary>
    /// Applies every pending change and phrases what happened.
    /// </summary>
    /// <returns>The outcome; the caller shows <see cref="ApplyOutcome.Message"/>.</returns>
    public async Task<ApplyOutcome> ApplyAsync()
    {
        var applied = PendingChanges();

        var result = await _adapter.ApplyAsync();

        return result.Succeeded
            ? new ApplyOutcome(true, $"Applied {applied} {(applied == 1 ? "change" : "changes")}")
            : new ApplyOutcome(false, DescribeFailure(result));
    }

    /// <summary>
    /// Reports a failed apply: the row that stopped the run, why, and the work still left behind.
    /// </summary>
    /// <param name="result">The outcome of the failed run.</param>
    /// <returns>The message the shell shows.</returns>
    private string DescribeFailure(ApplyResult result) =>
        "Failed to apply changes.\n" +
        $"{result.FailedScope} variable \"{result.FailedModel?.Name}\" could not be written: {result.Error}\n" +
        $"{DescribeWritten(result)}\n" +
        $"{DescribePendingChanges()}";

    /// <summary>
    /// Says how many rows the failed run had already written.
    /// </summary>
    /// <param name="result">The outcome of the failed run.</param>
    /// <returns>A sentence such as "3 changes were written before the failure."</returns>
    private static string DescribeWritten(ApplyResult result)
    {
        var written = result.UserApplied + result.MachineApplied;
        return written == 0
            ? "No changes were written."
            : $"{written} {(written == 1 ? "change was" : "changes were")} written before the failure.";
    }

    /// <summary>
    /// Summarises the rows still pending after a failed apply, with a count per scope.
    /// </summary>
    /// <returns>A sentence such as "4 changes are still pending (3 User, 1 Machine)."</returns>
    private string DescribePendingChanges()
    {
        var userCount = CountPending(_es.User);
        var machineCount = CountPending(_es.Machine);
        var total = userCount + machineCount;

        if (total == 0)
            return "No changes appear to be pending.";

        var scopes = new List<string>(2);

        if (userCount > 0)
            scopes.Add($"{userCount} User");

        if (machineCount > 0)
            scopes.Add($"{machineCount} Machine");

        var noun = total == 1 ? "change is" : "changes are";
        return $"{total} {noun} still pending ({string.Join(", ", scopes)}).";
    }

    private static int CountPending(IEnvScope scope) =>
        scope.Rows.OfType<EnvModel>().Count(v => v.State != EnvironmentalVariableState.Unchanged);
}
