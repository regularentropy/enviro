﻿using enviro.Services;
using enviro.Static;
using System.ComponentModel;
using System.Diagnostics;

namespace enviro;

internal partial class AboutForm : Form
{
    private readonly MetadataRepository _mr;
    private readonly IAppLog _log;
    public AboutForm(MetadataRepository mr, IAppLog log)
    {
        InitializeComponent();

        _mr = mr;
        _log = log;

        Text = $"About {_mr.Title}";
        titleLabel.Text = _mr.Title;
        versionLabel.Text = $"Version {_mr.Version}";
        copyrightLabel.Text = _mr.Copyright;

        // Each link carries its own destination, so one handler serves all three.
        repoLink.Tag = _mr.RepositoryLink;
        issuesLink.Tag = _mr.IssuesLink;
        licenseLink.Tag = _mr.LicenseLink;
    }

    private void okButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void OpenLink(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (sender is not LinkLabel { Tag: string url })
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // No default browser, or the shell refused the launch: a friendly notice, not the global crash dialog.
            _log.Error($"Could not open About link '{url}'.", ex);
            DialogHelper.ShowInfo("The link could not be opened in your browser.", "About");
        }
    }
}
