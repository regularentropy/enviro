using enviro.Models;

namespace enviro.Static;

internal static class EnvTypeConverter
{
    public static EnvironmentVariableTarget ToTarget(EnvironmentalVariableType type) =>
        type switch
        {
            EnvironmentalVariableType.User => EnvironmentVariableTarget.User,
            EnvironmentalVariableType.Machine => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.User
        };
}
