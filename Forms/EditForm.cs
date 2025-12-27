using enviro.Models;
using enviro.Services;

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

        bool nameChanged = newName != _pm_ref.Name;
        bool pathChanged = newPath != _pm_ref.Path;

        if (nameChanged && pathChanged)
        {
            _envService.Rename(_pm_ref, newName, _t);
            var newModel = _envService.GetModelByName(newName, _t);
            if (newModel != null)
                _envService.UpdatePath(newModel, newPath, _t);
        }
        else if (nameChanged)
        {
            _envService.Rename(_pm_ref, newName, _t);
        }
        else if (pathChanged)
        {
            _envService.UpdatePath(_pm_ref, newPath, _t);
        }

        this.Close();
    }
}