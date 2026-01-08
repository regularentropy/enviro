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
}
