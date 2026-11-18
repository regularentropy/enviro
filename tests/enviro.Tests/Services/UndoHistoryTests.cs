using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

public class UndoHistoryTests
{
    private static EnvModel Row(string name) =>
        new() { Name = name, OriginalName = name, Path = "v", OriginalPath = "v", State = EnvironmentalVariableState.Unchanged };

    private static UndoEntry Entry(string name, RowFields? before, RowFields? after)
    {
        var model = Row(name);
        return new UndoEntry([
            new ScopeDelta(EnvironmentalVariableType.User,
                [new RowDelta(model, 0, null, before, after)])
        ]);
    }

    [Fact]
    public void A_fresh_history_has_nothing_to_undo_or_redo()
    {
        var history = new UndoHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
    }

    [Fact]
    public void Push_enables_undo_and_undo_moves_the_swapped_entry_to_redo()
    {
        var history = new UndoHistory();
        var before = RowFields.Of(Row("a"));
        var after = before with { Path = "changed" };
        var entry = Entry("a", before, after);

        history.Push(entry);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        var popped = history.Undo();

        Assert.Same(entry, popped);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        var redone = history.Redo();

        Assert.NotNull(redone);
        var delta = Assert.Single(redone.Scopes).Rows;
        var row = Assert.Single(delta);
        Assert.Equal(after, row.Before);  // swapped: redo's before-side is the original after-state
        Assert.Equal(before, row.After);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void A_new_push_clears_the_redo_stack()
    {
        var history = new UndoHistory();
        var fields = RowFields.Of(Row("a"));

        history.Push(Entry("a", fields, fields));
        history.Undo();
        Assert.True(history.CanRedo);

        history.Push(Entry("b", fields, fields));

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_drops_both_stacks()
    {
        var history = new UndoHistory();
        var fields = RowFields.Of(Row("a"));

        history.Push(Entry("a", fields, fields));
        history.Undo();
        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void The_undo_stack_is_bounded_at_100_entries()
    {
        var history = new UndoHistory();
        var fields = RowFields.Of(Row("a"));

        for (var i = 0; i < 150; i++)
            history.Push(Entry($"v{i}", fields, fields));

        var undos = 0;
        while (history.Undo() is not null)
            undos++;

        Assert.Equal(100, undos); // the oldest 50 were dropped
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Nested_batches_merge_into_one_entry()
    {
        var history = new UndoHistory();
        var fields = RowFields.Of(Row("a"));

        using (history.BeginBatch())
        {
            history.Push(Entry("a", fields, fields));

            using (history.BeginBatch())
                history.Push(Entry("b", fields, fields));
        }

        Assert.True(history.CanUndo);

        var merged = history.Undo();

        Assert.NotNull(merged);
        var scope = Assert.Single(merged.Scopes);
        Assert.Equal(2, scope.Rows.Count);
        Assert.False(history.CanUndo); // one step, despite the nesting
    }

    [Fact]
    public void An_empty_batch_pushes_nothing()
    {
        var history = new UndoHistory();

        using (history.BeginBatch())
        {
        }

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void StackChanged_fires_on_every_stack_mutation()
    {
        var history = new UndoHistory();
        var fields = RowFields.Of(Row("a"));
        var fired = 0;
        history.StackChanged += (_, _) => fired++;

        history.Push(Entry("a", fields, fields));
        history.Undo();
        history.Redo();
        history.Clear();

        Assert.Equal(4, fired);
    }
}
