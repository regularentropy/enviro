using enviro.Services;
using System.Diagnostics;
using System.Reflection;

namespace enviro;

internal partial class AboutForm : Form
{
    private readonly ISoftwareMetadataService _sms;
    public AboutForm(ISoftwareMetadataService sms)
    {
        InitializeComponent();

        this._sms = sms;

        MinimizeBox = false;
        MaximizeBox = false;

        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.CenterToParent();

        versionLabel.Text = $"{_sms.Title} v.{_sms.Version}";
        repoLink.Text = $"{_sms.Title} on Github";
    }

    private void CloseForm(object sender, EventArgs e)
    {
        this.Close();
    }

    private void repoLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(_sms.RepositoryLink) { UseShellExecute = true });
    }
}
