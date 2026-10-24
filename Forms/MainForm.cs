using enviro.Factories;
using enviro.Forms;
using enviro.Models;
using enviro.Services;
using enviro.Static;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace enviro;

internal partial class MainForm : Form
{
    private readonly ITabFactory _tabFactory;
    private readonly IPathGridFactory _gridFactory;
    private readonly IEnvService _envService;
    private readonly IPathAdapter _pathAdapter;
    private readonly IActionFactory<EnvModel> _entryFormFactory;
    private readonly IUpdateService _updateService;
    private readonly IConfigService _configService;
    private readonly IServiceProvider _serviceProvider;

    // Set when the process is deliberately torn down (elevation restart, discard, or
    // apply-then-close) so the close guard does not ask a second time.
    private bool _isRestarting;

    // Command-line argument the elevated restart uses to pass the current window bounds.
    private const string WindowBoundsArgument = "--window-bounds";

    // Clears the transient "Applied N changes" message from the status strip.
    private readonly System.Windows.Forms.Timer _applyStatusTimer;

    public MainForm(IEnvService es, IPathAdapter pa, IActionFactory<EnvModel> af, IUpdateService us, ITabFactory tf, IPathGridFactory gf, IServiceProvider sp, IConfigService cs, MetadataRepository mr)
    {
        InitializeComponent();

        _tabFactory = tf;
        _gridFactory = gf;
        _updateService = us;
        _envService = es;
        _pathAdapter = pa;
        _entryFormFactory = af;
        _serviceProvider = sp;
        _configService = cs;

        ApplyElevationWindowGeometry();

        _applyStatusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _applyStatusTimer.Tick += (_, _) =>
        {
            _applyStatusTimer.Stop();
            applyStatusLabel.Text = string.Empty;
        };

        this.Name = Assembly.GetExecutingAssembly().GetName().Name;

        var isAdmin = AdminChecker.IsAdmin();

        elevationStatusLabel.Text = isAdmin ? "Administrator" : "Standard user";

        // Legend for the grid's state-indicator column, available without using colour vision.
        statusStrip1.ShowItemToolTips = true;
        pendingStatusLabel.ToolTipText = "Row markers: + added · ~ modified · – deleted · ! corrupted value";

        AllTab.Controls.AddRange([
            _tabFactory.Create(EnvironmentalVariableType.User),
            _tabFactory.Create(EnvironmentalVariableType.Machine, () => RestartAsAdmin(this, EventArgs.Empty)),
        ]);

        // Keep the toolbar's Add/Edit/Delete state in sync with the active tab and row.
        AllTab.SelectedIndexChanged += (_, _) => UpdateActionState();

        foreach (var grid in AllTab.TabPages.Cast<TabPage>().SelectMany(t => t.Controls.OfType<DataGridView>()))
            grid.SelectionChanged += (_, _) => UpdateActionState();

        _envService.GetUserVariables().ListChanged += (_, _) => UpdateActionState();
        _envService.GetMachineVariables().ListChanged += (_, _) => UpdateActionState();
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

        // Mark every row when enabled, clear every red row when disabled.
        _gridFactory.RescanAll();
        UpdateStatusStrip();
    }

    private void AboutButtonClicked(object sender, EventArgs e)
    {
        var form = _serviceProvider.GetRequiredService<AboutForm>();
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
            Arguments = $"{WindowBoundsArgument} {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height},{(maximized ? 1 : 0)}"
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

        var pending = CountTotalPendingChanges();

        switch (DialogHelper.ShowUnsavedChanges(pending))
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
        if (!await _pathAdapter.ApplyAsync())
        {
            ShowApplyFailure();
            return;
        }

        _isRestarting = true;
        Close();
    }

