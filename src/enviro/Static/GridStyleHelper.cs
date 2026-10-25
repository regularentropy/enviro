using enviro.Models;
using enviro.Services;
using System.ComponentModel;
using System.Data;

namespace enviro.Static;

/// <summary>
/// Static helper class for applying visual styles to DataGridView cells based on environmental variable state.
/// </summary>
internal static class GridStyleHelper
{
    /// <summary>
    /// Applies the active palette to a grid's chrome: the empty-area background, the grid lines,
    /// the default cell text, and the row/column headers.
    /// </summary>
    /// <remarks>
    /// Windows paints grid headers from the visual style, which ignores the dark colour mode. The
    /// dark palette therefore turns that painter off and supplies the header colours itself; the
    /// light palette leaves it on, so light mode looks exactly as it did before the theme work.
    /// The per-row backgrounds are <see cref="ApplyStateFormatting"/>'s job.
    /// </remarks>
    /// <param name="grid">The grid to theme.</param>
    public static void ApplyGridTheme(DataGridView grid)
    {
        grid.BackgroundColor = ThemePalette.GridBackground;
        grid.GridColor = ThemePalette.GridLines;

        grid.DefaultCellStyle.BackColor = ThemePalette.GridBackground;
        grid.DefaultCellStyle.ForeColor = ThemePalette.CellText;

        grid.EnableHeadersVisualStyles = ThemePalette.SystemHeaders;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemePalette.HeaderBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemePalette.HeaderText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemePalette.HeaderBackground;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = ThemePalette.HeaderText;
        grid.RowHeadersDefaultCellStyle.BackColor = ThemePalette.HeaderBackground;
        grid.RowHeadersDefaultCellStyle.ForeColor = ThemePalette.HeaderText;
    }

    /// <summary>
    /// Applies colour, selection colour and font formatting to a DataGridView cell based on the
    /// environmental variable's state and corrupted-path overlay.
    /// Every colour comes from <see cref="ThemePalette"/>, so the same row reads in light and dark
    /// mode; a deleted row is struck through and the corrupted overlay overrides the state background.
    /// The selected row keeps a recognisable matching shade of the same colour, with text chosen for
    /// contrast against it.
    /// </summary>
    /// <param name="e">The cell formatting event arguments.</param>
    /// <param name="model">The environmental variable model associated with the cell.</param>
    public static void ApplyStateFormatting(DataGridViewCellFormattingEventArgs e, EnvModel model)
    {
        var colors = ThemePalette.Row(model.State, model.IsCorrupted);

        e.CellStyle.BackColor = colors.Background;
        e.CellStyle.SelectionBackColor = colors.SelectionBackground;
        e.CellStyle.SelectionForeColor = ThemePalette.SelectionText;

        if (model.State == EnvironmentalVariableState.Deleted)
            e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Strikeout);
    }

    /// <summary>
    /// Returns the single character that conveys the row's state in the indicator column.
    /// </summary>
    /// <param name="model">The environmental variable model for the row.</param>
    /// <returns>
    /// <c>!</c> for a corrupted value, otherwise <c>+</c> added, <c>~</c> modified,
    /// <c>–</c> deleted, or an empty string for an unchanged row.
    /// </returns>
    public static string GetStateIndicator(EnvModel model) =>
        model.IsCorrupted
            ? "!"
            : model.State switch
            {
                EnvironmentalVariableState.Added => "+",
                EnvironmentalVariableState.Modified => "~",
                EnvironmentalVariableState.Deleted => "–",
                _ => string.Empty
            };

    /// <summary>
    /// Sorts the specified DataGridView by the given column, toggling between ascending and descending order, and
    /// updates the sort glyph accordingly. The refill belongs to the scope, so the selected model and the first
    /// displayed row survive it.
    /// </summary>
    /// <param name="grid">The DataGridView to be sorted. Must not be null and must contain the column specified by columnIndex.</param>
    /// <param name="scope">The scope whose rows the grid displays.</param>
    /// <param name="columnIndex">The zero-based index of the column to sort by. Must refer to a valid column in the grid.</param>
    public static void SortGrid(DataGridView grid, IEnvScope scope, int columnIndex)
    {
        var column = grid.Columns[columnIndex];
        var propertyName = column.DataPropertyName;

        if (string.IsNullOrEmpty(propertyName)) return;

        var sortOrder = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending
            ? SortOrder.Descending
            : SortOrder.Ascending;

        // Capture the user's place before the bound list is cleared and refilled.
        var selectedModel = grid.CurrentRow?.DataBoundItem as EnvModel;
        var selectedColumn = grid.CurrentCell?.ColumnIndex ?? 0;
        var firstDisplayedRow = grid.FirstDisplayedScrollingRowIndex;

        scope.Sort(
            propertyName,
            sortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);

        foreach (DataGridViewColumn col in grid.Columns)
            col.HeaderCell.SortGlyphDirection = SortOrder.None;

        column.HeaderCell.SortGlyphDirection = sortOrder;

        RestorePosition(grid, selectedModel, selectedColumn, firstDisplayedRow);
    }

    /// <summary>
    /// Re-selects the model that was selected before sorting and restores the scroll position when
    /// both are still present. Rows hidden by a filter are skipped.
    /// </summary>
    /// <param name="grid">The grid that was refilled.</param>
    /// <param name="selectedModel">The model that was selected before sorting, if any.</param>
    /// <param name="columnIndex">The column index of the cell that had focus.</param>
    /// <param name="firstDisplayedRow">The first displayed row index before sorting; negative when unknown.</param>
    private static void RestorePosition(DataGridView grid, EnvModel? selectedModel, int columnIndex, int firstDisplayedRow)
    {
        if (selectedModel is not null && grid.Rows.Count > 0 && grid.Columns.Count > 0)
        {
            var rowIndex = FindRow(grid, selectedModel);

            if (rowIndex >= 0 && grid.Rows[rowIndex].Visible)
            {
                grid.CurrentCell = grid[Math.Clamp(columnIndex, 0, grid.Columns.Count - 1), rowIndex];
                grid.Rows[rowIndex].Selected = true;
            }
        }

        if (firstDisplayedRow >= 0 && firstDisplayedRow < grid.Rows.Count && grid.Rows[firstDisplayedRow].Visible)
            grid.FirstDisplayedScrollingRowIndex = firstDisplayedRow;
    }

    /// <summary>
    /// Finds the row currently bound to the given model by reference.
    /// </summary>
    /// <param name="grid">The grid to search.</param>
    /// <param name="model">The model instance to find.</param>
    /// <returns>The row index, or -1 when the model is no longer in the grid.</returns>
    private static int FindRow(DataGridView grid, EnvModel model)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (ReferenceEquals(row.DataBoundItem, model))
                return row.Index;
        }

        return -1;
    }
}
