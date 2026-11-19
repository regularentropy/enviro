using enviro.Models;
using enviro.Services;
using enviro.Static;
using Xunit;

namespace enviro.Tests.Static;

public class StateCounterTests
{
    private static EnvScope ScopeWith(params EnvModel[] rows)
    {
        var scope = new EnvScope(EnvironmentalVariableType.User, new UndoHistory());
        scope.Replace([.. rows]);
        return scope;
    }

    private static EnvModel Row(string name, EnvironmentalVariableState state, bool corrupted = false, bool drifted = false) =>
        new() { Name = name, OriginalName = name, Path = "v", OriginalPath = "v", State = state, IsCorrupted = corrupted, IsDrifted = drifted };

    [Fact]
    public void CountByState_counts_only_the_requested_state()
    {
        var scope = ScopeWith(
            Row("a", EnvironmentalVariableState.Unchanged),
            Row("b", EnvironmentalVariableState.Modified),
            Row("c", EnvironmentalVariableState.Modified),
            Row("d", EnvironmentalVariableState.Added),
            Row("e", EnvironmentalVariableState.Deleted));

        Assert.Equal(1, StateCounter.CountByState(scope, EnvironmentalVariableState.Unchanged));
        Assert.Equal(2, StateCounter.CountByState(scope, EnvironmentalVariableState.Modified));
        Assert.Equal(1, StateCounter.CountByState(scope, EnvironmentalVariableState.Added));
        Assert.Equal(1, StateCounter.CountByState(scope, EnvironmentalVariableState.Deleted));
    }

    [Fact]
    public void CountCorrupted_counts_the_overlay_only()
    {
        var scope = ScopeWith(
            Row("a", EnvironmentalVariableState.Unchanged, corrupted: true),
            Row("b", EnvironmentalVariableState.Modified), // state does not matter for the overlay
            Row("c", EnvironmentalVariableState.Unchanged, corrupted: true));

        Assert.Equal(2, StateCounter.CountCorrupted(scope));
    }

    [Fact]
    public void CountDrifted_counts_the_overlay_only()
    {
        var scope = ScopeWith(
            Row("a", EnvironmentalVariableState.Unchanged, drifted: true),
            Row("b", EnvironmentalVariableState.Modified), // state does not matter for the overlay
            Row("c", EnvironmentalVariableState.Unchanged, drifted: true));

        Assert.Equal(2, StateCounter.CountDrifted(scope));
    }
}
