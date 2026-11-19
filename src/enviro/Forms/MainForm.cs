using enviro.Factories;
using enviro.Forms;
using enviro.Models;
using enviro.Services;
using enviro.Static;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace enviro;

internal partial class MainForm : Form
{
    private readonly ITabFactory _tabFactory;
    private readonly IEnvGridFactory _gridFactory;
    private readonly IEnvService _envService;
    private readonly IActionFactory<EnvModel> _entryFormFactory;
    private readonly IUpdateService _updateService;
    private readonly IConfigService _configService;
    private readonly ICorruptedPathService _corruption;
    private readonly IAppLog _log;
    private readonly ApplyCoordinator _applyCoordinator;
    private readonly IVariableFileService _variableFiles;
    private readonly IRegFileService _regFiles;
    private readonly IUndoHistory _undoHistory;
    private readonly IDriftService _drift;

    // Builds the About dialog on demand, so the shell holds no container.
    private readonly Func<AboutForm> _aboutFormFactory;

    // Set when the process is deliberately torn down (elevation restart, discard, or
    // apply-then-close) so the close guard does not ask a second time.
    private bool _isRestarting;

    // Clears the transient message (applied changes, failed settings save) from the status strip.
    private readonly System.Windows.Forms.Timer _applyStatusTimer;

    // Re-checks the live registry periodically so external changes surface without a refresh.
    private readonly System.Windows.Forms.Timer _driftTimer;

    // Every command the shell exposes, defined once; the shortcuts dispatch through it.
    private readonly MainCommandTable _commands;

    // Each command's projected controls, refreshed together so their enablement cannot drift.
    private readonly List<(MainCommand Command, ToolStripItem[] Targets)> _commandTargets = [];

    // The command the Apply button mirrors, matched by reference in UpdateActionState, plus the
    // two fonts the pending-changes emphasis switches between; created once and disposed never.
    private MainCommand _applyCommand = null!;
    private Font? _applyButtonRegularFont;
    private Font? _applyButtonBoldFont;

    // The View → "Show drifted only" item, kept so the drifted status label can toggle it.
    private ToolStripMenuItem? _driftOnlyItem;

    public MainForm(IEnvService es, IActionFactory<EnvModel> af, IUpdateService us, ITabFactory tf, IEnvGridFactory gf, Func<AboutForm> aboutForm, IConfigService cs, IAppLog log, ICorruptedPathService corruption, ApplyCoordinator applyCoordinator, IVariableFileService variableFiles, IUndoHistory undoHistory, IRegFileService regFiles, IDriftService drift)
    {
        InitializeComponent();

        // A Form shows the WinForms default icon unless told otherwise; use the app's icon so the
        // title bar and taskbar match the executable's embedded icon.
        Icon = Properties.Resources.favicon;

        _tabFactory = tf;
        _gridFactory = gf;
        _updateService = us;
        _envService = es;
        _entryFormFactory = af;
        _aboutFormFactory = aboutForm;
        _configService = cs;
        _corruption = corruption;
        _log = log;
        _applyCoordinator = applyCoordinator;
        _variableFiles = variableFiles;
        _regFiles = regFiles;
        _undoHistory = undoHistory;
        _drift = drift;

        ApplyElevationWindowGeometry();

        _applyStatusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _applyStatusTimer.Tick += (_, _) =>
        {
            _applyStatusTimer.Stop();
            applyStatusLabel.Text = string.Empty;
        };

        // Drift has no toggle: the live registry is re-checked periodically so an external
        // change made while the app idles still shows up without a manual refresh.
        _driftTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _driftTimer.Tick += (_, _) =>
        {
            _drift.CheckAll();
            UpdateStatusStrip();
        };
        _driftTimer.Start();

        // A failed settings save is reported without a dialog: the app keeps running, just unsaved.
        _configService.SaveFailed += OnConfigSaveFailed;

        this.Name = Assembly.GetExecutingAssembly().GetName().Name;

        // Legend for the grid's state-indicator column, available without using colour vision.
        statusStrip1.ShowItemToolTips = true;
        pendingStatusLabel.ToolTipText = "Row markers: + added · ~ modified · – deleted · ! corrupted · ≠ drifted. Click the pending count to apply.";
        driftStatusLabel.ToolTipText = "Click to show drifted rows only.";

        // Elevation cannot change while the process runs, so the label is written once here.
        elevationStatusLabel.Text = AdminChecker.IsAdmin() ? "Administrator" : "Standard user";

        AllTab.Controls.AddRange([
            _tabFactory.Create(EnvironmentalVariableType.User),
            _tabFactory.Create(EnvironmentalVariableType.Machine, () => RestartAsAdmin(this, EventArgs.Empty)),
        ]);

        // Keep the toolbar's Add/Edit/Delete state in sync with the active tab, row and focus:
        // the !FocusIsTextBox() predicates must re-evaluate when the filter box gains or loses
        // focus, otherwise a menu click (which does not re-check IsEnabled) would fire mid-text-edit.
        AllTab.SelectedIndexChanged += (_, _) => UpdateActionState();

        foreach (var grid in AllTab.TabPages.Cast<TabPage>().SelectMany(t => t.Controls.OfType<DataGridView>()))
        {
            grid.SelectionChanged += (_, _) => UpdateActionState();

            if (_gridFactory.FilterOf(grid) is { } filter)
            {
                filter.GotFocus += (_, _) => UpdateActionState();
                filter.LostFocus += (_, _) => UpdateActionState();
            }
        }

        _envService.User.Rows.ListChanged += (_, _) => UpdateActionState();
        _envService.Machine.Rows.ListChanged += (_, _) => UpdateActionState();

        // The undo/redo stacks change after the rows do, so the command enablement needs this too.
        _undoHistory.StackChanged += OnUndoStackChanged;

        _commands = CreateCommandSurfaces();
        _applyCommand = _commands.Commands.First(c => c.Label == "Apply");
        UpdateActionState();
        RestorePersistedView();

        // The status strip is clickable where it names an action: the pending count applies,
        // the drifted count toggles the drifted-only view.
        pendingStatusLabel.Click += (_, _) =>
        {
            if (_envService.HasChanges())
                Apply(this, EventArgs.Empty);
        };
        driftStatusLabel.Click += (_, _) =>
        {
            if (_driftOnlyItem is { } item)
                item.Checked = !item.Checked;
        };

        optionsToolStripMenuItem.DropDownItems.Add(CreateThemeMenu());
    }

