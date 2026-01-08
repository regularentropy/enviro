using enviro.Models;
using enviro.Services;
using enviro.Static;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating DataGridView instances for environmental variables.
/// </summary>
internal interface IPathGridFactory
{
    /// <summary>
    /// Creates a configured DataGridView for the specified environmental variable type.
    /// </summary>
    /// <param name="tab">The type of environmental variable (User or Machine).</param>
    /// <returns>A configured DataGridView instance.</returns>
    DataGridView Create(EnvironmentalVariableType tab);
}

/// <summary>
/// Factory for creating and configuring DataGridView instances for displaying environmental variables.
/// </summary>
internal sealed class PathGridFactory : IPathGridFactory
{
    private readonly IEnvService ps;
    private readonly IContextMenuFactory cf;
    private readonly IActionFactory<EnvModel> af;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathGridFactory"/> class.
    /// </summary>
    /// <param name="pathService">The service for managing environmental variables.</param>
    /// <param name="actionFactory">The factory for creating action forms.</param>
    /// <param name="contextMenuFactory">The factory for creating context menus.</param>
    public PathGridFactory(IEnvService pathService, IActionFactory<EnvModel> actionFactory, IContextMenuFactory contextMenuFactory)
    {
        ps = pathService;
        cf = contextMenuFactory;
        af = actionFactory;
    }

    /// <summary>
    /// Creates a DataGridView configured for displaying and editing environmental variables.
    /// Includes event handlers for double-click editing, right-click context menu, and cell formatting.
    /// </summary>
    /// <param name="tab">The type of environmental variable (User or Machine).</param>
    /// <returns>A fully configured DataGridView instance.</returns>
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
