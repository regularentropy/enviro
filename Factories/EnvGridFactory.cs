using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating DataGridView instances for environmental variables.
/// </summary>
internal interface IEnvGridFactory
{
    /// <summary>
    /// Creates a configured DataGridView for the specified environmental variable type.
    /// </summary>
    /// <param name="tab">The type of environmental variable (User or Machine).</param>
    /// <returns>A configured DataGridView instance.</returns>
    DataGridView Create(EnvironmentalVariableType tab);

    /// <summary>
    /// Connects a filter box to the grid. The grid hides rows whose name and value do not contain
    /// the filter text (case-insensitive) and re-applies the filter after every list change and sort.
    /// </summary>
    /// <param name="grid">The grid to filter.</param>
    /// <param name="filter">The text box that supplies the filter text.</param>
    void AttachFilter(DataGridView grid, TextBox filter);

    /// <summary>
    /// Clears every attached filter box, showing all rows again.
    /// </summary>
    void ClearFilters();
}

/// <summary>
/// Factory for creating and configuring DataGridView instances for displaying environmental variables.
/// </summary>
internal sealed class EnvGridFactory : IEnvGridFactory
{
    private const string StateIndicatorColumnName = "StateIndicator";

    private readonly IEnvService ps;
    private readonly IContextMenuFactory cf;
    private readonly IActionFactory<EnvModel> af;
    private readonly ICorruptedPathService _corruption;

    // Filter boxes are owned by the tab pages; the grids live for the lifetime of the window.
    private readonly Dictionary<DataGridView, TextBox> _filters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvGridFactory"/> class.
    /// </summary>
    /// <param name="pathService">The service for managing environmental variables.</param>
    /// <param name="actionFactory">The factory for creating action forms.</param>
    /// <param name="contextMenuFactory">The factory for creating context menus.</param>
    /// <param name="corruption">The service that owns the corrupted-path overlay.</param>
    public EnvGridFactory(IEnvService pathService, IActionFactory<EnvModel> actionFactory, IContextMenuFactory contextMenuFactory, ICorruptedPathService corruption)
    {
        ps = pathService;
        cf = contextMenuFactory;
        af = actionFactory;
        _corruption = corruption;
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

        var scope = ps.GetScope(tab);

        // The grid binds to the service-owned rows, so every change arrives as a ListChanged event.
        var bindingSource = new BindingSource { DataSource = scope.Rows };

        grid.DataSource = bindingSource;

        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            GridStyleHelper.SortGrid(grid, scope, e.ColumnIndex);
            ApplyFilter(grid);
        };

        // Corrupted variable detection: the service scans off the UI thread and debounces, so a
        // burst of changes costs one scan. The subscription is always attached so turning the
        // option on never requires rebuilding the grid.
        bindingSource.ListChanged += (s, e) =>
        {
            // Rows may have been added, removed or reordered; keep the filter applied.
            ApplyFilter(grid);

            // The overlay write is itself a list change; only value changes ask for a new scan.
            if (e.ListChangedType == ListChangedType.ItemChanged
                && e.PropertyDescriptor?.Name == nameof(EnvModel.IsCorrupted))
            {
                // IsCorrupted has no bound column, so DataGridView repaints no cell for it and the
                // row keeps its old colour until something else invalidates the grid.
                grid.Invalidate();
                return;
            }

            _corruption.RequestScan(scope);
        };

        // Review the rows already bound to this grid; the service ignores the request while
        // validation is off.
        _corruption.RequestScan(scope);

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
    /// Connects a filter box to the grid and applies the current text immediately.
    /// </summary>
    /// <param name="grid">The grid to filter.</param>
    /// <param name="filter">The text box that supplies the filter text.</param>
    public void AttachFilter(DataGridView grid, TextBox filter)
    {
        _filters[grid] = filter;
        filter.TextChanged += (_, _) => ApplyFilter(grid);
    }

    /// <summary>
    /// Clears every attached filter box, showing all rows again.
    /// </summary>
    public void ClearFilters()
    {
        foreach (var filter in _filters.Values)
            filter.Clear();
    }

    /// <summary>
    /// Hides the rows whose name and value do not contain the filter text. The bound list itself is
    /// never touched, so sorting and change tracking are unaffected. The selection is kept on a
    /// visible row; when nothing matches, the current cell is cleared.
    /// </summary>
    /// <param name="grid">The grid whose filter should be applied.</param>
    private void ApplyFilter(DataGridView grid)
    {
        if (!_filters.TryGetValue(grid, out var filter))
            return;

        var text = filter.Text.Trim();

        if (text.Length == 0)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.Visible)
                    row.Visible = true;
            }

            return;
        }

        // Clear the current cell first so WinForms does not try to keep a row that is about to be hidden.
        if (grid.CurrentRow is { DataBoundItem: EnvModel current } && !Matches(current, text))
            grid.CurrentCell = null;

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.DataBoundItem is EnvModel model)
                row.Visible = Matches(model, text);
        }

        EnsureVisibleSelection(grid);
    }

    /// <summary>
    /// Checks whether a variable matches the filter text in its name or value.
    /// </summary>
    /// <param name="model">The variable to test.</param>
    /// <param name="text">The trimmed, non-empty filter text.</param>
    /// <returns>True when the name or value contains the text, ignoring case.</returns>
    private static bool Matches(EnvModel model, string text) =>
        model.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || model.Path.Contains(text, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moves the selection to the first visible row when the previous one was filtered out.
    /// </summary>
    /// <param name="grid">The grid whose selection should be repaired.</param>
    private static void EnsureVisibleSelection(DataGridView grid)
    {
        if (grid.CurrentRow is { Visible: true })
            return;

        var firstVisible = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible);

        if (firstVisible is null)
        {
            grid.CurrentCell = null;
            return;
        }

        grid.CurrentCell = firstVisible.Cells[0];
        firstVisible.Selected = true;
    }
}
