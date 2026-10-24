using enviro.Models;
using enviro.Static;

namespace enviro.Services;

/// <summary>
/// Existence verdict for one <c>;</c>-separated segment of a variable value.
/// </summary>
internal enum PathSegmentState
{
    /// <summary>
    /// A rooted path that resolves to a directory or file on disk.
    /// </summary>
    Exists,

    /// <summary>
    /// A rooted path that resolves to nothing on disk.
    /// </summary>
    Missing,

    /// <summary>
    /// Text that is not a rooted path, so it can never be missing.
    /// </summary>
    NotPath,
}

/// <summary>
/// Owns corrupted-path validation: the per-segment verdict, the whole-value verdict derived from
/// it, and the background scans that write <see cref="EnvModel.IsCorrupted"/>.
/// </summary>
/// <remarks>
/// The overlay is written on the thread that requested the scan (the UI thread), never on the
/// worker, and a verdict is dropped when its row moved or changed while the scan was running.
/// </remarks>
internal interface ICorruptedPathService
{
    /// <summary>
    /// Classifies one segment of a value; <c>%VAR%</c> references are expanded first.
    /// </summary>
    /// <param name="segment">One entry of a possibly <c>;</c>-separated value.</param>
    /// <returns>Whether the segment exists, is missing, or is not a path at all.</returns>
    PathSegmentState ValidateSegment(string segment);

    /// <summary>
    /// Determines whether any non-blank segment of the value is a rooted path that does not exist.
    /// Non-path text is never corrupted.
    /// </summary>
    /// <param name="value">The variable value to check.</param>
    /// <returns>True when at least one path-like segment is missing.</returns>
    bool IsCorrupted(string value);

    /// <summary>
    /// Requests a scan of one scope on a worker thread. A burst of requests collapses into one
    /// scan, a new request cancels the previous scan of the same scope, and the verdicts are
    /// applied only where the reviewed row still holds the scanned value. No-op while validation
    /// is turned off.
    /// </summary>
    /// <param name="scope">The scope whose rows should be reviewed.</param>
    void RequestScan(IEnvScope scope);

    /// <summary>
    /// Requests a scan of both scopes, each with its own debounce and cancellation.
    /// </summary>
    void RequestScanAll();

    /// <summary>
    /// Cancels every pending and in-flight scan and drops verdicts that have not been applied yet.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Cancels the scans and clears the overlay on both scopes; used when validation is turned off.
    /// </summary>
    void ClearOverlay();
}

/// <summary>
/// Validates variable values and keeps the <see cref="EnvModel.IsCorrupted"/> overlay up to date
/// without blocking the UI thread.
/// </summary>
internal sealed class CorruptedPathService : ICorruptedPathService
{
    /// <summary>
    /// Quiet period that collapses a burst of list changes (one per keystroke) into one scan.
    /// </summary>
    private const int DebounceMilliseconds = 150;

    private readonly IEnvService _env;
    private readonly IConfigService _config;
    private readonly IAppLog _log;

    /// <summary>Marshals a finished scan back to the thread that owns the models.</summary>
    private readonly Action<Action>? _post;

    /// <summary>One pending debounce + scan per scope; a new request for the scope replaces it.</summary>
    private readonly Dictionary<IEnvScope, CancellationTokenSource> _pending = [];

    /// <param name="env">The service whose two scopes are scanned.</param>
    /// <param name="config">Read for the validation switch; a request while it is off is a no-op.</param>
    /// <param name="log">Where a scan failure is recorded.</param>
    /// <param name="post">
    /// Optional marshaller, so the scan can be driven without a UI thread. When null the
    /// <see cref="SynchronizationContext"/> of the requesting thread is used.
    /// </param>
    public CorruptedPathService(IEnvService env, IConfigService config, IAppLog log, Action<Action>? post = null)
    {
        _env = env;
        _config = config;
        _log = log;
        _post = post;
    }

    /// <inheritdoc />
    public PathSegmentState ValidateSegment(string segment)
    {
        var expanded = Environment.ExpandEnvironmentVariables(segment);

        if (!ValidationHelper.IsPathToDirectory(expanded))
            return PathSegmentState.NotPath;

        return ValidationHelper.IsMissingPath(segment) ? PathSegmentState.Missing : PathSegmentState.Exists;
    }

