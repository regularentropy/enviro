using enviro.Models;
using enviro.Services;

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
    }

    protected override void RunAction(object sender, EventArgs e)
    {
        var name = nameTextBox.Text.Trim();
        var path = pathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) | string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Name and path cannot be empty or consist only of spaces.",
                            "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var pathModel = new EnvModel { Name = name, Path = path };

        if (!_pathService.Contains(pathModel, _t))
        {
            if (_pathService.AddEntry(pathModel, _t))
            {
                MessageBox.Show("Variable successfully added", "Action",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            return;
        }

        var res = MessageBox.Show("The variable already exists. Override?", "Confirm",
                                  MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

        if (res == DialogResult.OK)
        {
            var existing = _pathService.GetModelByName(name, _t);
            if (existing != null)
            {
                _pathService.UpdatePath(existing, path, _t);
                MessageBox.Show("Variable successfully updated", "Action",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            this.Close();
        }
    }
}