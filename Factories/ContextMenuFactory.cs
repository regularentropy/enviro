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
        cms.Items.Add("Copy value", null, (_, _) => CopyToClipboard(grid, m => m.Name));
        cms.Items.Add("Copy path", null, (_, _) => CopyToClipboard(grid, m => m.Path));
        cms.Items.Add("Copy original path", null, (_, _) => CopyToClipboard(grid, m => m.OrginalPath));

        if (pm.State == EnvironmentalVariableState.Deleted)
            cms.Items.Insert(1, new ToolStripMenuItem("Restore", null, (_, _) => RestoreItem(pm)));
        else if (pm.State == EnvironmentalVariableState.Modified)
            cms.Items.Insert(1, new ToolStripMenuItem("Reset", null, (_, _) => ResetItem(pm)));

        return cms;
    }

    private void DeleteCurrentRow(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        var model = ControlHelper.GetCurrentModel(g);
        if (model is not null)
            _pathService.RemoveEntry(model, ControlHelper.GetTabType(g));
    }

    private void AppendToPath(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        _entryFormFactory.Create(ControlHelper.GetTabType(g)).ShowDialog();
    }

    private void CreateEdit(DataGridView g)
    {
        if (!AdminChecker.ValidateTab(g)) return;
        var model = ControlHelper.GetCurrentModel(g);
        if (model is not null)
            _entryFormFactory.Create(model, ControlHelper.GetTabType(g)).ShowDialog();
    }

    private void CopyToClipboard(DataGridView g, Func<EnvModel, string> selector)
    {
        var model = ControlHelper.GetCurrentModel(g);
        ClipboardHelper.CopyModelProperty(model, selector);
    }

    private void RestoreItem(EnvModel pm) => _pathService.RestoreItem(pm);
    private void ResetItem(EnvModel pm) => _pathService.ResetItem(pm);
}