    /// <inheritdoc />
    public bool IsCorrupted(string value)
    {
        foreach (var segment in value.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            if (ValidateSegment(segment) == PathSegmentState.Missing)
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void RequestScan(IEnvScope scope)
    {
        if (!_config.Config.EnableCorruptedValidation)
            return;

        // Snapshot on the caller's thread: the worker only sees values, never the live rows.
        List<(EnvModel Row, string Value)> snapshot =
            [.. scope.Rows.OfType<EnvModel>().Select(row => (Row: row, Value: row.Path))];

        var post = CreateMarshaller();

        if (post is null)
        {
            // Without a way back to the requesting thread the verdicts would write models off it.
            _log.Error($"Corrupted-path scan of the {scope.Type} scope skipped: no synchronization context.");
            return;
        }

        CancelScope(scope);

        var cancellation = new CancellationTokenSource();
        _pending[scope] = cancellation;

        _ = ScanAsync(scope, snapshot, post, cancellation.Token);
    }

    /// <inheritdoc />
    public void RequestScanAll()
    {
        RequestScan(_env.User);
        RequestScan(_env.Machine);
    }

    /// <inheritdoc />
    public void Cancel()
    {
        var pending = _pending.Values.ToArray();
        _pending.Clear();

        foreach (var cancellation in pending)
            cancellation.Cancel();
    }

    /// <inheritdoc />
    public void ClearOverlay()
    {
        Cancel();

        foreach (var scope in new[] { _env.User, _env.Machine })
        {
            foreach (var row in scope.Rows.OfType<EnvModel>())
                row.IsCorrupted = false;
        }
    }

    /// <summary>
    /// Debounces and scans off the calling thread, then posts the verdicts back.
    /// </summary>
    /// <param name="scope">The scope the rows belong to.</param>
    /// <param name="snapshot">The rows and values as they were when the scan was requested.</param>
    /// <param name="post">Marshals the result to the requesting thread.</param>
    /// <param name="cancellation">Cancelled by a newer request, the toggle or the closing window.</param>
    private async Task ScanAsync(
        IEnvScope scope,
        List<(EnvModel Row, string Value)> snapshot,
        Action<Action> post,
        CancellationToken cancellation)
    {
        try
        {
            var verdicts = await Task.Run(async () =>
            {
                await Task.Delay(DebounceMilliseconds, cancellation).ConfigureAwait(false);

                return snapshot
                    .Select(entry => (entry.Row, entry.Value, Corrupted: IsCorrupted(entry.Value)))
                    .ToList();
            }, cancellation).ConfigureAwait(false);

            post(() => Apply(scope, verdicts, cancellation));
        }
        catch (OperationCanceledException)
        {
            // A newer request, the disabled option or the closing window superseded this scan.
        }
        catch (Exception ex)
        {
            _log.Error("Corrupted-path scan failed.", ex);
        }
    }

    /// <summary>
    /// Writes the overlay on the requesting thread, dropping a verdict whose row was removed from
    /// the scope or edited while the scan was running.
    /// </summary>
    /// <param name="scope">The scope the rows belong to.</param>
    /// <param name="verdicts">The rows, the values that were scanned and their verdicts.</param>
    /// <param name="cancellation">Checked again, because a result can be posted and then superseded.</param>
    private static void Apply(
        IEnvScope scope,
        List<(EnvModel Row, string Value, bool Corrupted)> verdicts,
        CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
            return;

        foreach (var verdict in verdicts)
        {
            if (scope.Rows.Contains(verdict.Row) && verdict.Row.Path == verdict.Value)
                verdict.Row.IsCorrupted = verdict.Corrupted;
        }
    }

    /// <summary>
    /// The marshaller for one scan, resolved on the requesting thread.
    /// </summary>
    /// <returns>
    /// The injected seam, a post through the caller's synchronization context, or null when there is
    /// neither, in which case the scan is not started.
    /// </returns>
    private Action<Action>? CreateMarshaller()
    {
        if (_post is { } post)
            return post;

        var context = SynchronizationContext.Current;

        return context is null ? null : action => context.Post(_ => action(), null);
    }

    /// <summary>
    /// Cancels one scope's pending scan. The source is not disposed: the scan may still be
    /// unwinding on the token it cancelled.
    /// </summary>
    /// <param name="scope">The scope whose pending scan should be superseded.</param>
    private void CancelScope(IEnvScope scope)
    {
        if (_pending.Remove(scope, out var cancellation))
            cancellation.Cancel();
    }
}
