using enviro.Models;
using enviro.Static;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace enviro.Services;

/// <summary>
/// The JSON document exchanged by export and import: one section per scope, keyed by variable name.
/// </summary>
/// <remarks>
/// <see cref="Version"/> defaults to 0, so a document without a version is rejected instead of
/// being silently read as the current format.
/// </remarks>
internal sealed record VariableFile
{
    /// <summary>
    /// The only format version this build reads and writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The format version; any other value is rejected on import.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// The user variables, or null when the section is absent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? User { get; init; }

    /// <summary>
    /// The machine variables, or null when the section is absent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Machine { get; init; }

    /// <summary>
    /// The user section as import entries, empty when the section is absent.
    /// </summary>
    /// <returns>The entries in file order.</returns>
    public IReadOnlyList<KeyValuePair<string, string>> UserEntries() => Entries(User);

    /// <summary>
    /// The machine section as import entries, empty when the section is absent.
    /// </summary>
    /// <returns>The entries in file order.</returns>
    public IReadOnlyList<KeyValuePair<string, string>> MachineEntries() => Entries(Machine);

    private static IReadOnlyList<KeyValuePair<string, string>> Entries(Dictionary<string, string>? section) =>
        section is null ? [] : [.. section];
}

/// <summary>
/// What one file entry means for the review window; null when a list carries no comparison.
/// </summary>
internal enum VariableFileEntryStatus
{
    /// <summary>
    /// The name is not in the scope: importing adds it.
    /// </summary>
    New,

    /// <summary>
    /// The name already has a row with a different value: importing replaces it.
    /// </summary>
    Overwrites,

    /// <summary>
    /// The row already holds the identical value: importing leaves it untouched.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The input rules reject the entry: importing would skip it.
    /// </summary>
    Skipped,

    /// <summary>
    /// The entry belongs to the machine scope and this session may not write it.
    /// </summary>
    MachineSkipped,
}

/// <summary>
/// One line of the import/export review window: the entry as the file holds it, the scope it
/// belongs to, and — on an import — how the workspace compares with it.
/// </summary>
/// <param name="Scope">The scope the entry belongs to.</param>
/// <param name="Name">The variable name.</param>
/// <param name="Value">The value the file holds.</param>
/// <param name="Status">The import comparison; null when the list reviews an export.</param>
/// <param name="CurrentValue">
/// The workspace value an overwrite would replace, or null when no collision is shown.
/// </param>
internal sealed record VariableFileEntry(
    EnvironmentalVariableType Scope,
    string Name,
    string Value,
    VariableFileEntryStatus? Status,
    string? CurrentValue = null);

/// <summary>
/// Reads and writes the variable file used by export and import, including the in-memory snapshot
/// an export writes. IO and format failures come back as a message instead of an exception.
/// </summary>
internal interface IVariableFileService
{
    /// <summary>
    /// Builds the document for the current workspace: every row that is not pending deletion, so a
    /// pending deletion is never backed up.
    /// </summary>
    /// <param name="service">The store to snapshot.</param>
    /// <returns>The document to write.</returns>
    VariableFile Snapshot(IEnvService service);

    /// <summary>
    /// Lists the document's entries for the export review window, grouped by scope in name order.
    /// </summary>
    /// <param name="file">The document an export would write.</param>
    /// <returns>One entry per variable, without a comparison.</returns>
    IReadOnlyList<VariableFileEntry> Entries(VariableFile file);

    /// <summary>
    /// Compares the document with the workspace for the import review window: each entry's
    /// status and the workspace value an overwrite would replace.
    /// </summary>
    /// <param name="file">The document read from disk.</param>
    /// <param name="service">The store the import would merge into.</param>
    /// <param name="canImportMachine">
    /// Whether this session may write the machine scope; false marks every machine entry
    /// <see cref="VariableFileEntryStatus.MachineSkipped"/>.
    /// </param>
    /// <returns>One entry per variable, grouped by scope in name order.</returns>
    IReadOnlyList<VariableFileEntry> Compare(VariableFile file, IEnvService service, bool canImportMachine);

    /// <summary>
    /// Reads one variable file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>
    /// The document, or null and the user-facing message when the file is missing, unreadable,
    /// invalid JSON or written in another format version.
    /// </returns>
    (VariableFile? File, string? Error) Read(string path);

    /// <summary>
    /// Writes one variable file.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="file">The document to write.</param>
    /// <returns>Null on success, otherwise the user-facing message.</returns>
    string? Save(string path, VariableFile file);
}

