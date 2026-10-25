namespace enviro.Static;

/// <summary>
/// Static helper class for validating environmental variable input.
/// </summary>
internal static class ValidationHelper
{
    /// <summary>
    /// Validates that both name and path are not empty or whitespace, and that neither holds a forbidden character.
    /// Displays a warning dialog if validation fails.
    /// </summary>
    /// <param name="name">The environmental variable name to validate.</param>
    /// <param name="path">The environmental variable path to validate.</param>
    /// <returns>True if both name and path are valid, false otherwise.</returns>
    public static bool ValidateEnvironmentVariableInput(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            DialogHelper.ShowWarning("Name and value cannot be empty or consist only of spaces.", "Validation");
            return false;
        }

        if (ContainsInvalidCharacters(name))
        {
            DialogHelper.ShowWarning("Name cannot contain '=' or a null character.", "Validation");
            return false;
        }

        if (ContainsInvalidValueCharacters(path))
        {
            DialogHelper.ShowWarning("Value cannot contain a null character.", "Validation");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Silent version of <see cref="ValidateEnvironmentVariableInput"/>; same rules, no dialog.
    /// </summary>
    /// <param name="name">The environmental variable name to check.</param>
    /// <param name="path">The environmental variable path to check.</param>
    /// <returns>True if the input may be submitted, false otherwise.</returns>
    public static bool IsValidEnvironmentVariableInput(string name, string path) =>
        !string.IsNullOrWhiteSpace(name)
        && !string.IsNullOrWhiteSpace(path)
        && !ContainsInvalidCharacters(name)
        && !ContainsInvalidValueCharacters(path);

    /// <summary>
    /// Checks a variable name for characters Windows cannot accept: '=' separates name from value in the
    /// environment block (.NET throws on it), NUL terminates the entry.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if the name contains a forbidden character, false otherwise.</returns>
    private static bool ContainsInvalidCharacters(string name) =>
        name.Contains('=') || name.Contains('\0');

    /// <summary>
    /// Checks a variable value for characters Windows cannot accept. Only NUL is forbidden; '=', ';', quotes
    /// and the shell metacharacters are legal inside a value.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value contains a forbidden character, false otherwise.</returns>
    private static bool ContainsInvalidValueCharacters(string value) => value.Contains('\0');

    /// <summary>
    /// Whether a variable is worth editing as a <c>;</c>-separated list of entries: its name is
    /// <c>PATH</c> (case-insensitive) or its value already contains a separator.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>True when the segment editor applies.</returns>
    public static bool IsPathList(string name, string value) =>
        string.Equals(name.Trim(), "PATH", StringComparison.OrdinalIgnoreCase)
        || value.Contains(';');

    /// <summary>
    /// Splits a value into its <c>;</c>-separated entries, dropping blank ones.
    /// </summary>
    /// <param name="value">The variable value to split.</param>
    /// <returns>The non-blank segments, in order.</returns>
    public static IEnumerable<string> SplitPathList(string value) =>
        value.Split(';').Where(segment => !string.IsNullOrWhiteSpace(segment));

    /// <summary>
    /// Joins entries into a <c>;</c>-separated value, dropping blank ones.
    /// </summary>
    /// <param name="segments">The entries to join.</param>
    /// <returns>The joined value.</returns>
    public static string JoinPathList(IEnumerable<string> segments) =>
        string.Join(';', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));

    /// <summary>
    /// Checks whether a single <c>;</c>-separated segment points at a missing location.
    /// The segment must already be expanded; non-path text is never missing.
    /// </summary>
    /// <param name="entry">The expanded segment to check.</param>
    /// <returns>True when the segment looks like a path but does not exist.</returns>
    public static bool IsMissingPath(string entry) => !IsValidEntry(entry);

    /// <summary>
    /// Validating entry (whether the variable has corrupted path or a simple text)
    /// </summary>
    /// <param name="path">The already expanded segment to check.</param>
    /// <returns>True when the segment is not a missing path.</returns>
    private static bool IsValidEntry(string path) =>
        !IsPathToDirectory(path) || IsDirectoryExist(path);

    /// <summary>
    /// Tells a folder from a file for a location the caller already knows exists; a path that cannot
    /// be resolved is not a folder.
    /// </summary>
    /// <param name="path">A path that <see cref="IsDirectoryExist"/> reported as existing.</param>
    /// <returns>True for a directory, false for a file or an unresolvable path.</returns>
    public static bool IsExistingDirectory(string path) =>
        TryResolveFullPath(path, out var fullPath) && Directory.Exists(fullPath);

    /// <summary>
    /// Check if the directory or file exists inside the given path
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if directory exists</returns>
    private static bool IsDirectoryExist(string path) =>
        TryResolveFullPath(path, out var fullPath) && (Directory.Exists(fullPath) || File.Exists(fullPath));

    /// <summary>
    /// Resolves a path to its full form, treating a malformed path as one that cannot point anywhere.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="fullPath">The resolved path, empty when resolution failed.</param>
    /// <returns>True when the path could be resolved.</returns>
    private static bool TryResolveFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Checks, whether the given string is the path to a directory
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if is the path</returns>
    public static bool IsPathToDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // If the path has smth like C:\ in it
        if (Path.IsPathRooted(path))
            return true;

        // If this is a simple text
        return false;
    }
}
