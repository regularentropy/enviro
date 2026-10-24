using enviro.Services;
using enviro.Static;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace enviro.Forms;

internal abstract partial class AbstractEntryForm : Form
{
    protected readonly IEnvService _envService;

    public AbstractEntryForm(IEnvService es)
    {
        InitializeComponent();

        _envService = es;

        nameTextBox.TextChanged += TextBox_Changed;
        pathTextBox.TextChanged += TextBox_Changed;

        loadDialogueButton.MouseDown += DialogueAction;

        new ToolTip().SetToolTip(loadDialogueButton, "LMB - open folder, RMB - open file");

        UpdateActionState();

        this.CenterToScreen();
    }

    /// <summary>
    /// Enables the action button only when both fields are usable.
    /// </summary>
    protected void UpdateActionState() =>
        addButton.Enabled = ValidationHelper.IsValidEnvironmentVariableInput(nameTextBox.Text, pathTextBox.Text);

    private void TextBox_Changed(object? sender, EventArgs e) => UpdateActionState();

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
