using enviro.Services;
using enviro.Static;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Diagnostics;

namespace enviro.Forms;

/// <summary>
/// Ordered list of path entries with per-entry validation, hosted inside the edit window.
/// </summary>
internal partial class PathEntriesControl : UserControl
{
    private const string MissingText = "Missing";
    private const string ExistsText = "Exists";
    private const string NotPathText = "Not a path";

    private readonly ICorruptedPathService? _corruption;

    private bool _suppressChange;

    /// <summary>
    /// Raised after the user changes the list, so the host can update its value string.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Design-time constructor: without the service the existence column reports "Not a path"
    /// and the Open action is refused.
    /// </summary>
    public PathEntriesControl() : this(null) { }

    /// <param name="corruption">The service that classifies a segment; optional for the designer.</param>
    public PathEntriesControl(ICorruptedPathService? corruption)
    {
        _corruption = corruption;

        InitializeComponent();

        // Follow the font/DPI size like the main grid instead of a fixed pixel height.
        segmentsGrid.RowTemplate.Height = segmentsGrid.Font.Height + 12;

        segmentsGrid.SelectionChanged += (_, _) => UpdateActionState();
        segmentsGrid.CellValueChanged += GridCellValueChanged;
        segmentsGrid.CellEndEdit += GridCellEndEdit;

        UpdateActionState();
    }

    /// <summary>
    /// The list joined with <c>;</c>, dropping blank entries.
    /// </summary>
    public string JoinedValue =>
        ValidationHelper.JoinPathList(segmentsGrid.Rows
            .Cast<DataGridViewRow>()
            .Select(row => row.Cells[valueColumn.Index].Value?.ToString()?.Trim() ?? string.Empty));

    /// <summary>
    /// Replaces the list with the non-blank segments of a value. Does not raise
    /// <see cref="ValueChanged"/>.
    /// </summary>
    /// <param name="value">The <c>;</c>-separated value to load.</param>
    public void LoadValue(string value)
    {
        _suppressChange = true;

        try
        {
            segmentsGrid.Rows.Clear();

            foreach (var segment in ValidationHelper.SplitPathList(value))
                AddRow(segment.Trim());

            if (segmentsGrid.Rows.Count > 0)
                segmentsGrid.CurrentCell = segmentsGrid.Rows[0].Cells[valueColumn.Index];
        }
        finally
        {
            _suppressChange = false;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Adds one entry row and fills its existence indicator.
    /// </summary>
    /// <param name="value">The segment to add.</param>
    /// <returns>The index of the new row.</returns>
    private int AddRow(string value)
    {
        var index = segmentsGrid.Rows.Add(value, string.Empty);
        UpdateExistence(index);
        return index;
    }

    /// <summary>
    /// Recomputes the existence indicator for one row.
    /// </summary>
    /// <param name="rowIndex">The row whose value changed.</param>
    private void UpdateExistence(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= segmentsGrid.Rows.Count)
            return;

        var row = segmentsGrid.Rows[rowIndex];
        var value = row.Cells[valueColumn.Index].Value?.ToString() ?? string.Empty;
        var cell = row.Cells[existenceColumn.Index];

        switch (_corruption?.ValidateSegment(value) ?? PathSegmentState.NotPath)
        {
            case PathSegmentState.Exists:
                cell.Value = ExistsText;
                cell.Style.ForeColor = Color.ForestGreen;
                break;

            case PathSegmentState.Missing:
                cell.Value = MissingText;
                cell.Style.ForeColor = Color.Firebrick;
                break;

            default:
                cell.Value = NotPathText;
                cell.Style.ForeColor = SystemColors.GrayText;
                break;
        }
    }

    /// <summary>
    /// Enables the row actions for the current selection and position.
    /// </summary>
    private void UpdateActionState()
    {
        var index = segmentsGrid.CurrentRow?.Index ?? -1;

        removeButton.Enabled = index >= 0;
        moveUpButton.Enabled = index > 0;
        moveDownButton.Enabled = index >= 0 && index < segmentsGrid.Rows.Count - 1;
        openButton.Enabled = TryGetExistingTarget(out _);
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != valueColumn.Index)
            return;

        UpdateExistence(e.RowIndex);
        NotifyValueChanged();
    }

    private void GridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex == valueColumn.Index)
            UpdateExistence(e.RowIndex);
    }

    /// <summary>
    /// Tells the host that the joined value may have changed.
    /// </summary>
    private void NotifyValueChanged()
    {
        UpdateActionState();

        if (!_suppressChange)
            ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Picks a folder and appends it as a new segment.
    /// </summary>
    private void AddButtonClicked(object? sender, EventArgs e)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            return;

        var index = AddRow(dialog.FileName);
        segmentsGrid.CurrentCell = segmentsGrid.Rows[index].Cells[valueColumn.Index];
        NotifyValueChanged();
    }

    private void RemoveButtonClicked(object? sender, EventArgs e)
    {
        var index = segmentsGrid.CurrentRow?.Index ?? -1;

        if (index < 0)
            return;

        segmentsGrid.Rows.RemoveAt(index);

        if (segmentsGrid.Rows.Count > 0)
            segmentsGrid.CurrentCell = segmentsGrid.Rows[Math.Min(index, segmentsGrid.Rows.Count - 1)].Cells[valueColumn.Index];

        NotifyValueChanged();
    }

    private void MoveUpButtonClicked(object? sender, EventArgs e) => MoveCurrentRow(-1);

    private void MoveDownButtonClicked(object? sender, EventArgs e) => MoveCurrentRow(1);

    /// <summary>
    /// Swaps the current entry with its neighbour, keeping the moved entry selected.
    /// </summary>
    /// <param name="offset">-1 to move up, 1 to move down.</param>
    private void MoveCurrentRow(int offset)
    {
        var index = segmentsGrid.CurrentRow?.Index ?? -1;
        var target = index + offset;

        if (index < 0 || target < 0 || target >= segmentsGrid.Rows.Count)
            return;

        var moving = segmentsGrid.Rows[index].Cells[valueColumn.Index].Value;
        segmentsGrid.Rows[index].Cells[valueColumn.Index].Value = segmentsGrid.Rows[target].Cells[valueColumn.Index].Value;
        segmentsGrid.Rows[target].Cells[valueColumn.Index].Value = moving;

        UpdateExistence(index);
        UpdateExistence(target);

        segmentsGrid.CurrentCell = segmentsGrid.Rows[target].Cells[valueColumn.Index];
        NotifyValueChanged();
    }

    /// <summary>
    /// Opens the selected existing segment in Explorer (folders are opened, files are selected there).
    /// </summary>
    private void OpenButtonClicked(object? sender, EventArgs e)
    {
        segmentsGrid.EndEdit();

        if (!TryGetExistingTarget(out var target))
            return;

        if (ValidationHelper.IsExistingDirectory(target))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });

            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{target}\"",
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Resolves the selected segment to an existing location for the Open action. The service verdict
    /// decides whether it exists; <see cref="ValidationHelper.IsExistingDirectory"/> decides what it is.
    /// </summary>
    /// <param name="target">The expanded, existing path.</param>
    /// <returns>True when the selected segment exists.</returns>
    private bool TryGetExistingTarget(out string target)
    {
        target = string.Empty;

        var value = segmentsGrid.CurrentRow?.Cells[valueColumn.Index].Value?.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(value) || _corruption?.ValidateSegment(value) != PathSegmentState.Exists)
            return false;

        target = Environment.ExpandEnvironmentVariables(value);
        return true;
    }
}
