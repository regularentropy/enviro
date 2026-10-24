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
            _envService.AddEntry(pathModel, _t);
            this.Close();

            return;
        }

        var existing = _envService.GetModelByName(name, _t);
        if (existing is null)
            return;

        var res = DialogHelper.ShowConfirm($"'{existing.Name}' already exists with value:\n{existing.Path}\n\nOverride?");

        if (res == DialogResult.OK)
        {
            if (existing.State == EnvironmentalVariableState.Deleted)
                _envService.RestoreItem(existing);

            _envService.UpdatePath(existing, path, _t);
            this.Close();
        }
    }
}