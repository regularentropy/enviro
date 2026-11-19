using enviro.Models;
using enviro.Services;
using enviro.Static;
using System.ComponentModel;
using System.Text;

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
    /// Connects a filter box and a match-count label to the grid. The grid hides rows whose name
    /// and value do not contain the filter text (case-insensitive), re-applies the filter after
    /// every list change and sort, and writes "N of M shown" (or "No matches") onto the label.
    /// </summary>
    /// <param name="grid">The grid to filter.</param>
    /// <param name="filter">The text box that supplies the filter text.</param>
    /// <param name="count">The label that shows how many rows the current filter keeps visible.</param>
    void AttachFilter(DataGridView grid, TextBox filter, Label count);

    /// <summary>
    /// The filter box connected to a grid.
    /// </summary>
    /// <param name="grid">The grid whose filter box should be returned.</param>
    /// <returns>The filter box attached through <see cref="AttachFilter"/>, or null when there is none.</returns>
    TextBox? FilterOf(DataGridView grid);

    /// <summary>
    /// Clears every attached filter box, showing all rows again.
    /// </summary>
    void ClearFilters();

    /// <summary>
    /// Shows only drifted rows on a grid. Combined with the filter text, so both restrictions can
    /// be active at once.
    /// </summary>
    /// <param name="grid">The grid to filter.</param>
    /// <param name="on">True to show only drifted rows, false to clear the restriction.</param>
    void SetDriftOnly(DataGridView grid, bool on);

    /// <summary>
    /// Whether a grid currently shows drifted rows only.
    /// </summary>
    /// <param name="grid">The grid whose flag should be returned.</param>
    /// <returns>True when the grid shows drifted rows only.</returns>
    bool DriftOnlyOf(DataGridView grid);

    /// <summary>
    /// Applies a persisted sort to a grid without toggling: sorts the scope's rows by the given
    /// property, sets that column's sort glyph and re-applies the filter.
    /// </summary>
    /// <param name="grid">The grid whose sort should be restored.</param>
    /// <param name="propertyName">The bound model property to sort by.</param>
    /// <param name="descending">Whether the sort is descending.</param>
    void RestoreSort(DataGridView grid, string propertyName, bool descending);
}

/// <summary>
/// Factory for creating and configuring DataGridView instances for displaying environmental variables.
/// </summary>
internal sealed class EnvGridFactory : IEnvGridFactory
{
    private const string StateIndicatorColumnName = "StateIndicator";

    private readonly IEnvService env;
    private readonly IContextMenuFactory cf;
    private readonly IActionFactory<EnvModel> af;
    private readonly ICorruptedPathService _corruption;

    // Filter boxes are owned by the tab pages; the grids live for the lifetime of the window.
    private readonly Dictionary<DataGridView, TextBox> _filters = [];

    // The match-count label of each filter row, in the same mapping style as the filter boxes.
    private readonly Dictionary<DataGridView, Label> _filterCounts = [];

    // Session-only drift filter state, per grid, in the same mapping style as the filter boxes.
    private readonly Dictionary<DataGridView, bool> _driftOnly = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvGridFactory"/> class.
    /// </summary>
    /// <param name="envService">The service for managing environmental variables.</param>
    /// <param name="actionFactory">The factory for creating action forms.</param>
    /// <param name="contextMenuFactory">The factory for creating context menus.</param>
    /// <param name="corruption">The service that owns the corrupted-path overlay.</param>
    public EnvGridFactory(IEnvService envService, IActionFactory<EnvModel> actionFactory, IContextMenuFactory contextMenuFactory, ICorruptedPathService corruption)
    {
        env = envService;
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
            MultiSelect = true,
            ScrollBars = ScrollBars.Both,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            AllowUserToResizeRows = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            // Off because the grid's own truncated-value tooltip would show next to the CellMouseEnter
            // tooltip below, which already covers a clipped Name/Value cell.
            ShowCellToolTips = false,
        };

        // The palette owns the grid chrome; the state colours still arrive per cell below.
        GridStyleHelper.ApplyGridTheme(grid);

        // Comfortable row density that follows the font/DPI size instead of a fixed pixel value.
        grid.RowTemplate.Height = grid.Font.Height + 12;

        grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

