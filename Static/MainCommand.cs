using System.Windows.Forms;

namespace enviro.Static;

/// <summary>
/// One user command, defined once: its label, its shortcuts, its action and the one predicate
/// that decides whether it is available. It holds no WinForms control — the toolbar buttons and
/// the menu items are projections of the table, never its source.
/// </summary>
/// <param name="label">The command's name, used for the menu item that projects it.</param>
/// <param name="shortcuts">The key combinations that run it; the first is the one the menu shows.</param>
/// <param name="execute">What the command does.</param>
/// <param name="canExecute">Whether the command is available right now.</param>
internal sealed class MainCommand(string label, Keys[] shortcuts, Action execute, Func<bool> canExecute)
{
    /// <summary>
    /// The command's name, used for the menu item that projects it.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    /// The key combinations that run the command; the first is the one the menu shows.
    /// </summary>
    public Keys[] Shortcuts { get; } = shortcuts;

    /// <summary>
    /// What the command does.
    /// </summary>
    public Action Execute { get; } = execute;

    /// <summary>
    /// Whether the command is available right now: the single definition of its enablement.
    /// </summary>
    public bool IsEnabled => canExecute();
}

/// <summary>
/// The shell's command table: the toolbar, the menu and the shortcut router all go through it,
/// so a command has one action and one enablement rule wherever it is reached from.
/// </summary>
/// <param name="commands">The commands the shell exposes, in menu order.</param>
internal sealed class MainCommandTable(IEnumerable<MainCommand> commands)
{
    /// <summary>
    /// The commands the shell exposes, in menu order.
    /// </summary>
    public IReadOnlyList<MainCommand> Commands { get; } = [.. commands];

    /// <summary>
    /// Finds the command bound to a key combination.
    /// </summary>
    /// <param name="keyData">The pressed key combination.</param>
    /// <returns>The matching command, or null when no command uses that key.</returns>
    public MainCommand? Find(Keys keyData) => Commands.FirstOrDefault(c => c.Shortcuts.Contains(keyData));

    /// <summary>
    /// Runs the command bound to a key combination, if there is one and it is available.
    /// </summary>
    /// <param name="keyData">The pressed key combination.</param>
    /// <returns>True when a command ran, so the key must not be handled any other way.</returns>
    public bool TryExecute(Keys keyData)
    {
        if (Find(keyData) is not { IsEnabled: true } command)
            return false;

        command.Execute();
        return true;
    }
}
