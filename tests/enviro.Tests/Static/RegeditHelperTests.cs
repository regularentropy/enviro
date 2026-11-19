using enviro.Models;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class RegeditHelperTests
{
    [Fact]
    public void KeyPath_user_maps_to_HKCU_Environment()
    {
        Assert.Equal(@"HKEY_CURRENT_USER\Environment", RegeditHelper.KeyPath(EnvironmentalVariableType.User));
    }

    [Fact]
    public void KeyPath_machine_maps_to_the_SessionManager_key()
    {
        Assert.Equal(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment", RegeditHelper.KeyPath(EnvironmentalVariableType.Machine));
    }
}