        grid.AutoGenerateColumns = false;

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
            // Name fixed-width so a long Value cannot squeeze it in Fill mode; Value keeps Fill with a floor.
            new DataGridViewTextBoxColumn() { Name="NameText", HeaderText = "Name", DataPropertyName = nameof(EnvModel.Name), SortMode = DataGridViewColumnSortMode.Automatic, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 240, MinimumWidth = 140 },
            new DataGridViewTextBoxColumn() { Name="PathText", HeaderText = "Value", DataPropertyName = nameof(EnvModel.Path), SortMode = DataGridViewColumnSortMode.Automatic, MinimumWidth = 120 }
        ]);

        var scope = env.GetScope(tab);

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

            if (e.ListChangedType == ListChangedType.ItemChanged)
            {
                // State, IsCorrupted and the drift overlay have no bound column, so DataGridView
                // repaints no cell for them and the row keeps its old colour until something else
                // invalidates the grid. A state change carries no new value, so it asks for no scan
                // either; a reset or restore still reaches the scan through its Name/Path changes.
                // The drift properties never ask for a scan either: DriftService runs on its own
                // triggers, never as a reaction to its own overlay writes.
                if (e.PropertyDescriptor?.Name is nameof(EnvModel.IsCorrupted) or nameof(EnvModel.State)
                    or nameof(EnvModel.IsDrifted) or nameof(EnvModel.LiveValue))
                {
                    grid.Invalidate();
                    return;
                }
            }

            _corruption.RequestScan(scope);
        };

        // Review the rows already bound to this grid; the service ignores the request while
        // validation is off.
        _corruption.RequestScan(scope);

        // Hover tells the row's story: a drifted row shows what the registry holds now, the
        // indicator column explains its glyph, and a clipped name/value cell shows its full text.
        var toolTip = new ToolTip();

        // A PATH value has few spaces to wrap on, so the native tooltip draws it as one screen-wide
        // line; breaking it here keeps the popup a readable block.
        void ShowToolTip(string text) => toolTip.SetToolTip(grid, WrapToolTip(text));

        grid.CellMouseEnter += (s, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].DataBoundItem is not EnvModel model)
            {
                ShowToolTip(string.Empty);
                return;
            }

            if (model.IsDrifted)
            {
                ShowToolTip($"enviro: {model.OriginalPath}\nsystem: {model.LiveValue ?? "(deleted)"}");
                return;
            }

            if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == StateIndicatorColumnName)
            {
                ShowToolTip(DescribeState(model));
                return;
            }

            if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name is "NameText" or "PathText")
            {
                var text = grid.Columns[e.ColumnIndex].Name == "NameText" ? model.Name : model.Path;
                using var g = grid.CreateGraphics();
                ShowToolTip(g.MeasureString(text, grid.Font).Width > grid.Columns[e.ColumnIndex].Width - 8
                    ? text
                    : string.Empty);
                return;
            }

            ShowToolTip(string.Empty);
        };

        grid.CellMouseDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // The tab banner already says a read-only scope is read-only, so a double-click on it
            // stays silent; the scope rule itself is AdminChecker's.
            if (!AdminChecker.CanMutate(grid)) return;

            // The same guarded edit as the toolbar, the context menu and the shortcuts: the scope
            // comes from the grid's tab, so no elevation decision is taken from this factory.
            RowActionHelper.Edit(grid, af);
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

                // A right-click inside the current multi-selection keeps it, so the menu acts
                // on every selected row; outside it collapses to the clicked row.
                if (!grid.SelectedRows.Cast<DataGridViewRow>().Any(r => r.Index == hit.RowIndex))
                    grid.CurrentCell = grid[hit.ColumnIndex, hit.RowIndex];

                var model = (EnvModel)grid.CurrentRow.DataBoundItem;

                var cms = cf.CreateForRow(grid, model);
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
    /// Connects a filter box and its match-count label to the grid and applies the current text immediately.
    /// </summary>
    /// <param name="grid">The grid to filter.</param>
    /// <param name="filter">The text box that supplies the filter text.</param>
    /// <param name="count">The label that shows how many rows the current filter keeps visible.</param>
    public void AttachFilter(DataGridView grid, TextBox filter, Label count)
    {
        _filters[grid] = filter;
        _filterCounts[grid] = count;
        filter.TextChanged += (_, _) => ApplyFilter(grid);
    }

    /// <summary>
    /// The filter box connected to a grid; the mapping is owned here, so the shell never has to
    /// find the box by control type.
    /// </summary>
    /// <param name="grid">The grid whose filter box should be returned.</param>
    /// <returns>The filter box attached through <see cref="AttachFilter"/>, or null when there is none.</returns>
    public TextBox? FilterOf(DataGridView grid) => _filters.GetValueOrDefault(grid);

    /// <summary>
    /// Clears every attached filter box, showing all rows again.
    /// </summary>
    public void ClearFilters()
    {
        foreach (var grid in _filters.Keys)
            FilterOf(grid)?.Clear();
    }

    /// <inheritdoc/>
    public void SetDriftOnly(DataGridView grid, bool on)
    {
        _driftOnly[grid] = on;
        ApplyFilter(grid);
    }

    /// <inheritdoc/>
    public bool DriftOnlyOf(DataGridView grid) => _driftOnly.GetValueOrDefault(grid);

    /// <inheritdoc/>
    public void RestoreSort(DataGridView grid, string propertyName, bool descending)
    {
        var column = grid.Columns.Cast<DataGridViewColumn>()
            .FirstOrDefault(c => c.DataPropertyName == propertyName);

        if (column is null || ControlHelper.GetTabType(grid) is not { } type)
            return;

        env.GetScope(type).Sort(
            propertyName,
            descending ? ListSortDirection.Descending : ListSortDirection.Ascending);

        foreach (DataGridViewColumn col in grid.Columns)
            col.HeaderCell.SortGlyphDirection = SortOrder.None;

        column.HeaderCell.SortGlyphDirection = descending ? SortOrder.Descending : SortOrder.Ascending;

        ApplyFilter(grid);
    }

    /// <summary>
    /// Hides the rows the filter text and the drift-only flag exclude. The bound list itself is
    /// never touched, so sorting and change tracking are unaffected. The selection is kept on a
    /// visible row; when nothing matches, the current cell is cleared.
    /// </summary>
    /// <param name="grid">The grid whose filter should be applied.</param>
    private void ApplyFilter(DataGridView grid)
    {
        var driftOnly = DriftOnlyOf(grid);
        var text = FilterOf(grid)?.Text.Trim() ?? string.Empty;

        // One predicate for the guard and the loop: drift-only can hide the current row even with
        // an empty text filter (Matches is true for empty text), and hiding the currency manager's
        // row throws.
        bool Visible(EnvModel m) => Matches(m, text) && (!driftOnly || m.IsDrifted);

        if (text.Length == 0 && !driftOnly)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.Visible)
                    row.Visible = true;
            }

            UpdateFilterCount(grid, text, driftOnly);
            return;
        }

        // Clear the current cell first so WinForms does not try to keep a row that is about to be hidden.
        if (grid.CurrentRow is { DataBoundItem: EnvModel current } && !Visible(current))
            grid.CurrentCell = null;

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.DataBoundItem is EnvModel model)
                row.Visible = Visible(model);
        }

        EnsureVisibleSelection(grid);
        UpdateFilterCount(grid, text, driftOnly);
    }

    /// <summary>
    /// Writes the match count onto the filter row's label: empty while no restriction is active,
    /// "No matches" when the filter hides everything, otherwise "N of M shown".
    /// </summary>
    /// <param name="grid">The grid whose count label should be updated.</param>
    /// <param name="text">The trimmed filter text.</param>
    /// <param name="driftOnly">Whether the drift-only restriction is active.</param>
    private void UpdateFilterCount(DataGridView grid, string text, bool driftOnly)
    {
        if (_filterCounts.GetValueOrDefault(grid) is not { } count)
            return;

        if (text.Length == 0 && !driftOnly)
        {
            count.Text = string.Empty;
            return;
        }

        var visible = grid.Rows.Cast<DataGridViewRow>().Count(row => row.Visible);

        count.Text = visible == 0
            ? "No matches"
            : $"{visible} of {grid.Rows.Count} shown";
    }

    /// <summary>
    /// The indicator column's glyph in words, so a row's state reads without the legend tooltip.
    /// </summary>
    /// <param name="model">The row's model.</param>
    /// <returns>The tooltip text, or an empty string for an unchanged, unmarked row.</returns>
    private static string DescribeState(EnvModel model)
    {
        if (model.IsCorrupted)
            return "Corrupted — the value points to a missing location";

        return model.State switch
        {
            EnvironmentalVariableState.Added => "Added — not applied yet",
            EnvironmentalVariableState.Modified => $"Modified — original: {model.OriginalPath}",
            EnvironmentalVariableState.Deleted => "Marked for deletion — not applied yet",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Breaks a long tooltip line at a path separator or space so the native tooltip does not draw
    /// a PATH value as one screen-wide line.
    /// </summary>
    /// <param name="text">The tooltip text, possibly multi-line already.</param>
    /// <param name="width">The maximum characters per line.</param>
    /// <returns>The text with lines no longer than <paramref name="width"/> characters.</returns>
    internal static string WrapToolTip(string text, int width = 64)
    {
        if (text.Length <= width)
            return text;

        var sb = new StringBuilder(text.Length + text.Length / width);

        foreach (var line in text.Split('\n'))
        {
            for (var start = 0; start < line.Length;)
            {
                var take = Math.Min(width, line.Length - start);

                if (start + take < line.Length)
                {
                    // Prefer a separator so a path breaks between segments, not inside one.
                    var cut = line.LastIndexOfAny([';', '\\', '/', ' '], start + take - 1, take);

                    if (cut > start)
                        take = cut - start + 1;
                }

                sb.Append(line, start, take).Append('\n');
                start += take;
            }
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// Checks whether a variable matches the filter text in its name or value. An empty text
    /// matches everything.
    /// </summary>
    /// <param name="model">The variable to test.</param>
    /// <param name="text">The trimmed filter text, possibly empty.</param>
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
