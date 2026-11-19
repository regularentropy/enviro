using System.Collections;
using enviro.Models;
using enviro.Static;

namespace enviro.Services;

/// <summary>
/// Detects drift between the workspace rows and the live registry: variables that were changed,
/// created or deleted outside of enviro (setx, an installer, group policy) while the workspace
/// was loaded.
/// </summary>
internal interface IDriftService
{
    /// <summary>
    /// Compares every row of one scope against the live registry values of its target and
    /// updates the drift overlay on each row.
    /// </summary>
    /// <param name="scope">The scope to check.</param>
    void CheckScope(IEnvScope scope);

    /// <summary>
    /// Checks both scopes, the user one first.
    /// </summary>
    void CheckAll();

    /// <summary>
    /// Clears the drift overlay from every row of both scopes, e.g. before a full refresh that
    /// re-reads the registry anyway.
    /// </summary>
    void ClearOverlay();

    /// <summary>
    /// Undoes one row's external change in the registry itself: the row's baseline is written
    /// back under its original name (or the variable is deleted when the workspace never had it),
    /// and the drift overlay is cleared. Refused for a scope this session must not mutate.
    /// </summary>
    /// <param name="pm">The drifted row to revert.</param>
    /// <returns>True when the registry was written, false otherwise.</returns>
    bool Revert(EnvModel pm);
}

/// <summary>
/// The drift check itself: the live variables of a scope's target are read once into a
/// case-insensitive dictionary, then every row's baseline is compared against that snapshot.
/// Synchronous by design — <see cref="EnvService.Reload"/> already reads both scopes on the UI
/// thread and registry reads are cheap.
/// </summary>
/// <param name="env">The service whose two scopes are checked.</param>
/// <param name="readVariables">
/// Reads the live variables of one target; injectable so tests can fake the registry.
/// </param>
/// <param name="writeVariable">
/// Writes one live variable of a target; injectable so tests can fake the registry. A null value
/// deletes the variable.
/// </param>
/// <param name="log">The failure log; the writer's exceptions are logged and swallowed.</param>
internal sealed class DriftService(
    IEnvService env,
    Func<EnvironmentVariableTarget, IDictionary>? readVariables = null,
    Action<string, string?, EnvironmentVariableTarget>? writeVariable = null,
    IAppLog? log = null) : IDriftService
{
    private readonly IEnvService env = env;
    private readonly Func<EnvironmentVariableTarget, IDictionary> readVariables =
        readVariables ?? Environment.GetEnvironmentVariables;
    private readonly Action<string, string?, EnvironmentVariableTarget> writeVariable =
        writeVariable ?? Environment.SetEnvironmentVariable;
    private readonly IAppLog? log = log;

    /// <summary>
    /// Compares every row of one scope against the live registry values of its target and
    /// updates the drift overlay on each row. The baseline a row is compared against is its
    /// original value — or nothing for an <c>Added</c> row, which the registry does not hold
    /// yet — so the check catches external edits, external deletes and external creates alike.
    /// </summary>
    /// <param name="scope">The scope to check.</param>
    public void CheckScope(IEnvScope scope)
    {
        var live = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Collections.DictionaryEntry de in readVariables(EnvTypeConverter.ToTarget(scope.Type)))
            live[(string)de.Key] = de.Value?.ToString();

        foreach (EnvModel row in scope.Rows)
        {
            var baseline = row.State == EnvironmentalVariableState.Added ? null : row.OriginalPath;
            live.TryGetValue(row.OriginalName, out var liveValue);
            var drifted = !string.Equals(liveValue, baseline, StringComparison.Ordinal);

            // Log the rising edge only: a flag that stays true across checks must not spam the
            // log every check interval.
            if (drifted && !row.IsDrifted)
                log?.Info($"Drift detected: {scope.Type} variable \"{row.OriginalName}\" changed outside enviro: \"{row.OriginalPath}\" -> \"{liveValue ?? "(deleted)"}\"");

            row.IsDrifted = drifted;
            row.LiveValue = drifted ? liveValue : null;
        }

        // Live names no row holds were created outside enviro: they come in as Added+drifted rows
        // (without an undo delta — AddExternal records none), so the pending count and the drift
        // overlay make them visible and Apply's delete can still remove them. AddExternal skips
        // every name the scope already holds, so the check is idempotent.
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (EnvModel row in scope.Rows)
        {
            known.Add(row.Name);
            known.Add(row.OriginalName);
        }

        var external = live
            .Where(kv => kv.Value is not null && known.Add(kv.Key))
            .Select(kv => new EnvModel
            {
                Name = kv.Key,
                OriginalName = kv.Key,
                Path = kv.Value!,
                OriginalPath = kv.Value!,
                State = EnvironmentalVariableState.Added,
                IsDrifted = true,
                LiveValue = kv.Value,
            })
            .ToList();

        if (external.Count == 0)
            return;

        scope.AddExternal(external);

        foreach (var row in external)
            log?.Info($"Drift detected: {scope.Type} variable \"{row.Name}\" was created outside enviro: \"{row.Path}\"");
    }

    /// <summary>
    /// Checks both scopes, the user one first.
    /// </summary>
    public void CheckAll()
    {
        CheckScope(env.User);
        CheckScope(env.Machine);
    }

    /// <summary>
    /// Clears the drift overlay from every row of both scopes.
    /// </summary>
    public void ClearOverlay()
    {
        ClearScope(env.User);
        ClearScope(env.Machine);
    }

    /// <summary>
    /// Undoes one row's external change in the registry itself: the row's baseline is written
    /// back under its original name, or the variable is deleted when the row is an <c>Added</c>
    /// one the workspace never held. Refused without touching the registry when the row's scope
    /// must not be mutated this session. A failed write keeps the overlay, so the row stays
    /// flagged and a later retry can still reach it.
    /// </summary>
    /// <param name="pm">The drifted row to revert.</param>
    /// <returns>True when the registry was written, false when refused or failed.</returns>
    public bool Revert(EnvModel pm)
    {
        var scope = env.User.Rows.Contains(pm) ? env.User
            : env.Machine.Rows.Contains(pm) ? env.Machine
            : null;

        bool canMutate;

        try
        {
            canMutate = scope is not null && AdminChecker.CanMutate(scope.Type);
        }
        catch (PlatformNotSupportedException)
        {
            // The elevation check is Windows machinery; a platform that cannot even ask is
            // treated as refused, so a headless run never writes the registry.
            canMutate = false;
        }

        if (scope is null || !canMutate)
            return false;

        var target = EnvTypeConverter.ToTarget(scope.Type);

        try
        {
            // An Added row's baseline is absent by definition, so reverting it deletes the
            // variable something outside enviro created.
            writeVariable(pm.OriginalName, pm.State == EnvironmentalVariableState.Added ? null : pm.OriginalPath, target);
        }
        catch (Exception ex)
        {
            log?.Error($"Failed to revert variable '{pm.OriginalName}' to its baseline.", ex);
            return false;
        }

        pm.IsDrifted = false;
        pm.LiveValue = null;

        log?.Info($"Reverted {scope.Type} variable \"{pm.OriginalName}\" to baseline");

        return true;
    }

    /// <summary>
    /// Clears the drift overlay from every row of one scope.
    /// </summary>
    /// <param name="scope">The scope whose overlay is cleared.</param>
    private static void ClearScope(IEnvScope scope)
    {
        foreach (EnvModel row in scope.Rows)
        {
            row.IsDrifted = false;
            row.LiveValue = null;
        }
    }
}
