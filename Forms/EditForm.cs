using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Form used to edit entries. Path-like values can be edited either as text or as an
/// ordered list of segments, in the same window.
/// </summary>
internal partial class EditForm : AbstractEntryForm
{
    private readonly EnvModel _pm_ref;
    private readonly PathEntriesControl _entriesControl;
    private bool _entriesMode;

    public EditForm(IEnvScope scope, EnvModel pm) : base(scope)
    {
        _pm_ref = pm;

        nameTextBox.Text = pm.Name;
        pathTextBox.Text = pm.Path;

        this.Text = "Edit";
        addButton.Text = "Save";

        // Sits in the value row next to the plain text box; only one of the two is visible.
        _entriesControl = new PathEntriesControl { Dock = DockStyle.Fill, Visible = false };
        _entriesControl.ValueChanged += (_, _) => pathTextBox.Text = _entriesControl.JoinedValue;
        tableLayoutPanel3.Controls.Add(_entriesControl, 0, 1);

        entriesButton.Click += (_, _) => SetEntriesMode(!_entriesMode);
        nameTextBox.TextChanged += (_, _) => UpdateEntriesButton();
        pathTextBox.TextChanged += (_, _) => UpdateEntriesButton();

        UpdateEntriesButton();
    }

    /// <summary>
    /// Offers the entries view only while the current input is a path-like value, and always
    /// while it is open so the user can switch back.
    /// </summary>
    private void UpdateEntriesButton() =>
        entriesButton.Visible = _entriesMode || ValidationHelper.IsPathList(nameTextBox.Text, pathTextBox.Text);

    /// <summary>
    /// Switches the value row between the plain text box and the segment list. The value field
    /// stays the source of truth, so Save keeps its normal validation and change tracking.
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

        UpdateEntriesButton();
        UpdateActionState();
    }

    /// <summary>
    /// Converts a design-time height to pixels for the current DPI.
    /// </summary>
    /// <param name="logicalHeight">The height as laid out at 96 DPI.</param>
    /// <returns>The height in device pixels.</returns>
    private int ScaledHeight(int logicalHeight) =>
        (int)Math.Round(logicalHeight * DeviceDpi / 96.0);

    /// <summary>
    /// Save stays disabled until the trimmed name or value differs from the bound model.
    /// </summary>
    protected override bool HasPendingChange() =>
        _pm_ref is not null
        && (nameTextBox.Text.Trim() != _pm_ref.Name || pathTextBox.Text.Trim() != _pm_ref.Path);

    protected override void RunAction(object sender, EventArgs e)
    {
        var newName = nameTextBox.Text.Trim();
        var newPath = pathTextBox.Text.Trim();

        if (!ValidationHelper.ValidateEnvironmentVariableInput(newName, newPath))
            return;

        if (_pm_ref.State == EnvironmentalVariableState.Deleted)
        {
            DialogHelper.ShowError("Restore the variable before editing it.", "Validation");
            return;
        }

        bool nameChanged = newName != _pm_ref.Name;
        bool pathChanged = newPath != _pm_ref.Path;

        if (!nameChanged && !pathChanged)
        {
            DialogHelper.ShowInfo("No changes detected", "Edit");
            return;
        }

        var target = nameChanged ? _scope.Rename(_pm_ref.Name, newName) : _pm_ref;

        if (target == null)
        {
            DialogHelper.ShowError($"Cannot rename to '{newName}': the name is already in use.", "Validation");
            return;
        }

        if (pathChanged)
            _scope.Update(target, newPath);

        this.Close();
    }
}
