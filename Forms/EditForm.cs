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

    public EditForm(IEnvScope scope, EnvModel pm, ICorruptedPathService corruption)
        : base(scope, corruption)
    {
        _pm_ref = pm;

        nameTextBox.Text = pm.Name;
        pathTextBox.Text = pm.Path;

        this.Text = "Edit";
        addButton.Text = "Save";
    }

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

        if (!HasPendingChange())
        {
            DialogHelper.ShowInfo("No changes detected", "Edit");
            return;
        }

        var edit = _scope.ApplyEdit(_pm_ref, newName, newPath);

        if (edit.Model is null)
        {
            DialogHelper.ShowError(edit.Rejection ?? string.Empty, "Validation");
            return;
        }

        this.Close();
    }
}
