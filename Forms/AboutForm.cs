using enviro.Services;
using System.Diagnostics;

namespace enviro;

internal partial class AboutForm : Form
{
    private readonly MetadataRepository _mr;
    public AboutForm(MetadataRepository mr)
    {
        InitializeComponent();

        _mr = mr;

        MinimizeBox = false;
        MaximizeBox = false;

        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.CenterToParent();

        versionLabel.Text = $"{_mr.Title} v.{_mr.Version}";
        repoLink.Text = $"{_mr.Title} on Github";
    }

    private void CloseForm(object sender, EventArgs e)
    {
        this.Close();
    }

    private void repoLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(_mr.RepositoryLink) { UseShellExecute = true });
    }
}
