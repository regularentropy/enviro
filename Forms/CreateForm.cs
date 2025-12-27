using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Form used to create new entries
/// </summary>
internal partial class CreateForm : AbstractEntryForm
{
    private readonly EnvironmentalVariableType _t;

    public CreateForm(IEnvService es, EnvironmentalVariableType t) : base(es)
    {
        _t = t;

        this.Text = "Create";
        addButton.Text = "Create";

    }

    protected override void RunAction(object sender, EventArgs e)
    {
        var name = nameTextBox.Text.Trim();
        var path = pathTextBox.Text.Trim();

        if (!ValidationHelper.ValidateEnvironmentVariableInput(name, path))
            return;

        var pathModel = new EnvModel { Name = name, Path = path };

        if (!_envService.Contains(pathModel, _t))
        {
            if (_envService.AddEntry(pathModel, _t))
            {
                DialogHelper.ShowInfo("Variable successfully added", "Action");
                this.Close();
            }
            return;
        }

        var res = DialogHelper.ShowConfirm("The variable already exists. Override?");

        if (res == DialogResult.OK)
        {
            var existing = _envService.GetModelByName(name, _t);
            if (existing != null)
            {
                _envService.UpdatePath(existing, path, _t);
                DialogHelper.ShowInfo("Variable successfully updated", "Action");
            }

            this.Close();
        }
    }
}