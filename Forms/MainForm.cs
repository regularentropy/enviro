using enviro.Factories;
using enviro.Models;
using enviro.Services;
using enviro.Static;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace enviro;

internal partial class MainForm : Form
{
    private readonly ITabFactory _tabFactory;
    private readonly IEnvService _pathService;
    private readonly IPathAdapter _pathAdapter;
    private readonly IUpdateService _updateService;
    private readonly ISoftwareMetadataService _softwareMetadataService;

    private readonly IServiceProvider _serviceProvider;

    public MainForm(IEnvService ps, IPathAdapter pa, IUpdateService us, ITabFactory tf, IServiceProvider sp, ISoftwareMetadataService sms)
    {
        InitializeComponent();

        this.CenterToScreen();

        _tabFactory = tf;

        _updateService = us;
        _softwareMetadataService = sms;
        _pathService = ps;
        _pathAdapter = pa;

        _serviceProvider = sp;


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
            var result = await _updateService.CheckForUpdatesAsync();
            if (result)
            {
                var msg = MessageBox.Show("New version detected. Download?", "Updater", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (msg == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(_softwareMetadataService.RepositoryLink) { UseShellExecute = true });
                }
            }
            else
            {
                MessageBox.Show("No updates found", "Updater", MessageBoxButtons.OK);
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            MessageBox.Show("Cannot access the internet", "Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);

        }

        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MessageBox.Show("The repository is unavailable.\nCheck the owners webpage.", "Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void Apply(object sender, EventArgs e)
    {
        if (!_pathService.HasChanges())
        {
            MessageBox.Show("No changes detected", "Apply", MessageBoxButtons.OK);
            return;
        }

        var result = await _pathAdapter.ApplyAsync();
        if (result)
        {
            MessageBox.Show("Successfully applied changes", "Apply", MessageBoxButtons.OK);
        }
    }
}
