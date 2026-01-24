using enviro.Models;
using System.ComponentModel;
using System.Data;

namespace enviro.Static;

/// <summary>
/// Static helper class for applying visual styles to DataGridView cells based on environmental variable state.
/// </summary>
internal static class GridStyleHelper
{
    /// <summary>
    /// Applies color and font formatting to a DataGridView cell based on the environmental variable's state.
    /// Added: Light green background
    /// Modified: Light yellow background
    /// Deleted: Light coral background with strikeout text
    /// Unchanged: White background
    /// Corrupted: Red background
    /// </summary>
    /// <param name="e">The cell formatting event arguments.</param>
    /// <param name="model">The environmental variable model associated with the cell.</param>
    public static void ApplyStateFormatting(DataGridViewCellFormattingEventArgs e, EnvModel model)
    {
        switch (model.State)
        {
            case EnvironmentalVariableState.Added:
                e.CellStyle.BackColor = Color.LightGreen;
                break;
            case EnvironmentalVariableState.Modified:
                e.CellStyle.BackColor = Color.LightYellow;
                break;
            case EnvironmentalVariableState.Deleted:
                e.CellStyle.BackColor = Color.LightCoral;
                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Strikeout);
                break;
            case EnvironmentalVariableState.Unchanged:
                e.CellStyle.BackColor = Color.White;
                break;
            case EnvironmentalVariableState.Corrupted:
                e.CellStyle.BackColor = Color.Red;
                break;
        }
    }

    /// <summary>
    /// Sorts the specified DataGridView by the given column, toggling between ascending and descending order, and
    /// updates the sort glyph accordingly.
    /// </summary>
    /// <param name="grid">The DataGridView to be sorted. Must not be null and must contain the column specified by columnIndex.</param>
    /// <param name="bindingSource">The BindingSource providing the data for the DataGridView. Must contain a BindingList of EnvModel objects.</param>
    /// <param name="columnIndex">The zero-based index of the column to sort by. Must refer to a valid column in the grid.</param>
    public static void SortGrid(DataGridView grid, BindingSource bindingSource, int columnIndex)
    {
        var column = grid.Columns[columnIndex];
        var propertyName = column.DataPropertyName;

        if (string.IsNullOrEmpty(propertyName)) return;

        var sortOrder = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending
            ? SortOrder.Descending
            : SortOrder.Ascending;

        var currentList = (BindingList<EnvModel>)bindingSource.List;
        var sortedList = sortOrder == SortOrder.Ascending
            ? currentList.OrderBy(m => typeof(EnvModel).GetProperty(propertyName)?.GetValue(m)).ToList()
            : currentList.OrderByDescending(m => typeof(EnvModel).GetProperty(propertyName)?.GetValue(m)).ToList();

        currentList.Clear();
        foreach (var item in sortedList)
            currentList.Add(item);

        foreach (DataGridViewColumn col in grid.Columns)
            col.HeaderCell.SortGlyphDirection = SortOrder.None;

        column.HeaderCell.SortGlyphDirection = sortOrder;
    }
}
