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
    private readonly IEnvService _envService;
    private readonly IPathAdapter _pathAdapter;
    private readonly IUpdateService _updateService;
    private readonly IConfigService _configService;
    private readonly IServiceProvider _serviceProvider;

    private bool _revertingCorruptedToggle;

    public MainForm(IEnvService es, IPathAdapter pa, IUpdateService us, ITabFactory tf, IServiceProvider sp, IConfigService cs, MetadataRepository mr)
    {
        InitializeComponent();

        this.CenterToScreen();

        _tabFactory = tf;
        _updateService = us;
        _envService = es;
        _pathAdapter = pa;
        _serviceProvider = sp;
        _configService = cs;

        this.Name = Assembly.GetExecutingAssembly().GetName().Name;

        if (!AdminChecker.IsAdmin())
        {
            ToolStripItem item = new ToolStripMenuItem() { Text = "Run as Administrator" };
            saveToolStripMenuItem.DropDownItems.Add(item);

            item.Click += RestartAsAdmin;
        }

        AllTab.Controls.AddRange([
            _tabFactory.Create(EnvironmentalVariableType.User),
            _tabFactory.Create(EnvironmentalVariableType.Machine),
        ]);

        _envService.GetUserVariables().ListChanged += (_, _) => UpdateApplyState();
        _envService.GetMachineVariables().ListChanged += (_, _) => UpdateApplyState();
        UpdateApplyState();

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
        if (sender is not ToolStripMenuItem item || _revertingCorruptedToggle)
        {
            return;
        }

        var previous = _configService.Config.EnableCorruptedValidation;
        _configService.Config.EnableCorruptedValidation = item.Checked;

        var result = DialogHelper.ShowConfirm(
            "Application restart required",
            "Apply changes and restart now?"
        );

        if (result == DialogResult.OK)
        {
            Application.Restart();
            return;
        }

        // Declined: undo both the setting and the menu state.
        _configService.Config.EnableCorruptedValidation = previous;

        _revertingCorruptedToggle = true;
        item.Checked = previous;
        _revertingCorruptedToggle = false;
    }

    private void AboutButtonClicked(object sender, EventArgs e)
    {
        var form = _serviceProvider.GetRequiredService<AboutForm>();
        form.ShowDialog();
    }

    public void RestartAsAdmin(object? sender, EventArgs e)
    {
        ProcessStartInfo proc = new()
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Application.ExecutablePath,
            Verb = "runas"
        };
        try
        {
            Process.Start(proc);
            Application.Exit();
        }
        catch { }
    }

    private async void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
    {
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
    }

    /// <summary>
    /// Enables the Apply entry only while there is pending work.
    /// </summary>
    private void UpdateApplyState() => toolStripMenuItem1.Enabled = _envService.HasChanges();

    private async void Apply(object sender, EventArgs e)
    {
        // Defensive no-op: the entry is disabled whenever there is nothing to apply.
        if (!_envService.HasChanges())
            return;

        var result = await _pathAdapter.ApplyAsync();

        if (!result)
        {
            DialogHelper.ShowError(
                $"Failed to apply changes.\n{DescribePendingChanges()}\nSome variables may already have been written.",
                "Apply"
            );
            return;
        }

        DialogHelper.ShowMessage("Successfully applied changes", "Apply");
    }

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
