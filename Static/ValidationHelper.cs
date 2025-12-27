namespace enviro.Static;

internal static class ValidationHelper
{
    public static bool ValidateEnvironmentVariableInput(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            DialogHelper.ShowWarning("Name and path cannot be empty or consist only of spaces.", "Validation");
            return false;
        }
        return true;
    }

    public static bool ContainsInvalidCharacters(string text) => text.Contains('=');
}
