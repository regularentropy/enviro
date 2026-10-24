using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;

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

    /// <summary>
    /// Re-evaluates the corrupted-path overlay for every variable in both scopes.
    /// Clears the overlay instead when <see cref="IConfigService.Config"/>'s
    /// corrupted-validation option is off.
    /// </summary>
    void RescanAll();
}

/// <summary>
/// Factory for creating and configuring DataGridView instances for displaying environmental variables.
/// </summary>
internal sealed class PathGridFactory : IPathGridFactory
{
    private const string StateIndicatorColumnName = "StateIndicator";

    private readonly IEnvService ps;
    private readonly IContextMenuFactory cf;
    private readonly IActionFactory<EnvModel> af;
    private readonly IConfigService _cs;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathGridFactory"/> class.
    /// </summary>
    /// <param name="pathService">The service for managing environmental variables.</param>
    /// <param name="actionFactory">The factory for creating action forms.</param>
    /// <param name="contextMenuFactory">The factory for creating context menus.</param>
    public PathGridFactory(IEnvService pathService, IActionFactory<EnvModel> actionFactory, IContextMenuFactory contextMenuFactory, IConfigService cs)
    {
        ps = pathService;
        cf = contextMenuFactory;
        af = actionFactory;
        _cs = cs;
    }

    /// <summary>
    /// Creates a DataGridView configured for displaying and editing environmental variables.
    /// Includes event handlers for double-click editing, corrupted variable check, right-click context menu, and cell formatting.
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
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
        };

        // Comfortable row density that follows the font/DPI size instead of a fixed pixel value.
        grid.RowTemplate.Height = grid.Font.Height + 12;

        grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

#if DEBUG
        grid.AutoGenerateColumns = true;
#else
        grid.AutoGenerateColumns = false;
#endif

        grid.Columns.AddRange([
            new DataGridViewTextBoxColumn()
            {
                Name = StateIndicatorColumnName,
                HeaderText = string.Empty,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 28,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            },
            new DataGridViewTextBoxColumn() { Name="NameText", HeaderText = "Name", DataPropertyName = nameof(EnvModel.Name), SortMode = DataGridViewColumnSortMode.Automatic},
            new DataGridViewTextBoxColumn() { Name="PathText", HeaderText = "Value", DataPropertyName = nameof(EnvModel.Path), SortMode = DataGridViewColumnSortMode.Automatic }
        ]);

        var bundle = ps.GetPathModelBundle();

        var bindingSource = new BindingSource
        {
            DataSource = bundle,
            DataMember = tab == EnvironmentalVariableType.User ? nameof(EnvModelBundle.User) : nameof(EnvModelBundle.Machine)
        };

        grid.DataSource = bindingSource;

        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            GridStyleHelper.SortGrid(grid, bindingSource, e.ColumnIndex);
        };

        // Corrupted variable detection. The subscription is always attached so turning the
        // option on never requires rebuilding the grid; the handler checks the config instead.
        bindingSource.ListChanged += (s, e) =>
        {
            // index resets to -1 on change
            if (e.NewIndex < 0 || !_cs.Config.EnableCorruptedValidation)
                return;

            EnvModel m = (EnvModel)bindingSource[e.NewIndex]!;
            MarkIfCorrupted(m);
        };

        // Mark the rows already bound to this grid when validation is enabled.
        if (_cs.Config.EnableCorruptedValidation)
        {
            var models = tab == EnvironmentalVariableType.User ? bundle.User : bundle.Machine;

            foreach (var env in models) MarkIfCorrupted(env);
        }

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

                // Clicking the empty area offers Create, or nothing on a read-only scope.
                if (hit.RowIndex < 0)
                {
                    cf.CreateForEmptyArea(grid)?.Show(grid, e.Location);
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
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Rows[e.RowIndex].DataBoundItem is not EnvModel pm)
                    return;

                GridStyleHelper.ApplyStateFormatting(e, pm);

                // The indicator column is unbound: render the state glyph from the model.
                if (grid.Columns[e.ColumnIndex].Name == StateIndicatorColumnName)
                {
                    e.Value = GridStyleHelper.GetStateIndicator(pm);
                    e.FormattingApplied = true;
                }
            };

        return grid;
    }

    /// <summary>
    /// Marks or clears the corrupted-path overlay of an environmental variable without touching its edit state.
    /// </summary>
    /// <param name="env">The model to validate.</param>
    public void MarkIfCorrupted(EnvModel env) => env.IsCorrupted = ValidationHelper.IsCorrupted(env.Path);

    /// <summary>
    /// Re-evaluates the corrupted-path overlay for every variable in both scopes, or clears it
    /// when the option is off.
    /// </summary>
    public void RescanAll()
    {
        if (!_cs.Config.EnableCorruptedValidation)
        {
            foreach (var env in ps.GetUserVariables()) env.IsCorrupted = false;
            foreach (var env in ps.GetMachineVariables()) env.IsCorrupted = false;
            return;
        }

        foreach (var env in ps.GetUserVariables()) MarkIfCorrupted(env);
        foreach (var env in ps.GetMachineVariables()) MarkIfCorrupted(env);
    }
}
