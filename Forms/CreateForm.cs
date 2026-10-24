using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Form used to create new entries
/// </summary>
internal partial class CreateForm : AbstractEntryForm
{
    public CreateForm(IEnvScope scope) : base(scope)
    {
        this.Text = "Create";
        addButton.Text = "Create";

    }

    protected override void RunAction(object sender, EventArgs e)
    {
        var name = nameTextBox.Text.Trim();
        var path = pathTextBox.Text.Trim();

        if (!ValidationHelper.ValidateEnvironmentVariableInput(name, path))
            return;

        var existing = _scope.Contains(name) ? _scope.GetByName(name) : null;

        if (existing is not null)
        {
            var res = DialogHelper.ShowConfirm($"'{existing.Name}' already exists with value:\n{existing.Path}\n\nOverride?");

            if (res != DialogResult.OK)
                return;
        }

        _scope.Upsert(name, path, overwrite: existing is not null);
        this.Close();
    }
}
