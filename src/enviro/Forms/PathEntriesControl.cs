using enviro.Models;
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

    /// <summary>Segments of the live registry value while the variable is drifted; null when it is not.</summary>
    private HashSet<string>? _liveSegments;

    /// <summary>True when the variable was deleted outside enviro, so every segment is drifted.</summary>
    private bool _driftAll;

    private const int UndoLimit = 100;

    private readonly Stack<SegmentDelta> _undo = [];
    private readonly Stack<SegmentDelta> _redo = [];

    /// <summary>
    /// The cell being edited and its value at edit start, so a committed edit records one delta.
    /// </summary>
    private (int Row, string OldValue)? _pendingEdit;

    /// <summary>The row a row drag is currently hovering as its drop target, or -1.</summary>
    private int _dragTargetRow = -1;

    private enum SegmentDeltaKind { Add, Remove, Move, Edit }

    /// <summary>
    /// One segment-list change as a delta (kind + affected indexes + old/new value), so an undo
    /// step costs O(1) instead of copying the whole list. <c>Target</c> is the swap partner of a
    /// <see cref="SegmentDeltaKind.Move"/>.
    /// </summary>
    private sealed record SegmentDelta(SegmentDeltaKind Kind, int Index, int Target, string? OldValue, string? NewValue);

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
        GridStyleHelper.ApplyGridTheme(segmentsGrid);

        segmentsGrid.SelectionChanged += (_, _) => UpdateActionState();
        segmentsGrid.CellBeginEdit += GridCellBeginEdit;
        segmentsGrid.CellValueChanged += GridCellValueChanged;
        segmentsGrid.CellEndEdit += GridCellEndEdit;

        // Keyboard (Ctrl+Up/Ctrl+Down) and drag reorder; the same MoveRow path as the buttons.
        segmentsGrid.AllowDrop = true;
        segmentsGrid.KeyDown += GridKeyDown;
        segmentsGrid.MouseDown += GridMouseDown;
        segmentsGrid.DragEnter += GridDragOver;
        segmentsGrid.DragOver += GridDragOver;
        segmentsGrid.DragDrop += GridDragDrop;

        // Drift overlay: a segment the live registry no longer holds is painted with the drifted
        // row colour, so an external edit reads directly in the segment list.
        segmentsGrid.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || (!_driftAll && _liveSegments is null))
                return;

            var value = segmentsGrid.Rows[e.RowIndex].Cells[valueColumn.Index].Value?.ToString()?.Trim() ?? string.Empty;

            if (_driftAll || !_liveSegments!.Contains(value))
            {
                var drift = ThemePalette.Row(EnvironmentalVariableState.Unchanged, isCorrupted: false, isDrifted: true);
                e.CellStyle.BackColor = drift.Background;
                e.CellStyle.SelectionBackColor = drift.SelectionBackground;
            }
        };

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
    /// Sets the drift overlay from the bound row: when the variable was changed outside enviro,
    /// segments the live registry value no longer contains are highlighted; when it was deleted
    /// outside enviro, every segment is. Clears the overlay when the row is not drifted.
    /// </summary>
    /// <param name="drifted">Whether the row's drift flag is set.</param>
    /// <param name="liveValue">The value the registry currently holds, or null when it holds none.</param>
    public void SetDrift(bool drifted, string? liveValue)
    {
        _driftAll = drifted && liveValue is null;
        _liveSegments = drifted && liveValue is not null
            ? new HashSet<string>(
                ValidationHelper.SplitPathList(liveValue).Select(segment => segment.Trim()),
                StringComparer.OrdinalIgnoreCase)
            : null;

        segmentsGrid.Invalidate();
    }

    /// <summary>
    /// Replaces the list with the non-blank segments of a value. Does not raise
    /// <see cref="ValueChanged"/>.
    /// </summary>
    /// <param name="value">The <c>;</c>-separated value to load.</param>
    public void LoadValue(string value)
    {
        _undo.Clear();
        _redo.Clear();
        _pendingEdit = null;

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

    /// <summary>Whether a segment-list change can be undone inside this control.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether an undone segment-list change can be redone.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Whether a grid cell is in edit mode; while it is, Ctrl+Z keeps its native text-undo meaning.</summary>
    public bool IsEditing => segmentsGrid.IsCurrentCellInEditMode;

    /// <summary>
    /// Reverts the last segment-list change (add, remove, move, committed cell edit).
    /// </summary>
    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var delta = _undo.Pop();
        _redo.Push(delta);
        Apply(delta, forward: false);
    }

    /// <summary>
    /// Reapplies the last undone segment-list change.
    /// </summary>
    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        var delta = _redo.Pop();
        _undo.Push(delta);
        Apply(delta, forward: true);
    }

    /// <summary>
    /// Applies one delta forward (redo) or in inverse (undo) and notifies the host once.
    /// </summary>
    private void Apply(SegmentDelta delta, bool forward)
    {
        // Programmatic cell writes raise CellValueChanged, which would notify the host once per
        // cell (and mid-swap for a Move); suppress those and notify exactly once at the end.
        _suppressChange = true;

        try
        {
            switch (delta.Kind)
            {
                case SegmentDeltaKind.Add:
                    if (forward) InsertRow(delta.Index, delta.NewValue);
                    else RemoveRow(delta.Index);
                    break;

                case SegmentDeltaKind.Remove:
                    if (forward) RemoveRow(delta.Index);
                    else InsertRow(delta.Index, delta.OldValue);
                    break;

                case SegmentDeltaKind.Move:
                    SwapRows(delta.Index, delta.Target);
                    break;

                case SegmentDeltaKind.Edit:
                    SetValue(delta.Index, forward ? delta.NewValue : delta.OldValue);
                    break;
            }
        }
        finally
        {
            _suppressChange = false;
        }

        NotifyValueChanged();
    }

    private void Push(SegmentDelta delta)
    {
        _undo.Push(delta);

        if (_undo.Count > UndoLimit)
            TrimOldest(_undo);

        _redo.Clear();
    }

    /// <summary>
    /// Drops the oldest entry of a stack that grew past the limit. A stack has no random access,
    /// so trimming the bottom rebuilds it; <see cref="Stack{T}.ToArray"/> copies newest first.
    /// </summary>
    /// <param name="stack">The over-limit stack.</param>
    private static void TrimOldest(Stack<SegmentDelta> stack)
    {
        var entries = stack.ToArray();
        stack.Clear();

        for (var i = entries.Length - 2; i >= 0; i--)
            stack.Push(entries[i]);
    }

    private void InsertRow(int index, string? value)
    {
        index = Math.Clamp(index, 0, segmentsGrid.Rows.Count);
        segmentsGrid.Rows.Insert(index, value ?? string.Empty, string.Empty);
        UpdateExistence(index);
        SelectRow(index);
    }

    private void RemoveRow(int index)
    {
        if (index < 0 || index >= segmentsGrid.Rows.Count)
            return;

        segmentsGrid.Rows.RemoveAt(index);
        SelectRow(Math.Min(index, segmentsGrid.Rows.Count - 1));
    }

    private void SwapRows(int a, int b)
    {
        var moving = segmentsGrid.Rows[a].Cells[valueColumn.Index].Value;
        segmentsGrid.Rows[a].Cells[valueColumn.Index].Value = segmentsGrid.Rows[b].Cells[valueColumn.Index].Value;
        segmentsGrid.Rows[b].Cells[valueColumn.Index].Value = moving;

        UpdateExistence(a);
        UpdateExistence(b);

        SelectRow(b);
    }

    private void SetValue(int index, string? value)
    {
        if (index < 0 || index >= segmentsGrid.Rows.Count)
            return;

        segmentsGrid.Rows[index].Cells[valueColumn.Index].Value = value;
        UpdateExistence(index);
        SelectRow(index);
    }

    private void SelectRow(int index)
    {
        if (index >= 0 && index < segmentsGrid.Rows.Count)
            segmentsGrid.CurrentCell = segmentsGrid.Rows[index].Cells[valueColumn.Index];
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
        var verdict = _corruption?.ValidateSegment(value) ?? PathSegmentState.NotPath;

        switch (verdict)
        {
            case PathSegmentState.Exists:
                cell.Value = ExistsText;
                break;

            case PathSegmentState.Missing:
                cell.Value = MissingText;
                break;

            default:
                cell.Value = NotPathText;
                break;
        }

        cell.Style.ForeColor = ThemePalette.SegmentText(verdict);
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

    private void GridCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (e.ColumnIndex == valueColumn.Index && e.RowIndex >= 0)
            _pendingEdit = (e.RowIndex, segmentsGrid.Rows[e.RowIndex].Cells[valueColumn.Index].Value?.ToString() ?? string.Empty);
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != valueColumn.Index)
            return;

        if (_pendingEdit is { } pending && pending.Row == e.RowIndex)
        {
            var newValue = segmentsGrid.Rows[e.RowIndex].Cells[valueColumn.Index].Value?.ToString() ?? string.Empty;

            if (pending.OldValue != newValue)
                Push(new(SegmentDeltaKind.Edit, e.RowIndex, -1, pending.OldValue, newValue));

            _pendingEdit = null;
        }

        UpdateExistence(e.RowIndex);
        NotifyValueChanged();
    }

    private void GridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        _pendingEdit = null;

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

        var index = segmentsGrid.Rows.Count;
        Push(new(SegmentDeltaKind.Add, index, -1, null, dialog.FileName));
        AddRow(dialog.FileName);
        segmentsGrid.CurrentCell = segmentsGrid.Rows[index].Cells[valueColumn.Index];
        NotifyValueChanged();
    }

    private void RemoveButtonClicked(object? sender, EventArgs e)
    {
        var index = segmentsGrid.CurrentRow?.Index ?? -1;

        if (index < 0)
            return;

        Push(new(SegmentDeltaKind.Remove, index, -1,
            segmentsGrid.Rows[index].Cells[valueColumn.Index].Value?.ToString() ?? string.Empty, null));
        RemoveRow(index);
        NotifyValueChanged();
    }

    private void MoveUpButtonClicked(object? sender, EventArgs e) => MoveCurrentRow(-1);

    private void MoveDownButtonClicked(object? sender, EventArgs e) => MoveCurrentRow(1);

    /// <summary>
    /// Ctrl+Up / Ctrl+Down reorder the current entry; plain Up/Down keep native row navigation,
    /// and while a cell editor is open the keys keep their native meaning (see <see cref="IsEditing"/>).
    /// </summary>
    private void GridKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || IsEditing || (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down))
            return;

        MoveCurrentRow(e.KeyCode == Keys.Up ? -1 : 1);
        e.Handled = true;
    }

    /// <summary>
    /// Starts a row drag (left button over a data row, not while editing).
    /// </summary>
    private void GridMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || IsEditing)
            return;

        var hit = segmentsGrid.HitTest(e.X, e.Y);

        if (hit.RowIndex >= 0)
            _ = segmentsGrid.DoDragDrop(hit.RowIndex, DragDropEffects.Move);
    }

    /// <summary>
    /// Drag-over feedback: a pressed row index is a move; the hovered row is the drop target.
    /// </summary>
    private void GridDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(int)) is not true || IsEditing)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        var client = segmentsGrid.PointToClient(new Point(e.X, e.Y));
        var hit = segmentsGrid.HitTest(client.X, client.Y);
        _dragTargetRow = hit.RowIndex >= 0 ? hit.RowIndex : segmentsGrid.Rows.Count - 1;
        e.Effect = DragDropEffects.Move;
    }

    /// <summary>
    /// Drops the pressed row on the last hovered row as an undoable move.
    /// </summary>
    private void GridDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(int)) is not int from || IsEditing)
            return;

        var target = _dragTargetRow;
        _dragTargetRow = -1;

        MoveRow(from, target);
    }

    /// <summary>
    /// Swaps the current entry with its neighbour, keeping the moved entry selected.
    /// </summary>
    /// <param name="offset">-1 to move up, 1 to move down.</param>
    private void MoveCurrentRow(int offset)
    {
        var index = segmentsGrid.CurrentRow?.Index ?? -1;

        if (index < 0)
            return;

        MoveRow(index, index + offset);
    }

    /// <summary>
    /// Moves one entry to a new position as an undoable Move delta, keeping the moved entry
    /// selected. Equal or out-of-bounds indexes are a no-op.
    /// </summary>
    /// <param name="from">The row to move.</param>
    /// <param name="to">The row to swap it with (the new position).</param>
    private void MoveRow(int from, int to)
    {
        if (from < 0 || to < 0 || from == to
            || from >= segmentsGrid.Rows.Count || to >= segmentsGrid.Rows.Count)
            return;

        Push(new(SegmentDeltaKind.Move, from, to, null, null));
        SwapRows(from, to);
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
