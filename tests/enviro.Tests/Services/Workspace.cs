using enviro.Models;
using enviro.Services;

namespace enviro.Tests.Services;

/// <summary>
/// Two scopes on one undo history, replayed through <see cref="EnvScope.ApplyDelta"/> exactly the
/// way the private <c>EnvService.ApplyEntry</c> does — the harness mirrors those three lines.
/// </summary>
internal sealed class Workspace
{
    public UndoHistory History { get; } = new();
    public EnvScope User { get; }
    public EnvScope Machine { get; }

    public Workspace()
    {
        User = new EnvScope(EnvironmentalVariableType.User, History);
        Machine = new EnvScope(EnvironmentalVariableType.Machine, History);
    }

    public void Undo()
    {
        if (History.Undo() is { } entry)
            Apply(entry, forward: false);
    }

    public void Redo()
    {
        if (History.Redo() is { } entry)
            Apply(entry, forward: true);
    }

    private void Apply(UndoEntry entry, bool forward)
    {
        foreach (var delta in entry.Scopes)
            (delta.Type == EnvironmentalVariableType.User ? User : Machine).ApplyDelta(delta, forward);
    }

    /// <summary>A row as the system would hand it out: baseline synced, Unchanged.</summary>
    public static EnvModel Row(string name, string value) =>
        new() { Name = name, OriginalName = name, Path = value, OriginalPath = value, State = EnvironmentalVariableState.Unchanged };

    public static List<string> Names(EnvScope scope) => [.. scope.Rows.Cast<EnvModel>().Select(r => r.Name)];
}
