using enviro.Models;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class EnvTypeConverterTests
{
    [Fact]
    public void ToTarget_maps_user_and_machine()
    {
        Assert.Equal(EnvironmentVariableTarget.User, EnvTypeConverter.ToTarget(EnvironmentalVariableType.User));
        Assert.Equal(EnvironmentVariableTarget.Machine, EnvTypeConverter.ToTarget(EnvironmentalVariableType.Machine));
    }

    [Fact]
    public void ToTarget_throws_on_an_unknown_type() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnvTypeConverter.ToTarget((EnvironmentalVariableType)999));
}
