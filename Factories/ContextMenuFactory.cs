using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Factories;

internal interface IContextMenuFactory
{
    ContextMenuStrip CreateForPathGrid(DataGridView grid, EnvModel pm);
}

internal sealed class ContextMenuFactory : IContextMenuFactory
{

    private readonly IEnvService _pathService;
    private readonly IActionFactory<EnvModel> _entryFormFactory;

    public ContextMenuFactory(IEnvService envService, IActionFactory<EnvModel> factory)
    {
        _pathService = envService;
        _entryFormFactory = factory;
    }

    public ContextMenuStrip CreateForPathGrid(DataGridView grid, EnvModel pm)
    {
        var cms = new ContextMenuStrip();
        cms.Items.Add("Create", null, (_, _) => AppendToPath(grid));
        cms.Items.Add("Edit", null, (_, _) => CreateEdit(grid));
        cms.Items.Add("Delete", null, (_, _) => DeleteCurrentRow(grid));
        cms.Items.Add(new ToolStripSeparator());
        cms.Items.Add("Copy value", null, (_, _) => CopyValue(grid));
        cms.Items.Add("Copy path", null, (_, _) => CopyPath(grid));
        cms.Items.Add("Copy original path", null, (_, _) => CopyOriginalPath(grid));

        if (pm.State == EnvironmentalVariableState.Deleted)
            cms.Items.Insert(1, new ToolStripMenuItem("Restore", null, (_, _) => RestoreItem(pm)));
        else if (pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Insert(1, new ToolStripMenuItem("Reset", null, (_, _) => ResetItem(pm)));

        return cms;
    }

    private void DeleteCurrentRow(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        if (g.CurrentRow?.DataBoundItem is EnvModel m)
            _pathService.RemoveEntry(m, TabOf(g));
    }

    private void AppendToPath(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        _entryFormFactory.Create(TabOf(g)).ShowDialog();
    }

    private void CreateEdit(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        if (g.CurrentRow?.DataBoundItem is EnvModel m)
            _entryFormFactory.Create(m, TabOf(g)).ShowDialog();
    }

    private void CopyPath(DataGridView g)
    {
        if (g.CurrentRow?.DataBoundItem is EnvModel m)
            Clipboard.SetText(m.Path);
    }

    private void CopyValue(DataGridView g)
    {
        if (g.CurrentRow?.DataBoundItem is EnvModel m)
            Clipboard.SetText(m.Name);
    }
    private void CopyOriginalPath(DataGridView g)
    {
        if (g.CurrentRow?.DataBoundItem is EnvModel m)
            Clipboard.SetText(m.OrginalPath);
    }

    private void RestoreItem(EnvModel pm) => _pathService.RestoreItem(pm);
    private void ResetItem(EnvModel pm) => _pathService.ResetItem(pm);

    private EnvironmentalVariableType TabOf(Control c) =>
        c.Parent?.Text switch
        {
            "Machine" => EnvironmentalVariableType.Machine,
            "User" => EnvironmentalVariableType.User
        };
}
