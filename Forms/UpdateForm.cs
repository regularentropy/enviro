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
        releaseNotesTextBox.Text = ur.ReleaseNotes;
        releaseNotesLabel.Text = $"Release notes for v{ur.Tag}";
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void downloadButton_Click(object sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo(_updateResponse.LastReleaseURL) { UseShellExecute = true });
    }
}
