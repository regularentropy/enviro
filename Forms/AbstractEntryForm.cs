using enviro.Services;
using enviro.Static;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace enviro.Forms;

internal abstract partial class AbstractEntryForm : Form
{
    /// <summary>
    /// Design-time height of the form in its plain text view, as laid out at 96 DPI.
    /// </summary>
    protected const int TextModeHeight = 180;

    /// <summary>
    /// Design-time height of the form while the segment list is shown, as laid out at 96 DPI.
    /// </summary>
    protected const int EntriesModeHeight = 500;

    protected readonly IEnvScope _scope;

    private readonly ContextMenuStrip _browseMenu;
    private readonly PathEntriesControl _entriesControl;
    private bool _entriesMode;

    public AbstractEntryForm(IEnvScope scope, ICorruptedPathService corruption)
    {
        InitializeComponent();

        _scope = scope;

        nameTextBox.TextChanged += TextBox_Changed;
        pathTextBox.TextChanged += TextBox_Changed;

        _browseMenu = new ContextMenuStrip();
        _browseMenu.Items.Add("Browse folder…", null, (_, _) => Browse(isFolder: true));
        _browseMenu.Items.Add("Browse file…", null, (_, _) => Browse(isFolder: false));

        browseButton.Click += BrowseButtonClicked;

        // Sits in the value row next to the plain text box; only one of the two is visible.
        _entriesControl = new PathEntriesControl(corruption) { Dock = DockStyle.Fill, Visible = false };
        _entriesControl.ValueChanged += (_, _) => pathTextBox.Text = _entriesControl.JoinedValue;
        tableLayoutPanel3.Controls.Add(_entriesControl, 0, 1);

        entriesButton.Click += (_, _) => SetEntriesMode(!_entriesMode);
        // Always offered: the segment list is the tool for building and extending a path list,
        // so it must not require the user to type a ';' before it appears.
        entriesButton.Visible = true;

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
    /// Opens the shell picker and writes the chosen file or folder into the value field.
    /// The folder picker allows multiple selections; the picks are joined into one path list.
    /// </summary>
    /// <param name="isFolder">True to pick folders, false to pick a file.</param>
    private void Browse(bool isFolder)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = isFolder,
            AllowNonFileSystemItems = true,
            Multiselect = isFolder
        };

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            return;

        var picks = isFolder ? dialog.FileNames : [dialog.FileName];
        pathTextBox.Text = ValidationHelper.JoinPathList(picks);
    }

    /// <summary>
    /// Switches the value row between the plain text box and the segment list. The value field
    /// stays the source of truth, so the subclass's save keeps its normal validation and change
    /// tracking.
    /// </summary>
    /// <param name="enabled">True to show the segment list, false to show the text box.</param>
    private void SetEntriesMode(bool enabled)
    {
        _entriesMode = enabled;

        if (enabled)
            _entriesControl.LoadValue(pathTextBox.Text);

        tableLayoutPanel4.Visible = !enabled;
        _entriesControl.Visible = enabled;
        entriesButton.Text = enabled ? "Edit string" : "Edit entries";

        // In entries mode the value row owns the height instead of hugging the text box.
        tableLayoutPanel3.AutoSize = !enabled;
        tableLayoutPanel3.RowStyles[1] = enabled
            ? new RowStyle(SizeType.Percent, 100F)
            : new RowStyle(SizeType.AutoSize);

        ClientSize = new Size(ClientSize.Width, ScaledHeight(enabled ? EntriesModeHeight : TextModeHeight));

        UpdateActionState();
    }

    /// <summary>
    /// Converts a design-time height to pixels for the current DPI.
    /// </summary>
    /// <param name="logicalHeight">The height as laid out at 96 DPI.</param>
    /// <returns>The height in device pixels.</returns>
    private int ScaledHeight(int logicalHeight) =>
        (int)Math.Round(logicalHeight * DeviceDpi / 96.0);

    protected void Close(object sender, EventArgs e)
    {
        this.Close();
    }
}
