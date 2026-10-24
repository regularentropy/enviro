using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Form used to edit entries
/// </summary>
internal partial class EditForm : AbstractEntryForm
{
    private readonly EnvModel _pm_ref;
    private readonly EnvironmentalVariableType _t;

    public EditForm(IEnvService ps, EnvModel pm, EnvironmentalVariableType t) : base(ps)
    {
        _pm_ref = pm;
        _t = t;

        nameTextBox.Text = pm.Name;
        pathTextBox.Text = pm.Path;

        this.Text = "Edit";
        addButton.Text = "Edit";
    }

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
            DialogHelper.ShowError("No changes detected");
            return;
        }

        var target = nameChanged ? _envService.TryRename(_pm_ref.Name, newName, _t) : _pm_ref;

        if (target == null)
        {
            DialogHelper.ShowError($"Cannot rename to '{newName}': the name is already in use.", "Validation");
            return;
        }

        if (pathChanged)
            _envService.UpdatePath(target, newPath, _t);

        this.Close();
    }
}