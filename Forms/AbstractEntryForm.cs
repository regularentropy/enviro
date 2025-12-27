using enviro.Services;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace enviro.Forms;

internal abstract partial class AbstractEntryForm : Form
{
    protected readonly IEnvService _envService;

    public AbstractEntryForm(IEnvService es)
    {
        InitializeComponent();

        _envService = es;

        nameTextBox.TextChanged += PathTextBox_Validating;
        pathTextBox.TextChanged += PathTextBox_Validating;

        loadDialogueButton.MouseDown += DialogueAction;

        new ToolTip().SetToolTip(loadDialogueButton, "LMB - open folder, RMB - open file");

        this.CenterToScreen();
    }

    protected void PathTextBox_Validating(object? sender, EventArgs e)
    {
        var box = (TextBox)sender!;
        if (box.Text.Contains('='))
        {
            addButton.Enabled = false;
            return;
        }
        addButton.Enabled = true;
    }

    protected abstract void RunAction(object sender, EventArgs e);

    protected void DialogueAction(object? sender, MouseEventArgs e)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = e.Button == MouseButtons.Left,
            AllowNonFileSystemItems = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            pathTextBox.Text = dialog.FileName;
        }
    }

    protected void Close(object sender, EventArgs e)
    {
        this.Close();
    }
}
