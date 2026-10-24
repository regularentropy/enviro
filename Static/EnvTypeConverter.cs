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
    /// <returns>EnvironmentVariableTarget.User for User type, EnvironmentVariableTarget.Machine for Machine type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type is neither <see cref="EnvironmentalVariableType.User"/> nor
    /// <see cref="EnvironmentalVariableType.Machine"/>.
    /// </exception>
    public static EnvironmentVariableTarget ToTarget(EnvironmentalVariableType type) =>
        type switch
        {
            EnvironmentalVariableType.User => EnvironmentVariableTarget.User,
            EnvironmentalVariableType.Machine => EnvironmentVariableTarget.Machine,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown environmental variable type.")
        };
}
