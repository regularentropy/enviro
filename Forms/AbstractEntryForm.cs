using enviro.Services;
using enviro.Static;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace enviro.Forms;

internal abstract partial class AbstractEntryForm : Form
{
    protected readonly IEnvService _envService;

    private readonly ContextMenuStrip _browseMenu;

    public AbstractEntryForm(IEnvService es)
    {
        InitializeComponent();

        _envService = es;

        nameTextBox.TextChanged += TextBox_Changed;
        pathTextBox.TextChanged += TextBox_Changed;

        _browseMenu = new ContextMenuStrip();
        _browseMenu.Items.Add("Browse folder…", null, (_, _) => Browse(isFolder: true));
        _browseMenu.Items.Add("Browse file…", null, (_, _) => Browse(isFolder: false));

        browseButton.Click += BrowseButtonClicked;

        UpdateActionState();

        this.CenterToScreen();
    }

    /// <summary>
    /// Enables the action button only when both fields are usable and, for an edit,
    /// when the input actually differs from the bound model.
    /// </summary>
    protected void UpdateActionState() =>
        addButton.Enabled =
            HasPendingChange()
            && ValidationHelper.IsValidEnvironmentVariableInput(nameTextBox.Text, pathTextBox.Text);

    /// <summary>
    /// Whether the current input differs from what the form was opened with. Create forms
    /// always allow submission, so the default is <see langword="true"/>.
    /// </summary>
    protected virtual bool HasPendingChange() => true;

    private void TextBox_Changed(object? sender, EventArgs e) => UpdateActionState();

    protected abstract void RunAction(object sender, EventArgs e);

    private void BrowseButtonClicked(object? sender, EventArgs e) =>
        _browseMenu.Show(browseButton, new Point(0, browseButton.Height));

    /// <summary>
    /// Opens the shell picker and inserts the chosen file or folder into the value field.
    /// </summary>
    /// <param name="isFolder">True to pick a folder, false to pick a file.</param>
    private void Browse(bool isFolder)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = isFolder,
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
