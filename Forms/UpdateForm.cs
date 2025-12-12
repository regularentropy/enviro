using enviro.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace enviro.Forms;

internal partial class UpdateForm : Form
{
    private MetadataRepository _metadataRepository;
    private UpdateResponse _updateResponse;
    public UpdateForm(MetadataRepository mr, UpdateResponse ur)
    {
        _metadataRepository = mr;
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
        Process.Start(new ProcessStartInfo(_updateResponse.LastReleaseURL) { UseShellExecute = true});
    }
}