    /// <summary>
    /// Builds the Options → Theme radio menu from the persisted preference.
    /// </summary>
    /// <returns>The submenu listing the three preferences.</returns>
    private ToolStripMenuItem CreateThemeMenu()
    {
        var menu = new ToolStripMenuItem("Theme");

        foreach (var theme in Enum.GetValues<ThemePreference>())
        {
            var item = new ToolStripMenuItem(theme.ToString())
            {
                Checked = _configService.Config.Theme == theme,
                Tag = theme,
            };

            item.Click += ThemeItemClicked;
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// Builds the View menu's checkable corrupted-validation setting from the persisted preference.
    /// </summary>
    /// <remarks>
    /// The toggle is a setting, not a command: it has no shortcut, no toolbar button and no
    /// enablement predicate, so it stays out of the command table.
    /// </remarks>
    /// <returns>The checkable item, with its toggle handler attached.</returns>
    private ToolStripMenuItem CreateCorruptedValidationMenu()
    {
        var item = new ToolStripMenuItem("Check for corrupted variables")
        {
            CheckOnClick = true,
            CheckState = _configService.Config.EnableCorruptedValidation ? CheckState.Checked : CheckState.Unchecked,
            ToolTipText = "Checks whether a variable points to a path or file that doesn't exist."
        };

        item.CheckedChanged += CorruptedVarsButtonClicked;
        return item;
    }

    /// <summary>
    /// Stores a new theme choice and says when it applies; nothing is recoloured live, because the
    /// colour mode is fixed once a window exists.
    /// </summary>
    private void ThemeItemClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: ThemePreference theme } item
            || item.OwnerItem is not ToolStripMenuItem menu)
            return;

        // WinForms menu items have check marks, not radio buttons; writing every sibling makes the
        // three exclusive so the menu reads like the one-of-three choice it is.
        foreach (var sibling in menu.DropDownItems.OfType<ToolStripMenuItem>())
            sibling.Checked = ReferenceEquals(sibling, item);

        if (_configService.Config.Theme == theme)
            return;

        _configService.Config.Theme = theme;

        // Persist immediately like the corruption toggle, so the choice survives even a crash.
        _configService.Save();

