using enviro.Static;
using enviro.Services;
using System.ComponentModel;
using System.Diagnostics;

namespace enviro.Forms;

internal partial class UpdateForm : Form
{
    private readonly UpdateResponse _updateResponse;
    private readonly IAppLog _log;

    public UpdateForm(UpdateResponse ur, IAppLog log)
    {
        _updateResponse = ur;
        _log = log;
        InitializeComponent();

        // The release URL arrives from the GitHub response, so only a plain http(s) address may reach
        // the shell; anything else leaves the action unavailable instead of a dead click or a crash.
        openReleaseButton.Enabled = IsOpenableUrl(_updateResponse.LastReleaseURL);

        // This is a normal window, so it shows a title-bar icon; use the app's icon instead of the
        // WinForms default.
        Icon = Properties.Resources.favicon;

        // The logo ships as black ink; recolour it for the active palette so it does not vanish on
        // a dark window.
        appIcon.Image = ThemeArtwork.Tint(Properties.Resources.pen_square_svgrepo_com);

        versionLabel.Text = $"You have v{ur.CurrentVersion}. Version v{ur.Tag} is available.";
        releaseNotesLabel.Text = $"Release notes for v{ur.Tag}";

        // The notes are Markdown, so they are rendered as RTF: bold headings, real bullets and
        // clickable links. Colours and the body font come from the palette and the box itself, so the
        // notes follow the active colour mode and scale with the window font.
        releaseNotesTextBox.BackColor = ThemePalette.GridBackground;
        releaseNotesTextBox.ForeColor = ThemePalette.CellText;
        releaseNotesTextBox.Rtf = ReleaseNotesFormatter.ToRtf(
            ur.ReleaseNotes,
            releaseNotesTextBox.Font.Name,
            releaseNotesTextBox.Font.SizeInPoints,
            ThemePalette.CellText,
            ThemePalette.LinkText);
        releaseNotesTextBox.SelectionStart = 0;
        releaseNotesTextBox.SelectionLength = 0;
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void openReleaseButton_Click(object sender, EventArgs e)
    {
        OpenUrl(_updateResponse.LastReleaseURL);
    }

    private void releaseNotesTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
    {
        // The notes are untrusted text and the label hides where the click really goes, so a link from
        // them names its target before anything reaches the shell.
        if (e.LinkText is not { } url || !IsOpenableUrl(url))
            return;

        if (DialogHelper.ShowConfirm($"Open this address in your browser?\n\n{url}", "Open link") != DialogResult.OK)
            return;

        OpenUrl(url);
    }

    /// <summary>
    /// Opens <paramref name="url"/> in the default browser.
    /// </summary>
    /// <param name="url">The address to open; anything that is not an absolute http or https URI is ignored.</param>
    private void OpenUrl(string? url)
    {
        if (url is null || !IsOpenableUrl(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // No default browser, or the shell refused the launch: a friendly notice, not the global crash dialog.
            _log.Error($"Could not open '{url}' from the update dialog.", ex);
            DialogHelper.ShowInfo("The link could not be opened in your browser.", "Updater");
        }
    }

    /// <summary>
    /// Determines whether <paramref name="url"/> is an absolute http or https URI that is safe to open
    /// through the shell.
    /// </summary>
    private static bool IsOpenableUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
