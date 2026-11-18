using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Undo/redo over the real <see cref="EnvService"/>. The service reads the process environment on
/// construction; on a test host both scopes simply start from whatever the host holds, and every
/// test only touches names it created itself.
/// </summary>
public class EnvServiceTests
{
    private static string UniqueName() => "ENVIRO_TEST_" + Guid.NewGuid().ToString("N");

    private static EnvService NewService(out UndoHistory history)
    {
        history = new UndoHistory();
        return new EnvService(history);
    }

    [Fact]
    public void GetScope_maps_types_and_throws_on_unknown()
    {
        var service = NewService(out _);

        Assert.Equal(EnvironmentalVariableType.User, service.GetScope(EnvironmentalVariableType.User).Type);
        Assert.Equal(EnvironmentalVariableType.Machine, service.GetScope(EnvironmentalVariableType.Machine).Type);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetScope((EnvironmentalVariableType)999));
    }

    [Fact]
    public void HasChanges_reflects_pending_rows_in_either_scope()
    {
        var service = NewService(out _);
        var name = UniqueName();
        Assert.False(service.HasChanges());

        service.User.Upsert(name, "1", overwrite: true);
        Assert.True(service.HasChanges());

        service.Undo();
        Assert.False(service.HasChanges());
    }

    [Fact]
    public void Undo_removes_an_added_row_and_redo_brings_it_back()
    {
        var service = NewService(out _);
        var name = UniqueName();

        service.User.Upsert(name, "1", overwrite: true);
        Assert.NotNull(service.User.GetByName(name));
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);

        service.Undo();
        Assert.Null(service.User.GetByName(name));
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);

        service.Redo();
        var row = service.User.GetByName(name);
        Assert.NotNull(row);
        Assert.Equal("1", row!.Path);
        Assert.Equal(EnvironmentalVariableState.Added, row.State);
    }

    [Fact]
    public void Undo_restores_the_previous_fields_of_a_modified_row()
    {
        var service = NewService(out _);
        var name = UniqueName();
        service.User.Upsert(name, "before", overwrite: true);
        service.Undo(); // drop the add, so the row is gone; redo it to get a persisted-looking baseline
        service.Redo();

        var row = service.User.GetByName(name)!;
        service.User.Update(row, "after");
        Assert.Equal("after", row.Path);

        service.Undo();
        Assert.Equal("before", row.Path);

        service.Redo();
        Assert.Equal("after", row.Path);
    }

    [Fact]
    public void Undo_of_a_delete_brings_the_row_back_at_its_position()
    {
        var service = NewService(out _);
        var name = UniqueName();
        service.User.Upsert(name, "1", overwrite: true);
        var row = service.User.GetByName(name)!;

        service.User.Delete(row); // an Added row leaves the list
        Assert.Null(service.User.GetByName(name));

        service.Undo();
        Assert.Same(row, service.User.GetByName(name));
        Assert.Equal(EnvironmentalVariableState.Added, row.State);
    }

    [Fact]
    public void A_new_action_after_undo_clears_redo()
    {
        var service = NewService(out _);

        service.User.Upsert(UniqueName(), "1", overwrite: true);
        service.Undo();
        Assert.True(service.CanRedo);

        service.User.Upsert(UniqueName(), "2", overwrite: true);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Undo_and_redo_on_a_fresh_service_are_noops()
    {
        var service = NewService(out _);

        service.Undo();
        service.Redo();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.False(service.HasChanges());
    }

    [Fact]
    public void Reload_clears_the_undo_history()
    {
        var service = NewService(out _);

        service.User.Upsert(UniqueName(), "1", overwrite: true);
        Assert.True(service.CanUndo);

        service.Reload();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.False(service.HasChanges());
    }
}