    /// <summary>
    /// Applies the bounds passed by an elevation restart; every other launch centres the window.
    /// </summary>
    /// <remarks>
    /// The values live only in this process's command line, so they cannot survive a reboot or
    /// leak into a later ordinary launch.
    /// </remarks>
    private void ApplyElevationWindowGeometry()
    {
        if (!TryGetElevationBounds(out var bounds, out var maximized) || !IsOnConnectedScreen(bounds))
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
    /// Reads the optional <c>--window-bounds x,y,width,height,maximized</c> argument.
    /// </summary>
    private static bool TryGetElevationBounds(out Rectangle bounds, out bool maximized)
    {
        bounds = Rectangle.Empty;
        maximized = false;

        var args = Environment.GetCommandLineArgs();

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], WindowBoundsArgument, StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = args[i + 1].Split(',');

            if (parts.Length != 5
                || !int.TryParse(parts[0], out var x)
                || !int.TryParse(parts[1], out var y)
                || !int.TryParse(parts[2], out var width)
                || !int.TryParse(parts[3], out var height))
                return false;

            bounds = new Rectangle(x, y, width, height);
            maximized = parts[4] == "1";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the passed bounds are plausible and still touch a connected screen.
    /// </summary>
    private static bool IsOnConnectedScreen(Rectangle bounds) =>
        bounds.Width >= 200
        && bounds.Height >= 150
        && Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds));

    /// <summary>
    /// Re-reads both scopes from the system, discarding pending edits only after confirmation.
    /// </summary>
    private void RefreshEntryClicked(object? sender, EventArgs e)
    {
        if (_envService.HasChanges())
        {
            var pending = CountTotalPendingChanges();

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

        // The reloaded models start clean; re-run the corruption pass and refresh the status strip.
        _gridFactory.RescanAll();
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
        catch (OperationCanceledException)
        {
            // HttpClient timeout: the host never answered.
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
    /// Enables the toolbar actions for the active tab's scope and selection, and refreshes Apply.
    /// </summary>
    private void UpdateActionState()
    {
        var grid = GetActiveGrid();
        var canMutate = grid is not null && AdminChecker.CanMutate(grid);
        var hasSelection = grid is not null && ControlHelper.GetCurrentModel(grid) is not null;

        addToolStripButton.Enabled = canMutate;
        editToolStripButton.Enabled = canMutate && hasSelection;
        deleteToolStripButton.Enabled = canMutate && hasSelection;

        UpdateApplyState();
        UpdateStatusStrip();
    }

    /// <summary>
    /// Summarises pending work, corrupted rows and the elevation state in the status strip.
    /// </summary>
    private void UpdateStatusStrip()
    {
        var bundle = _envService.GetPathModelBundle();

        var added = StateCounter.CountByState(bundle, EnvironmentalVariableState.Added);
        var modified = StateCounter.CountByState(bundle, EnvironmentalVariableState.Modified);
        var deleted = StateCounter.CountByState(bundle, EnvironmentalVariableState.Deleted);
        var total = added + modified + deleted;

        pendingStatusLabel.Text = total == 0
            ? "No pending changes"
            : $"{added} added · {modified} modified · {deleted} deleted";

        if (_configService.Config.EnableCorruptedValidation)
        {
            corruptedStatusLabel.Text = $"{StateCounter.CountCorrupted(bundle)} corrupted";
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
    /// Routes the keyboard shortcuts to the toolbar actions, respecting their enablement.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            RefreshEntryClicked(this, EventArgs.Empty);
            return true;
        }

        if (keyData == (Keys.Control | Keys.F))
        {
            if (AllTab.SelectedTab?.Controls.OfType<TextBox>().FirstOrDefault() is { } filter)
            {
                filter.Focus();
                filter.SelectAll();
                return true;
            }
        }

        if (keyData == (Keys.Control | Keys.N) && addToolStripButton.Enabled)
        {
            addToolStripButton.PerformClick();
            return true;
        }

        if ((keyData == Keys.F2 || keyData == (Keys.Control | Keys.E)) && editToolStripButton.Enabled)
        {
            editToolStripButton.PerformClick();
            return true;
        }

        if (keyData == Keys.Delete && deleteToolStripButton.Enabled)
        {
            deleteToolStripButton.PerformClick();
            return true;
        }

        if (keyData == (Keys.Control | Keys.S) && applyToolStripButton.Enabled)
        {
            applyToolStripButton.PerformClick();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// Enables the Apply toolbar button only while there is pending work.
    /// </summary>
    private void UpdateApplyState()
    {
        applyToolStripButton.Enabled = _envService.HasChanges();
    }

    private async void Apply(object sender, EventArgs e)
    {
        // Defensive no-op: the entry is disabled whenever there is nothing to apply.
        if (!_envService.HasChanges())
            return;

        var applied = CountTotalPendingChanges();

        var result = await _pathAdapter.ApplyAsync();

        if (!result)
        {
            ShowApplyFailure();
            return;
        }

        // A successful apply resets the workspace; start from the full list again.
        _gridFactory.ClearFilters();
        ShowApplied(applied);
    }

    /// <summary>
    /// Reports a successful apply in the status strip for a few seconds instead of a modal dialog.
    /// </summary>
    /// <param name="count">The number of changes that were applied.</param>
    private void ShowApplied(int count)
    {
        applyStatusLabel.Text = $"Applied {count} {(count == 1 ? "change" : "changes")}";
        _applyStatusTimer.Stop();
        _applyStatusTimer.Start();
    }

    /// <summary>
    /// Stops the transient-status timer when the window goes away.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _applyStatusTimer.Stop();
        base.OnFormClosed(e);
    }

    /// <summary>
    /// Reports a failed apply with the pending work that is still left behind.
    /// </summary>
    private void ShowApplyFailure() =>
        DialogHelper.ShowError(
            $"Failed to apply changes.\n{DescribePendingChanges()}\nSome variables may already have been written.",
            "Apply"
        );

    /// <summary>
    /// Counts the rows pending across both scopes.
    /// </summary>
    private int CountTotalPendingChanges() =>
        CountPending(_envService.GetUserVariables()) + CountPending(_envService.GetMachineVariables());

    /// <summary>
    /// Summarises the rows still pending after a failed apply, with a count per scope.
    /// </summary>
    /// <returns>A sentence such as "4 changes are still pending (3 User, 1 Machine)."</returns>
    private string DescribePendingChanges()
    {
        var userCount = CountPending(_envService.GetUserVariables());
        var machineCount = CountPending(_envService.GetMachineVariables());
        var total = userCount + machineCount;

        if (total == 0)
            return "No changes appear to be pending.";

        var scopes = new List<string>(2);

        if (userCount > 0)
            scopes.Add($"{userCount} User");

        if (machineCount > 0)
            scopes.Add($"{machineCount} Machine");

        var noun = total == 1 ? "change is" : "changes are";
        return $"{total} {noun} still pending ({string.Join(", ", scopes)}).";
    }

    private static int CountPending(BindingList<EnvModel> variables) =>
        variables.Count(v => v.State != EnvironmentalVariableState.Unchanged);
}
