using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Form used to create new entries
/// </summary>
internal partial class CreateForm : AbstractEntryForm
{
    public CreateForm(IEnvScope scope, ICorruptedPathService corruption) : base(scope, corruption)
    {
        this.Text = "Create";
        addButton.Text = "Create";

    }

    /// <summary>
    /// Warns inline while the typed name already exists, instead of asking only on submit.
    /// </summary>
    protected override string? CustomWarning()
    {
        var name = nameTextBox.Text.Trim();

        if (name.Length == 0)
            return null;

        var existing = _scope.Contains(name) ? _scope.GetByName(name) : null;

        if (existing is null)
            return null;

        return existing.State == EnvironmentalVariableState.Deleted
            ? $"'{existing.Name}' is marked for deletion — creating it restores the row."
            : $"'{existing.Name}' already exists — saving overwrites its value.";
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
            // A pending deletion is not in the registry, and creating over it restores the row.
            var message = existing.State == EnvironmentalVariableState.Deleted
                ? $"'{existing.Name}' is marked for deletion.\n\nCreating it restores the row with the value:\n{path}\n\nContinue?"
                : $"'{existing.Name}' already exists with value:\n{existing.Path}\n\nOverride?";

            var res = DialogHelper.ShowConfirm(message);

            if (res != DialogResult.OK)
                return;
        }

        _scope.Upsert(name, path, overwrite: existing is not null);
        this.Close();
    }
}
