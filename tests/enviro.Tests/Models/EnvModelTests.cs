using System.ComponentModel;
using enviro.Models;
using Xunit;

namespace enviro.Tests.Models;

public class EnvModelTests
{
    [Fact]
    public void PropertyChanged_fires_only_when_the_value_actually_changes()
    {
        var model = new EnvModel
        {
            Name = "A", OriginalName = "A", Path = "v", OriginalPath = "v",
            State = EnvironmentalVariableState.Unchanged,
        };

        var fired = new List<string>();
        model.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);

        model.Name = "A";   // same value: no event
        model.Name = "B";   // changed: event
        model.Path = "v";   // same: no event
        model.Path = "w";   // changed
        model.State = EnvironmentalVariableState.Unchanged; // same
        model.State = EnvironmentalVariableState.Modified;  // changed
        model.IsCorrupted = false; // same
        model.IsCorrupted = true;  // changed
        model.OriginalName = "A";  // same
        model.OriginalPath = "v";  // same

        Assert.Equal(["Name", "Path", "State", "IsCorrupted"], fired);
    }
}
