namespace enviro.Static;

/// <summary>
/// Static helper class for validating environmental variable input.
/// </summary>
internal static class ValidationHelper
{
    /// <summary>
    /// Validates that both name and path are not empty or whitespace.
    /// Displays a warning dialog if validation fails.
    /// </summary>
    /// <param name="name">The environmental variable name to validate.</param>
    /// <param name="path">The environmental variable path to validate.</param>
    /// <returns>True if both name and path are valid, false otherwise.</returns>
    public static bool ValidateEnvironmentVariableInput(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            DialogHelper.ShowWarning("Name and path cannot be empty or consist only of spaces.", "Validation");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if the text contains the equals sign ('=') character, which is invalid in environmental variable names.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text contains '=', false otherwise.</returns>
    public static bool ContainsInvalidCharacters(string text) => text.Contains('=');

    /// <summary>
    /// Validates if the path is corrupted.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>True if the path is corrupted</returns>
    public static bool IsCorrupted(string path)
    {
        // Handling structures similar to PATH, which have variables separated by ;
        if (path.Contains(';'))
        {
            string[] pathArr = path.Split(';');
            foreach (var item in pathArr)
            {
                if (!IsValidEntry(item)) return true;
                return false;
            }
        }
        if (!IsValidEntry(path)) return true;
        return false;
    }

    /// <summary>
    /// Validating entry (whether the variable has corrupted path or a simple text)
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool IsValidEntry(string path)
    {
        // Unwrapping current entry from the %entry% form
        var extendedPath = Environment.ExpandEnvironmentVariables(path);

        if (!IsPathToDirectory(extendedPath)) return true;

        if (IsDirectoryExist(extendedPath)) return true;

        return false;
    }

    /// <summary>
    /// Check if the directory or file exists inside the given path
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if directory exists</returns>
    private static bool IsDirectoryExist(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath) || File.Exists(fullPath);
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
