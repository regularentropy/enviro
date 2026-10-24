using enviro.Factories;
using enviro.Forms;
using enviro.Models;
using enviro.Services;
using enviro.Static;
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

    // Builds the About dialog on demand, so the shell holds no container.
    private readonly Func<AboutForm> _aboutFormFactory;

    // Set when the process is deliberately torn down (elevation restart, discard, or
    // apply-then-close) so the close guard does not ask a second time.
    private bool _isRestarting;

    // Clears the transient message (applied changes, failed settings save) from the status strip.
    private readonly System.Windows.Forms.Timer _applyStatusTimer;

    // Every command the shell exposes, defined once; the shortcuts dispatch through it.
    private readonly MainCommandTable _commands;

    // Each command's projected controls, refreshed together so their enablement cannot drift.
    private readonly List<(MainCommand Command, ToolStripItem[] Targets)> _commandTargets = [];

    public MainForm(IEnvService es, IActionFactory<EnvModel> af, IUpdateService us, ITabFactory tf, IEnvGridFactory gf, Func<AboutForm> aboutForm, IConfigService cs, IAppLog log, ICorruptedPathService corruption, ApplyCoordinator applyCoordinator)
    {
        InitializeComponent();

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

        ApplyElevationWindowGeometry();

        _applyStatusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _applyStatusTimer.Tick += (_, _) =>
        {
            _applyStatusTimer.Stop();
            applyStatusLabel.Text = string.Empty;
        };

        // A failed settings save is reported without a dialog: the app keeps running, just unsaved.
        _configService.SaveFailed += OnConfigSaveFailed;

        this.Name = Assembly.GetExecutingAssembly().GetName().Name;

        // Legend for the grid's state-indicator column, available without using colour vision.
        statusStrip1.ShowItemToolTips = true;
        pendingStatusLabel.ToolTipText = "Row markers: + added · ~ modified · – deleted · ! corrupted value";

        // Elevation cannot change while the process runs, so the label is written once here.
        elevationStatusLabel.Text = AdminChecker.IsAdmin() ? "Administrator" : "Standard user";

        AllTab.Controls.AddRange([
            _tabFactory.Create(EnvironmentalVariableType.User),
            _tabFactory.Create(EnvironmentalVariableType.Machine, () => RestartAsAdmin(this, EventArgs.Empty)),
        ]);

        // Keep the toolbar's Add/Edit/Delete state in sync with the active tab and row.
        AllTab.SelectedIndexChanged += (_, _) => UpdateActionState();

        foreach (var grid in AllTab.TabPages.Cast<TabPage>().SelectMany(t => t.Controls.OfType<DataGridView>()))
            grid.SelectionChanged += (_, _) => UpdateActionState();

        _envService.User.Rows.ListChanged += (_, _) => UpdateActionState();
        _envService.Machine.Rows.ListChanged += (_, _) => UpdateActionState();

        _commands = CreateCommandSurfaces();
        UpdateActionState();

        var corruptedVarsButton = new ToolStripMenuItem("Check for corrupted variables")
        {
            CheckOnClick = true,
            CheckState = _configService.Config.EnableCorruptedValidation ? CheckState.Checked : CheckState.Unchecked,
            ToolTipText = "Checks whether a variable points to a path or file that doesn't exist."
        };

        corruptedVarsButton.CheckedChanged += CorruptedVarsButtonClicked;
        optionsToolStripMenuItem.DropDownItems.Add(corruptedVarsButton);
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
        form.ShowDialog();
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
        if (!WindowGeometry.TryParse(Environment.GetCommandLineArgs(), out var bounds, out var maximized)
            || !WindowGeometry.IsOnScreen(bounds, Screen.AllScreens.Select(screen => screen.WorkingArea)))
        {
            CenterToScreen();
            return;
        }

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;

        if (maximized)
            WindowState = FormWindowState.Maximized;
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

        // Rows are rebuilt from the OS; drop filters and the transient apply message so the view starts clean.
        _gridFactory.ClearFilters();
        applyStatusLabel.Text = string.Empty;
        _applyStatusTimer.Stop();

        _envService.Reload();

        // The reloaded models start clean; review both scopes again and refresh the status strip.
        _corruption.RequestScanAll();
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
            new UpdateForm(response).ShowDialog();
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
    /// Defines every command once and projects it onto the toolbar buttons and a new Edit menu.
    /// </summary>
    /// <returns>The table the shortcut router dispatches through.</returns>
    private MainCommandTable CreateCommandSurfaces()
    {
        var add = new MainCommand("Add", [Keys.Control | Keys.N], () => CreateEntryClicked(this, EventArgs.Empty), CanMutate);
        var edit = new MainCommand("Edit", [Keys.F2, Keys.Control | Keys.E], () => EditEntryClicked(this, EventArgs.Empty), () => CanMutate() && HasSelection());
        var delete = new MainCommand("Delete", [Keys.Delete], () => DeleteEntryClicked(this, EventArgs.Empty), () => CanMutate() && HasSelection());
        var refresh = new MainCommand("Refresh", [Keys.F5], () => RefreshEntryClicked(this, EventArgs.Empty), () => true);
        var apply = new MainCommand("Apply", [Keys.Control | Keys.S], () => Apply(this, EventArgs.Empty), () => _envService.HasChanges());
        var focusFilter = new MainCommand("Focus filter", [Keys.Control | Keys.F], FocusFilter, () => ActiveFilter() is not null);

        var editMenu = new ToolStripMenuItem("Edit");
        menuStrip1.Items.Insert(1, editMenu);

        Project(add, addToolStripButton, editMenu);
        Project(edit, editToolStripButton, editMenu);
        Project(delete, deleteToolStripButton, editMenu);
        Project(refresh, refreshToolStripButton, editMenu);
        Project(apply, applyToolStripButton, editMenu);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        Project(focusFilter, null, editMenu);

        return new MainCommandTable([add, edit, delete, refresh, apply, focusFilter]);
    }

    /// <summary>
    /// Shows one command in the Edit menu and mirrors its enablement onto the toolbar button that
    /// already exists for it.
    /// </summary>
    /// <param name="command">The command to project.</param>
    /// <param name="button">The toolbar button for the command, or null when the toolbar has none.</param>
    /// <param name="menu">The menu the command is listed in.</param>
    private void Project(MainCommand command, ToolStripButton? button, ToolStripMenuItem menu)
    {
        var item = new ToolStripMenuItem(command.Label) { ShortcutKeys = command.Shortcuts[0] };
        item.Click += (_, _) => command.Execute();
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

    private void EditEntryClicked(object? sender, EventArgs e)
    {
        if (GetActiveGrid() is { } grid)
            RowActionHelper.Edit(grid, _entryFormFactory);
    }

    private void DeleteEntryClicked(object? sender, EventArgs e)
    {
        if (GetActiveGrid() is { } grid)
            RowActionHelper.Delete(grid, _envService);
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
        ShowTransientStatus(outcome.Message);
    }

    private void OnConfigSaveFailed(object? sender, string message) => ShowTransientStatus(message);

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
    }

    /// <summary>
    /// Stops the transient-status timer when the window goes away.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // The exit hook still saves after the window is gone; it must not touch a dead form.
        _configService.SaveFailed -= OnConfigSaveFailed;

        // No scan may outlive the window: pending work is dropped and in-flight verdicts discarded.
        _corruption.Cancel();

        _applyStatusTimer.Stop();
        base.OnFormClosed(e);
    }
}
