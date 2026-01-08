using enviro.Models;

namespace enviro.Static;

/// <summary>
/// Static helper class for converting between EnvironmentalVariableType and EnvironmentVariableTarget.
/// </summary>
internal static class EnvTypeConverter
{
    /// <summary>
    /// Converts an EnvironmentalVariableType to the corresponding EnvironmentVariableTarget.
    /// </summary>
    /// <param name="type">The environmental variable type to convert.</param>
    /// <returns>EnvironmentVariableTarget.Machine for Machine type, EnvironmentVariableTarget.User otherwise.</returns>
    public static EnvironmentVariableTarget ToTarget(EnvironmentalVariableType type) =>
        type switch
        {
            EnvironmentalVariableType.User => EnvironmentVariableTarget.User,
            EnvironmentalVariableType.Machine => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.User
        };
}