        DialogHelper.ShowInfo(
            $"The {item.Text} theme will be used the next time Enviro starts.",
            "Theme");
    }

    /// <summary>
    /// Builds the View menu's "show drifted only" toggle for both grids.
    /// </summary>
    /// <remarks>
    /// The toggle is session-only, so unlike the corruption check it is never persisted.
    /// </remarks>
    /// <returns>The checkable item, with its toggle handler attached.</returns>
    private ToolStripMenuItem CreateDriftOnlyMenu()
    {
        var item = new ToolStripMenuItem("Show drifted only")
        {
            CheckOnClick = true,
            ToolTipText = "Shows only the rows whose value has changed in the registry since enviro last read it."
        };

        item.CheckedChanged += (_, _) =>
        {
            foreach (var grid in AllTab.TabPages.Cast<TabPage>().SelectMany(t => t.Controls.OfType<DataGridView>()))
                _gridFactory.SetDriftOnly(grid, item.Checked);
        };
        return item;
    }

    private void CorruptedVarsButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item)
            return;

        _configService.Config.EnableCorruptedValidation = item.Checked;

        // Persist immediately so the choice survives even a crash; the exit hook stays as-is.
        _configService.Save();

        // Enabling reviews both scopes shortly after; disabling cancels the pending work and
        // clears every mark.
        if (item.Checked)
            _corruption.RequestScanAll();
        else
            _corruption.ClearOverlay();

        UpdateStatusStrip();
    }

    private void AboutButtonClicked(object sender, EventArgs e)
    {
        var form = _aboutFormFactory();
        form.ShowDialog(this);
    }

    /// <summary>
    /// Closes the window through the normal close path, so unapplied changes are still confirmed.
    /// </summary>
    private void CloseApplication(object? sender, EventArgs e) => Close();

    public void RestartAsAdmin(object? sender, EventArgs e)
    {
        // Kept for this process tree only: the elevated instance reads it from its command line,
        // so nothing is persisted and a later launch (or a reboot) starts centred.
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var maximized = WindowState == FormWindowState.Maximized;

        ProcessStartInfo proc = new()
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Application.ExecutablePath,
            Verb = "runas",
            Arguments = $"{WindowGeometry.Argument} {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height},{(maximized ? 1 : 0)}"
        };
        try
        {
            Process.Start(proc);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC was cancelled: nothing was started, so keep this instance running.
            DialogHelper.ShowInfo(
                "Elevation was cancelled. The Machine tab stays read-only.",
                "Run as Administrator");
            return;
        }

        _isRestarting = true;
        Application.Exit();
    }

    /// <summary>
    /// Asks what to do with unapplied changes before the window closes.
    /// </summary>
    /// <remarks>
    /// The close attempt is always cancelled first, so the window only closes once the
    /// user's choice is known (and, for "Yes", only after the apply succeeds).
    /// </remarks>
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (e.Cancel || _isRestarting || !_envService.HasChanges())
            return;

        e.Cancel = true;

        switch (DialogHelper.ShowUnsavedChanges(_applyCoordinator.PendingChanges()))
        {
            case DialogResult.No:
                _isRestarting = true;
                Close();
                return;

            case DialogResult.Yes:
                break;

            default:
                return;
        }

        // ApplyAsync completes synchronously today, so this runs to the end before the
        // close is re-issued.
        var outcome = await _applyCoordinator.ApplyAsync();

        // A failed run leaves rows unwritten, so the window stays open.
        if (!outcome.Succeeded)
        {
            DialogHelper.ShowError(outcome.Message, "Apply");
            return;
        }

        _isRestarting = true;
        Close();
    }

    /// <summary>
    /// Applies the bounds passed by an elevation restart when they still fit one screen;
    /// every other launch centres the window.
    /// </summary>
    /// <remarks>
    /// The values live only in this process's command line, so they cannot survive a reboot or
    /// leak into a later ordinary launch.
    /// </remarks>
    private void ApplyElevationWindowGeometry()
    {
        var workingAreas = Screen.AllScreens.Select(screen => screen.WorkingArea);

        if (WindowGeometry.TryParse(Environment.GetCommandLineArgs(), out var bounds, out var maximized)
            && WindowGeometry.IsOnScreen(bounds, workingAreas))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;

            if (maximized)
                WindowState = FormWindowState.Maximized;

            return;
        }

        // No restart argument: fall back to the bounds persisted on the last close when they
        // still fit entirely inside one screen, so a moved or resized window survives a restart.
        var saved = _configService.Config.WindowBounds;

        if (saved is not null
            && WindowGeometry.IsOnScreen(new Rectangle(saved.X, saved.Y, saved.Width, saved.Height), workingAreas))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);

            if (_configService.Config.WindowMaximized)
                WindowState = FormWindowState.Maximized;

            return;
        }

        CenterToScreen();
    }

    /// <summary>
    /// Applies the persisted per-scope view state once the tabs exist: the active tab, each
    /// grid's filter text and each grid's sort.
    /// </summary>
    private void RestorePersistedView()
    {
        AllTab.SelectedIndex = Math.Clamp(_configService.Config.ActiveTab, 0, AllTab.TabPages.Count - 1);

        foreach (var tab in AllTab.TabPages.Cast<TabPage>())
        {
            if (ControlHelper.GetTabType(tab) is not { } type
                || tab.Controls.OfType<DataGridView>().FirstOrDefault() is not { } grid)
                continue;

            if (_gridFactory.FilterOf(grid) is { } filter)
                filter.Text = type == EnvironmentalVariableType.User
                    ? _configService.Config.UserFilter
                    : _configService.Config.MachineFilter;

            var sort = type == EnvironmentalVariableType.User
                ? _configService.Config.UserSort
                : _configService.Config.MachineSort;

            if (sort is not null)
                _gridFactory.RestoreSort(grid, sort.Property, sort.Descending);
        }

        UpdateActionState();
    }

    /// <summary>
    /// Writes the window bounds, active tab, filter texts and sorts into the config; the exit
    /// hook saves the dirty config when the application ends.
    /// </summary>
    private void PersistViewState()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

        _configService.Config.WindowBounds = new WindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _configService.Config.WindowMaximized = WindowState == FormWindowState.Maximized;
        _configService.Config.ActiveTab = AllTab.SelectedIndex;

        foreach (var tab in AllTab.TabPages.Cast<TabPage>())
        {
            if (ControlHelper.GetTabType(tab) is not { } type
                || tab.Controls.OfType<DataGridView>().FirstOrDefault() is not { } grid)
                continue;

            var filter = _gridFactory.FilterOf(grid)?.Text.Trim() ?? string.Empty;

            if (type == EnvironmentalVariableType.User)
                _configService.Config.UserFilter = filter;
            else
                _configService.Config.MachineFilter = filter;

            var sorted = grid.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => c.HeaderCell.SortGlyphDirection != SortOrder.None);

            var sort = sorted is { DataPropertyName: { Length: > 0 } property }
                ? new SortPreference(property, sorted.HeaderCell.SortGlyphDirection == SortOrder.Descending)
                : null;

            if (type == EnvironmentalVariableType.User)
                _configService.Config.UserSort = sort;
            else
                _configService.Config.MachineSort = sort;
        }
    }

    /// <summary>
    /// Re-reads both scopes from the system, discarding pending edits only after confirmation.
    /// </summary>
    private void RefreshEntryClicked(object? sender, EventArgs e)
    {
        if (_envService.HasChanges())
        {
            var pending = _applyCoordinator.PendingChanges();

            var result = DialogHelper.ShowConfirm(
                $"{pending} {(pending == 1 ? "change has" : "changes have")} not been applied.\n\n" +
                "Refreshing discards them. Continue?",
                "Refresh");

            if (result != DialogResult.OK)
                return;
        }

        // Rows are rebuilt from the OS; the filter text stays and re-applies through the list
        // change, only the transient apply message is cleared.
        applyStatusLabel.Text = string.Empty;
        _applyStatusTimer.Stop();

        _envService.Reload();

        // The reloaded models start clean; review both scopes again and refresh the status strip.
        _corruption.RequestScanAll();
        _drift.CheckAll();
        UpdateStatusStrip();
        UpdateActionState();
    }

    private async void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        // Disabled while the check is in flight, so a second one cannot start.
        checkForUpdatesToolStripMenuItem.Enabled = false;
        try
        {
            var response = await _updateService.CheckForUpdatesAsync();
            if (response is null)
            {
                DialogHelper.ShowMessage("No updates found", "Updater");
                return;
            }
            new UpdateForm(response, _log).ShowDialog();
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            DialogHelper.ShowInfo("Cannot access the internet", "Updater");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            DialogHelper.ShowInfo("The repository is unavailable.\nCheck the owners webpage.", "Updater");
        }
        catch (OperationCanceledException ex)
        {
            // HttpClient timeout: the host never answered.
            _log.Error("Update check timed out.", ex);
            DialogHelper.ShowInfo("Cannot access the internet", "Updater");
        }
        finally
        {
            checkForUpdatesToolStripMenuItem.Enabled = true;
        }
    }

    /// <summary>
    /// The grid of the currently selected tab.
    /// </summary>
    private DataGridView? GetActiveGrid() =>
        AllTab.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();

    /// <summary>
    /// The one place command enablement is recomputed: every toolbar button and menu item takes
    /// its state from its command's predicate, so the two surfaces cannot drift.
    /// </summary>
    private void UpdateActionState()
    {
        foreach (var (command, targets) in _commandTargets)
        {
            var enabled = command.IsEnabled;

            foreach (var target in targets)
                target.Enabled = enabled;

            // The Apply button carries the pending-changes emphasis: bold while there is something
            // to apply, regular once the workspace is clean. Matched by reference so a different
            // command that happens to spell "Apply" in another language never takes the emphasis.
            if (ReferenceEquals(command, _applyCommand))
            {
                _applyButtonRegularFont ??= applyToolStripButton.Font;
                _applyButtonBoldFont ??= new Font(_applyButtonRegularFont, _applyButtonRegularFont.Style | FontStyle.Bold);

                applyToolStripButton.Font = enabled ? _applyButtonBoldFont : _applyButtonRegularFont;
            }
        }

        UpdateStatusStrip();
    }

    /// <summary>
    /// Whether the active tab's scope accepts mutations in this session.
    /// </summary>
    private bool CanMutate() => GetActiveGrid() is { } grid && AdminChecker.CanMutate(grid);

    /// <summary>
    /// Whether the active tab's grid has a selected row.
    /// </summary>
    private bool HasSelection() => GetActiveGrid() is { } grid && ControlHelper.GetCurrentModel(grid) is not null;

    /// <summary>
    /// Whether exactly one row is selected; single-row actions like Edit need this.
    /// </summary>
    private bool SingleSelection() =>
        GetActiveGrid() is { } grid && ControlHelper.GetSelectedModels(grid).Count == 1;

    /// <summary>
    /// Selects every visible row of the active grid. Rows hidden by the filter stay unselected,
    /// so a bulk action (Delete, Copy) never touches rows the user cannot see.
    /// </summary>
    private void SelectAllRows()
    {
        if (GetActiveGrid() is not { } grid)
            return;

        foreach (DataGridViewRow row in grid.Rows)
            row.Selected = row.Visible;

        if (grid.CurrentCell is null)
        {
            var firstVisible = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible);

            if (firstVisible is not null)
                grid.CurrentCell = firstVisible.Cells[0];
        }
    }

    /// <summary>
    /// Copies the grid's selection to the clipboard and says so in the status strip. The format
    /// comes from <see cref="ClipboardHelper"/>, so the context menu copies the same text.
    /// </summary>
    /// <param name="format">Formats the selected rows.</param>
    /// <param name="what">The property's name for the status message.</param>
    private void CopySelection(Func<IReadOnlyList<EnvModel>, string> format, string what)
    {
        if (GetActiveGrid() is not { } grid)
            return;

        var models = ControlHelper.GetSelectedModels(grid);

        if (models.Count == 0)
            return;

        ClipboardHelper.CopyText(format(models));

        ShowTransientStatus(models.Count == 1
            ? $"Copied {what} of \"{models[0].Name}\"."
            : $"Copied {models.Count} variables.");
    }

    /// <summary>
    /// Whether the keyboard focus sits in a text box, where Delete and F2 must keep their normal
    /// text-editing meaning instead of acting on the selected row.
    /// </summary>
    /// <remarks>
    /// A container control's active control is the focused control itself, even when it is nested in
    /// a tab page, so the form's ActiveControl is the filter box while the user types in it.
    /// </remarks>
    private bool FocusIsTextBox() => ActiveControl is TextBox;

    /// <summary>
    /// The filter box of the active tab, from the factory that owns the grid→filter mapping.
    /// </summary>
    private TextBox? ActiveFilter() => GetActiveGrid() is { } grid ? _gridFactory.FilterOf(grid) : null;

    /// <summary>
    /// Focuses and selects the active tab's filter box.
    /// </summary>
    private void FocusFilter()
    {
        if (ActiveFilter() is not { } filter)
            return;

        filter.Focus();
        filter.SelectAll();
    }

    /// <summary>
    /// Defines every command once and projects it onto the toolbar buttons and the Variables and
    /// View menus.
    /// </summary>
    /// <returns>The table the shortcut router dispatches through.</returns>
    private MainCommandTable CreateCommandSurfaces()
    {
        var exportVariables = new MainCommand("Export variables…", [], ExportVariables, () => true);
        var importVariables = new MainCommand("Import variables…", [], ImportVariables, () => true);
        // Menu-only and self-disabling when the process is already elevated, so the item cannot
        // restart an administrator session into the same state.
        var restartAsAdmin = new MainCommand("Restart as administrator", [],
            () => RestartAsAdmin(this, EventArgs.Empty),
            () => !AdminChecker.CanMutate(EnvironmentalVariableType.Machine));
        var add = new MainCommand("Add", [Keys.Control | Keys.N], () => CreateEntryClicked(this, EventArgs.Empty), CanMutate);
        // Edit acts on exactly one row, so it is unavailable in a multi-selection; Delete and
        // Copy stay available and act on all selected rows.
        var edit = new MainCommand("Edit", [Keys.F2, Keys.Control | Keys.E], () => EditEntryClicked(this, EventArgs.Empty), () => CanMutate() && SingleSelection() && !FocusIsTextBox());
        var delete = new MainCommand("Delete", [Keys.Delete], () => DeleteEntryClicked(this, EventArgs.Empty), () => CanMutate() && HasSelection() && !FocusIsTextBox());
        // Copy rides the same focus guard as Delete: in the filter box Ctrl+C keeps its native
        // text-copy meaning because the projected menu item is disabled there.
        var copyValue = new MainCommand("Copy value", [Keys.Control | Keys.C],
            () => CopySelection(ClipboardHelper.FormatValueSelection, "value"),
            () => HasSelection() && !FocusIsTextBox());
        var copyName = new MainCommand("Copy name", [Keys.Control | Keys.Shift | Keys.C],
            () => CopySelection(ClipboardHelper.FormatNameSelection, "name"),
            () => HasSelection() && !FocusIsTextBox());
        var refresh = new MainCommand("Refresh", [Keys.F5], () => RefreshEntryClicked(this, EventArgs.Empty), () => true);
        var apply = new MainCommand("Apply", [Keys.Control | Keys.S], () => Apply(this, EventArgs.Empty), () => _envService.HasChanges());
        var undo = new MainCommand("Undo", [Keys.Control | Keys.Z], () => _envService.Undo(), () => _envService.CanUndo && !FocusIsTextBox());
        var redo = new MainCommand("Redo", [Keys.Control | Keys.Y], () => _envService.Redo(), () => _envService.CanRedo && !FocusIsTextBox());
        // Ctrl+A selects every visible row; in the filter box it keeps its native select-all-text
        // meaning via the same focus guard as the other commands.
        var selectAll = new MainCommand("Select all", [Keys.Control | Keys.A], SelectAllRows, () => GetActiveGrid() is { } g && g.Rows.Count > 0 && !FocusIsTextBox());
        var focusFilter = new MainCommand("Focus filter", [Keys.Control | Keys.F], FocusFilter, () => ActiveFilter() is not null);
        var acceptAllDrift = new MainCommand("Accept all live values", [], AcceptAllDrift, () => DriftedCount() > 0);
        var revertAllDrift = new MainCommand("Revert all drifted", [], RevertAllDrift, () => DriftedCount() > 0);

        // Menu-only commands: no toolbar button and no shortcut key, so the command surface stays
        // six buttons wide and the shortcut router is untouched.
        Project(exportVariables, null, fileToolStripMenuItem, 0);
        Project(importVariables, null, fileToolStripMenuItem, 1);
        fileToolStripMenuItem.DropDownItems.Insert(2, new ToolStripSeparator());
        Project(restartAsAdmin, null, fileToolStripMenuItem, 3);

        // Both menus are inserted after File and ahead of the designer's Options and Help menus.
        var variablesMenu = new ToolStripMenuItem("Variables");
        menuStrip1.Items.Insert(1, variablesMenu);

        Project(add, addToolStripButton, variablesMenu);
        Project(edit, editToolStripButton, variablesMenu);
        Project(delete, deleteToolStripButton, variablesMenu);
        variablesMenu.DropDownItems.Add(new ToolStripSeparator());
        Project(copyValue, null, variablesMenu);
        Project(copyName, null, variablesMenu);
        Project(apply, applyToolStripButton, variablesMenu);

        // Menu-only: undo/redo are workspace commands, not row actions, so the text-only toolbar
        // keeps its five buttons and the shortcuts ride the command table alone.
        Project(undo, null, variablesMenu);
        Project(redo, null, variablesMenu);
        variablesMenu.DropDownItems.Add(new ToolStripSeparator());
        Project(selectAll, null, variablesMenu);

        var viewMenu = new ToolStripMenuItem("View");
        menuStrip1.Items.Insert(2, viewMenu);

        Project(refresh, refreshToolStripButton, viewMenu);
        Project(focusFilter, null, viewMenu);
        viewMenu.DropDownItems.Add(CreateCorruptedValidationMenu());
        _driftOnlyItem = CreateDriftOnlyMenu();
        viewMenu.DropDownItems.Add(_driftOnlyItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        Project(acceptAllDrift, null, viewMenu);
        Project(revertAllDrift, null, viewMenu);

        return new MainCommandTable([exportVariables, importVariables, restartAsAdmin, add, edit, delete, copyValue, copyName, refresh, apply, focusFilter, acceptAllDrift, revertAllDrift, undo, redo, selectAll]);
    }

    /// <summary>
    /// Shows one command in a menu and mirrors its enablement onto the toolbar button that already
    /// exists for it.
    /// </summary>
    /// <param name="command">The command to project.</param>
    /// <param name="button">The toolbar button for the command, or null when the toolbar has none.</param>
    /// <param name="menu">The menu the command is listed in.</param>
    /// <param name="index">Where to insert it, or null to append.</param>
    private void Project(MainCommand command, ToolStripButton? button, ToolStripMenuItem menu, int? index = null)
    {
        var item = new ToolStripMenuItem(command.Label);

        // A menu-only command has no shortcut to show.
        if (command.Shortcuts.Length > 0)
            item.ShortcutKeys = command.Shortcuts[0];

        item.Click += (_, _) => command.Execute();

        if (index is { } position)
            menu.DropDownItems.Insert(position, item);
        else
            menu.DropDownItems.Add(item);

        _commandTargets.Add((command, button is null ? [item] : [button, item]));
    }

    /// <summary>
    /// Writes the summarised workspace state onto the status-strip labels.
    /// </summary>
    private void UpdateStatusStrip()
    {
        var status = StatusPresenter.Describe(
            _envService, _configService.Config.EnableCorruptedValidation);

        pendingStatusLabel.Text = status.Pending;

        // The drifted count occupies the strip only while there is drift to look at; clicking
        // it toggles the drifted-only view.
        driftStatusLabel.Text = status.Drifted;
        driftStatusLabel.Visible = status.DriftedCount > 0;

        if (status.ShowCorrupted)
        {
            corruptedStatusLabel.Text = status.Corrupted;
            corruptedStatusLabel.Visible = true;
        }
        else
        {
            corruptedStatusLabel.Visible = false;
        }
    }

    private void CreateEntryClicked(object? sender, EventArgs e)
    {
        if (GetActiveGrid() is { } grid)
            RowActionHelper.Create(grid, _entryFormFactory);
    }

    /// <summary>
    /// Writes the variables the user keeps to a file they choose — JSON or .reg, picked by the
    /// dialog's filter: the file matches the grid, and a row whose deletion is still pending is
    /// not backed up.
    /// </summary>
    private void ExportVariables()
    {
        var dialog = new CommonSaveFileDialog
        {
            // No default extension: with none set, the dialog appends the selected filter's own
            // extension, so the name the user picks carries the format they chose.
            DefaultFileName = $"enviro-variables-{DateTime.Now:yyyyMMdd}",
        };
        dialog.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
        dialog.Filters.Add(new CommonFileDialogFilter("Registration files", "*.reg"));

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            return;

        // The selected filter decides the format (the file may not exist yet, so its extension
        // alone cannot be trusted); the name is forced to carry that format's extension.
        var reg = dialog.SelectedFileTypeIndex == 2;
        IVariableFileService files = reg ? _regFiles : _variableFiles;
        var path = dialog.FileName;

        if (reg && !path.EndsWith(".reg", StringComparison.OrdinalIgnoreCase))
            path += ".reg";

        if (!reg && Path.GetExtension(path).Length == 0)
            path += ".json";

        var entries = files.Entries(files.Snapshot(_envService));

        if (entries.Count == 0)
        {
            DialogHelper.ShowInfo("There are no variables to export.", "Export variables");
            return;
        }

        var review = new VariableFileForm("Export variables", VariableFileDirection.Export, entries);

        if (review.ShowDialog(this) != DialogResult.OK)
            return;

        if (files.Save(path, BuildFile(review.SelectedEntries)) is { } error)
        {
            DialogHelper.ShowError(error, "Export variables");
            return;
        }

        ShowTransientStatus(
            $"Exported {review.SelectedEntries.Count} " +
            $"{(review.SelectedEntries.Count == 1 ? "variable" : "variables")} to {Path.GetFileName(path)}");
    }

    /// <summary>
    /// Merges the entry selection of a chosen file into the workspace: user entries always, machine
    /// entries only when this session may write them. The rows are reviewed and applied like any
    /// other edit; Apply writes the registry.
    /// </summary>
    private void ImportVariables()
    {
        var dialog = new CommonOpenFileDialog { Multiselect = false };
        dialog.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
        dialog.Filters.Add(new CommonFileDialogFilter("Registration files", "*.reg"));

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            return;

        // The file's extension decides the reader, so a .reg picked under the JSON filter still parses as .reg.
        IVariableFileService files = Path.GetExtension(dialog.FileName).Equals(".reg", StringComparison.OrdinalIgnoreCase)
            ? _regFiles
            : _variableFiles;

        var (file, error) = files.Read(dialog.FileName);

        if (file is null)
        {
            DialogHelper.ShowError(error ?? $"The variables could not be read from:\n{dialog.FileName}", "Import variables");
            return;
        }

        // The same gate Apply uses: a machine section is only selectable in an elevated session.
        var canImportMachine = AdminChecker.CanMutate(EnvironmentalVariableType.Machine);
        var entries = files.Compare(file, _envService, canImportMachine);

        if (entries.Count == 0)
        {
            DialogHelper.ShowInfo("The file contains no variables.", "Import variables");
            return;
        }

        var review = new VariableFileForm("Import variables", VariableFileDirection.Import, entries);

        if (review.ShowDialog(this) != DialogResult.OK)
            return;

        if (review.SelectedEntries.Count == 0)
        {
            ShowTransientStatus("No variables were selected; nothing was imported.");
            return;
        }

        // One import is one undo step, even when it merges into both scopes.
        using var batch = _undoHistory.BeginBatch();

        var user = ImportSelected(EnvironmentalVariableType.User, review.SelectedEntries);
        var machine = ImportSelected(EnvironmentalVariableType.Machine, review.SelectedEntries);

        // The window disables a machine entry without elevation, so the file's section is still
        // reported as skipped even though nothing was handed to the machine scope.
        var machineSkipped = file.MachineEntries().Count > 0 && !canImportMachine;

        // Each import raised its one list refresh, so the grids, the status strip and the
        // corruption overlay are already up to date.
        ShowTransientStatus(DescribeImport(user, machine, machineSkipped));
    }

    /// <summary>
    /// Builds the document from the entries the review window kept, leaving a scope out entirely
    /// when none of its entries were selected.
    /// </summary>
    /// <param name="entries">The checked entries.</param>
    /// <returns>The document to write.</returns>
    private static VariableFile BuildFile(IReadOnlyList<VariableFileEntry> entries) =>
        new()
        {
            Version = VariableFile.CurrentVersion,
            User = Section(entries, EnvironmentalVariableType.User),
            Machine = Section(entries, EnvironmentalVariableType.Machine),
        };

    /// <summary>
    /// Turns one scope's checked entries into a name-keyed section.
    /// </summary>
    /// <param name="entries">The checked entries.</param>
    /// <param name="scope">The scope to collect.</param>
    /// <returns>The section, or null when no entry of that scope was selected.</returns>
    private static Dictionary<string, string>? Section(
        IReadOnlyList<VariableFileEntry> entries, EnvironmentalVariableType scope)
    {
        var selected = entries.Where(entry => entry.Scope == scope).ToList();

        return selected.Count == 0
            ? null
            : selected.ToDictionary(entry => entry.Name, entry => entry.Value);
    }

    /// <summary>
    /// Merges one scope's selected entries; an empty selection does not touch the scope.
    /// </summary>
    /// <param name="scope">The scope to merge into.</param>
    /// <param name="entries">The checked entries.</param>
    /// <returns>What the merge did, or zero counts when no entry belonged to the scope.</returns>
    private ImportOutcome ImportSelected(
        EnvironmentalVariableType scope, IReadOnlyList<VariableFileEntry> entries)
    {
        var selected = entries
            .Where(entry => entry.Scope == scope)
            .Select(entry => new KeyValuePair<string, string>(entry.Name, entry.Value))
            .ToList();

        return selected.Count == 0
            ? new ImportOutcome(0, 0, 0, 0)
            : _envService.GetScope(scope).Import(selected);
    }

    /// <summary>
    /// Phrases one import for the transient status message, keeping it short enough for the strip.
    /// </summary>
    /// <param name="user">What the user scope's import did.</param>
    /// <param name="machine">What the machine scope's import did (all skipped when it was gated).</param>
    /// <param name="machineSkipped">Whether the machine section was skipped for lack of elevation.</param>
    /// <returns>The message to show.</returns>
    private static string DescribeImport(ImportOutcome user, ImportOutcome machine, bool machineSkipped)
    {
        var added = user.Added + machine.Added;
        var overwritten = user.Overwritten + machine.Overwritten;
        var unchanged = user.Unchanged + machine.Unchanged;
        var skipped = user.Skipped + machine.Skipped;

        var message = $"Imported {added + overwritten} {(added + overwritten == 1 ? "variable" : "variables")}: " +
                      $"{added} added, {overwritten} updated, {unchanged} unchanged";

        if (skipped > 0)
            message += $", {skipped} skipped";

        return machineSkipped
            ? message + ". Machine section skipped — run as administrator."
            : message + ".";
    }

    private void EditEntryClicked(object? sender, EventArgs e)
    {
        if (GetActiveGrid() is { } grid)
            RowActionHelper.Edit(grid, _entryFormFactory);
    }

    private void DeleteEntryClicked(object? sender, EventArgs e)
    {
        if (GetActiveGrid() is { } grid)
            RowActionHelper.Delete(grid, _envService, _undoHistory);
    }

    /// <summary>
    /// Runs the command bound to the pressed key; a key no command owns, or one whose command is
    /// unavailable, keeps its default handling.
    /// </summary>
    /// <remarks>
    /// A modal dialog runs its own message loop, so its keys never reach this form.
    /// </remarks>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        _commands.TryExecute(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private async void Apply(object sender, EventArgs e)
    {
        // Defensive no-op: the entry is disabled whenever there is nothing to apply.
        if (!_envService.HasChanges())
            return;

        var outcome = await _applyCoordinator.ApplyAsync();

        if (!outcome.Succeeded)
        {
            DialogHelper.ShowError(outcome.Message, "Apply");
            return;
        }

        // A successful apply resets the workspace; start from the full list again.
        _gridFactory.ClearFilters();

        // The written rows now match the registry, so the drift overlay is stale until re-checked.
        _drift.CheckAll();
        UpdateStatusStrip();

        ShowTransientStatus(outcome.Message);
    }

    private void OnConfigSaveFailed(object? sender, string message) => ShowTransientStatus(message);

    /// <summary>
    /// The number of drifted rows across both scopes; drives the bulk drift commands.
    /// </summary>
    private int DriftedCount() =>
        StateCounter.CountDrifted(_envService.User) + StateCounter.CountDrifted(_envService.Machine);

    /// <summary>
    /// Takes the live registry value of every drifted row into the workspace as its new
    /// baseline, in one undo step.
    /// </summary>
    private void AcceptAllDrift()
    {
        using var batch = _undoHistory.BeginBatch();
        var count = 0;

        foreach (var scope in new[] { _envService.User, _envService.Machine })
        {
            foreach (var row in scope.Rows.OfType<EnvModel>().Where(m => m.IsDrifted).ToList())
            {
                scope.AcceptDrift(row);
                count++;
            }
        }

        UpdateActionState();
        UpdateStatusStrip();
        ShowTransientStatus(count == 1
            ? "Accepted the live value of 1 variable."
            : $"Accepted the live values of {count} variables.");
    }

    /// <summary>
    /// Writes every drifted row's baseline back to the registry. Rows in a scope this session
    /// must not mutate are refused individually and reported in the summary.
    /// </summary>
    private void RevertAllDrift()
    {
        var reverted = 0;
        var failed = 0;

        foreach (var scope in new[] { _envService.User, _envService.Machine })
        {
            foreach (var row in scope.Rows.OfType<EnvModel>().Where(m => m.IsDrifted).ToList())
            {
                if (_drift.Revert(row))
                    reverted++;
                else
                    failed++;
            }
        }

        // The registry changed under the rows, so the drift overlay needs a fresh look.
        _drift.CheckAll();
        UpdateActionState();
        UpdateStatusStrip();

        if (failed > 0)
            DialogHelper.ShowWarning(
                $"Reverted {reverted}; {failed} could not be written (the Machine scope needs administrator).",
                "Revert all drifted");
        else
            ShowTransientStatus(reverted == 1
                ? "Reverted 1 drifted variable."
                : $"Reverted {reverted} drifted variables.");
    }

    /// <summary>
    /// Refreshes the command enablement when the undo/redo stacks change.
    /// </summary>
    private void OnUndoStackChanged(object? sender, EventArgs e) => UpdateActionState();

    /// <summary>
    /// Shows a message in the status strip for a few seconds instead of a modal dialog.
    /// </summary>
    /// <param name="message">The message to show.</param>
    private void ShowTransientStatus(string message)
    {
        applyStatusLabel.Text = message;
        _applyStatusTimer.Stop();
        _applyStatusTimer.Start();
    }

    /// <summary>
    /// Requests the first corrupted-path scan once the window is up.
    /// </summary>
    /// <remarks>
    /// The grid asks for a scan while it is created, which happens in the constructor, before
    /// <c>Application.Run</c> pumps messages — that request can be lost, leaving the overlay empty
    /// until something else asks again. This one is issued on the UI thread inside the running loop.
    /// </remarks>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        _corruption.RequestScanAll();

        // First drift check once the window is up, so the label shows a real count.
        _drift.CheckAll();
        UpdateStatusStrip();
    }

    /// <summary>
    /// Stops the transient-status timer when the window goes away.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        PersistViewState();

        // The exit hook still saves after the window is gone; it must not touch a dead form.
        _configService.SaveFailed -= OnConfigSaveFailed;
        _undoHistory.StackChanged -= OnUndoStackChanged;

        // No scan may outlive the window: pending work is dropped and in-flight verdicts discarded.
        _corruption.Cancel();

        _applyStatusTimer.Stop();
        _driftTimer.Stop();
        base.OnFormClosed(e);
    }
}
