using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Factories;

internal interface IPathGridFactory
{
    DataGridView Create(EnvironmentalVariableType tab);
}

internal sealed class PathGridFactory : IPathGridFactory
{
    private readonly IEnvService ps;
    private readonly IContextMenuFactory cf;
    private readonly IActionFactory<EnvModel> af;

    public PathGridFactory(IEnvService pathService, IActionFactory<EnvModel> actionFactory, IContextMenuFactory contextMenuFactory)
    {
        ps = pathService;
        cf = contextMenuFactory;
        af = actionFactory;
    }

    public DataGridView Create(EnvironmentalVariableType tab)
    {
        DataGridView grid = new()
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            AllowUserToResizeRows = false,
            BackgroundColor = Color.White,
        };

#if DEBUG
        grid.AutoGenerateColumns = true;
#else
        grid.AutoGenerateColumns = false;
#endif

        grid.Columns.AddRange([
            new DataGridViewTextBoxColumn() { Name="NameText", HeaderText = "Name", DataPropertyName = nameof(EnvModel.Name)},
            new DataGridViewTextBoxColumn() { Name="PathText", HeaderText = "Path", DataPropertyName = nameof(EnvModel.Path) }
        ]);

        grid.DataSource = tab == EnvironmentalVariableType.User ? ps.GetUserVariables() : ps.GetMachineVariables();

        grid.CellMouseDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Only an admin can edit the "Machine" panel
            if (tab == EnvironmentalVariableType.Machine && !AdminChecker.IsAdmin())
                return;

            var pm = grid.CurrentRow.DataBoundItem as EnvModel;

                af.Create(pm, tab).ShowDialog();
        };

        grid.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                // Getting the place of the cursor
                var hit = grid.HitTest(e.X, e.Y);

                // Not showing if user clicked on an empty space 
                if (hit.RowIndex < 0)
                {
                    return;
                }

                grid.CurrentCell = grid[hit.ColumnIndex, hit.RowIndex];

                var model = (EnvModel)grid.CurrentRow.DataBoundItem;

                var cms = cf.CreateForPathGrid(grid, model);
                cms.Show(grid, e.Location);
            }
        };

        grid.CellFormatting += (s, e) =>
            {
                if (grid.Rows[e.RowIndex].DataBoundItem is EnvModel pm)
                {
                    GridStyleHelper.ApplyStateFormatting(e, pm);
                }
            };

        return grid;
    }
}