/// <summary>
/// The variable file: JSON with a version and one name-keyed section per scope.
/// </summary>
/// <param name="_log">Where read and write failures are recorded.</param>
internal sealed class VariableFileService(IAppLog _log) : IVariableFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public VariableFile Snapshot(IEnvService service) =>
        new()
        {
            Version = VariableFile.CurrentVersion,
            User = Section(service.User),
            Machine = Section(service.Machine),
        };

    /// <inheritdoc />
    public IReadOnlyList<VariableFileEntry> Entries(VariableFile file) =>
    [
        .. ExportEntries(EnvironmentalVariableType.User, file.User),
        .. ExportEntries(EnvironmentalVariableType.Machine, file.Machine),
    ];

    /// <inheritdoc />
    public IReadOnlyList<VariableFileEntry> Compare(
        VariableFile file, IEnvService service, bool canImportMachine) =>
    [
        .. ImportEntries(EnvironmentalVariableType.User, file.User, service.User, canImportMachine: true),
        .. ImportEntries(EnvironmentalVariableType.Machine, file.Machine, service.Machine, canImportMachine),
    ];

    /// <inheritdoc />
    public (VariableFile? File, string? Error) Read(string path)
    {
        VariableFile? file;

        try
        {
            using var stream = File.OpenRead(path);
            file = JsonSerializer.Deserialize<VariableFile>(stream, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            _log.Error($"Could not read variables from {path}.", ex);
            return (null, DescribeFailure(path, ex.Message));
        }

        if (file is null)
            return Fail(path, "The file is empty.");

        if (file.Version != VariableFile.CurrentVersion)
            return Fail(path, $"Format version {file.Version} is not supported; this build reads version {VariableFile.CurrentVersion}.");

        return (Normalise(file), null);
    }

    /// <inheritdoc />
    public string? Save(string path, VariableFile file)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            _log.Error($"Could not write variables to {path}.", ex);
            return DescribeFailure(path, ex.Message);
        }
    }

    /// <summary>
    /// Lists one section's entries for an export review, without a comparison.
    /// </summary>
    /// <param name="scope">The section's scope.</param>
    /// <param name="section">The section, or null when the file has none.</param>
    /// <returns>The entries in name order.</returns>
    private static IEnumerable<VariableFileEntry> ExportEntries(
        EnvironmentalVariableType scope, Dictionary<string, string>? section) =>
        section is null
            ? []
            : section
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new VariableFileEntry(scope, entry.Key, entry.Value, Status: null));

    /// <summary>
    /// Compares one section's entries with its scope, applying the same rules the import merge uses.
    /// </summary>
    /// <param name="scope">The section's scope.</param>
    /// <param name="section">The section, or null when the file has none.</param>
    /// <param name="workspace">The scope the entries would merge into.</param>
    /// <param name="canImportMachine">Whether this session may write the machine scope.</param>
    /// <returns>The entries in name order, each with its comparison.</returns>
    private static IEnumerable<VariableFileEntry> ImportEntries(
        EnvironmentalVariableType scope,
        Dictionary<string, string>? section,
        IEnvScope workspace,
        bool canImportMachine) =>
        section is null
            ? []
            : section
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => CompareEntry(scope, entry, workspace, canImportMachine));

    /// <summary>
    /// Resolves one entry's import status. The elevation gate comes first, so a machine section in a
    /// standard-user session reads as skipped as a whole — the same gate the import itself applies.
    /// </summary>
    /// <param name="scope">The entry's scope.</param>
    /// <param name="entry">The name and value the file holds.</param>
    /// <param name="workspace">The scope the entry would merge into.</param>
    /// <param name="canImportMachine">Whether this session may write the machine scope.</param>
    /// <returns>The entry with its status and, for an overwrite, the current workspace value.</returns>
    private static VariableFileEntry CompareEntry(
        EnvironmentalVariableType scope,
        KeyValuePair<string, string> entry,
        IEnvScope workspace,
        bool canImportMachine)
    {
        var (name, value) = entry;

        if (scope == EnvironmentalVariableType.Machine && !canImportMachine)
            return new VariableFileEntry(scope, name, value, VariableFileEntryStatus.MachineSkipped);

        if (!ValidationHelper.IsValidEnvironmentVariableInput(name, value))
            return new VariableFileEntry(scope, name, value, VariableFileEntryStatus.Skipped);

        var existing = workspace.GetByName(name);

        if (existing is null)
            return new VariableFileEntry(scope, name, value, VariableFileEntryStatus.New);

        // A pending deletion is restored by the merge, so it is an overwrite like any other row.
        if (existing.State != EnvironmentalVariableState.Deleted
            && string.Equals(existing.Path, value, StringComparison.Ordinal))
            return new VariableFileEntry(scope, name, value, VariableFileEntryStatus.Unchanged);

        return new VariableFileEntry(scope, name, value, VariableFileEntryStatus.Overwrites, existing.Path);
    }

    /// <summary>
    /// Turns one scope's rows into a name-keyed section, skipping rows whose deletion is pending.
    /// </summary>
    /// <param name="scope">The scope to snapshot.</param>
    /// <returns>The section, ordered by name.</returns>
    private static Dictionary<string, string> Section(IEnvScope scope)
    {
        var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in scope.Rows.Cast<EnvModel>()
                     .Where(row => row.State != EnvironmentalVariableState.Deleted)
                     .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase))
        {
            section[row.Name] = row.Path;
        }

        return section;
    }

    /// <summary>
    /// Drops null values and merges names that differ only in case into one case-insensitive
    /// section, so a hand-edited file cannot fail the import on its own shape.
    /// </summary>
    /// <param name="file">The document as read.</param>
    /// <returns>The document with both sections normalised.</returns>
    private static VariableFile Normalise(VariableFile file) =>
        file with { User = Normalise(file.User), Machine = Normalise(file.Machine) };

    private static Dictionary<string, string> Normalise(Dictionary<string, string>? section)
    {
        var normalised = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (section is null)
            return normalised;

        foreach (var (name, value) in section)
        {
            if (name is not null && value is not null)
                normalised[name] = value;
        }

        return normalised;
    }

    /// <summary>
    /// Records a format failure that has no exception behind it.
    /// </summary>
    /// <param name="path">The file that was read.</param>
    /// <param name="reason">The user-facing reason.</param>
    /// <returns>No document, and the message to show.</returns>
    private (VariableFile? File, string? Error) Fail(string path, string reason)
    {
        _log.Error($"Could not read variables from {path}: {reason}");
        return (null, DescribeFailure(path, reason));
    }

    /// <summary>
    /// Builds the one error message the shell shows, naming the file.
    /// </summary>
    /// <param name="path">The file involved.</param>
    /// <param name="reason">Why the operation failed.</param>
    /// <returns>The user-facing message.</returns>
    private static string DescribeFailure(string path, string reason) =>
        $"The variables could not be processed:\n{path}\n\n{reason}";
}
