using enviro.Static;
using enviro.Services;
using System.Diagnostics;

namespace enviro.Forms;

internal partial class UpdateForm : Form
{
    private readonly UpdateResponse _updateResponse;
    public UpdateForm(UpdateResponse ur)
    {
        _updateResponse = ur;
        InitializeComponent();

        // This is a normal window, so it shows a title-bar icon; use the app's icon instead of the
        // WinForms default.
        Icon = Properties.Resources.favicon;

        // The logo ships as black ink; recolour it for the active palette so it does not vanish on
        // a dark window.
        appIcon.Image = ThemeArtwork.Tint(Properties.Resources.pen_square_svgrepo_com);

        versionLabel.Text = $"You have v{ur.CurrentVersion}. Version v{ur.Tag} is available.";
        releaseNotesLabel.Text = $"Release notes for v{ur.Tag}";
        releaseNotesTextBox.Text = ReleaseNotesFormatter.ToPlainText(ur.ReleaseNotes);
        releaseNotesTextBox.SelectionStart = 0;
        releaseNotesTextBox.SelectionLength = 0;
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void openReleaseButton_Click(object sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo(_updateResponse.LastReleaseURL) { UseShellExecute = true });
    }
}
