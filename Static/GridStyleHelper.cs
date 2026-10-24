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
    /// Applies colour, selection colour and font formatting to a DataGridView cell based on the
    /// environmental variable's state and corrupted-path overlay.
    /// Added: light green, Modified: light yellow, Deleted: light coral with strikeout,
    /// Unchanged: white, Corrupted: red (overrides the state background).
    /// The selected row keeps a recognisable matching shade of the same colour with black text.
    /// </summary>
    /// <param name="e">The cell formatting event arguments.</param>
    /// <param name="model">The environmental variable model associated with the cell.</param>
    public static void ApplyStateFormatting(DataGridViewCellFormattingEventArgs e, EnvModel model)
    {
        var colors = GetStateColors(model);

        e.CellStyle.BackColor = colors.Background;
        e.CellStyle.SelectionBackColor = colors.SelectionBackground;
        e.CellStyle.SelectionForeColor = Color.Black;

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
    /// Maps a model to the pair of backgrounds used for normal and selected cells.
    /// Both shades live here so they cannot drift apart.
    /// </summary>
    private static StateColors GetStateColors(EnvModel model)
    {
        if (model.IsCorrupted)
            return new(Color.Red, Color.FromArgb(230, 96, 96));

        return model.State switch
        {
            EnvironmentalVariableState.Added => new(Color.LightGreen, Color.FromArgb(112, 199, 112)),
            EnvironmentalVariableState.Modified => new(Color.LightYellow, Color.FromArgb(229, 214, 128)),
            EnvironmentalVariableState.Deleted => new(Color.LightCoral, Color.FromArgb(222, 133, 133)),
            _ => new(Color.White, Color.FromArgb(214, 214, 214))
        };
    }

    /// <summary>
    /// The background and selection background for one row state.
    /// </summary>
    private readonly record struct StateColors(Color Background, Color SelectionBackground);

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
