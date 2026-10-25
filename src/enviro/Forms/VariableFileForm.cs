using enviro.Services;
using enviro.Static;

namespace enviro.Forms;

/// <summary>
/// Which way the variable file is travelling; decides the wording and which columns apply.
/// </summary>
internal enum VariableFileDirection
{
    /// <summary>
    /// The workspace is written to a file: every entry starts selected, and a cleared entry is
    /// omitted from the file.
    /// </summary>
    Export,

    /// <summary>
    /// The file is merged into the workspace: an overwrite starts unselected, and invalid or
    /// elevation-gated entries cannot be selected.
    /// </summary>
    Import,
}

/// <summary>
/// Modal review of the entries an export or import would carry: one checkable row per variable,
/// grouped by scope, with the import comparison in the status column. It only chooses entries; the
/// caller keeps the merge, the file write and the Apply rule.
/// </summary>
internal sealed partial class VariableFileForm : Form
{
    private readonly IReadOnlyList<VariableFileEntry> _entries;
    private readonly VariableFileDirection _direction;

    // Set while a bulk action rewrites many checkboxes, so the footer is refreshed once.
    private bool _updatingSelection;

    /// <param name="title">The window title, e.g. "Export variables".</param>
    /// <param name="direction">Whether the entries travel to a file or into the workspace.</param>
    /// <param name="entries">The entries to review, in display order.</param>
    public VariableFileForm(string title, VariableFileDirection direction, IReadOnlyList<VariableFileEntry> entries)
    {
        _entries = entries;
        _direction = direction;

        InitializeComponent();

        Text = title;
        acceptButton.Text = direction == VariableFileDirection.Export ? "Export" : "Import";
        introLabel.Text = direction == VariableFileDirection.Export
            ? "Choose the variables to write to the file."
            : "Choose the variables to merge into the workspace.";
        hintLabel.Text = direction == VariableFileDirection.Export
            ? "Every variable starts selected; clear the ones the file should not carry."
            : "Rows listed as Overwrites start unselected. Invalid and machine entries cannot be selected.";
        statusColumn.Visible = direction == VariableFileDirection.Import;
        currentValueColumn.Visible = direction == VariableFileDirection.Import;

        // Follow the font/DPI size like the main grid instead of a fixed pixel height.
        entriesGrid.RowTemplate.Height = entriesGrid.Font.Height + 12;
        GridStyleHelper.ApplyGridTheme(entriesGrid);
        entriesGrid.CurrentCellDirtyStateChanged += GridCurrentCellDirtyStateChanged;
        entriesGrid.CellValueChanged += GridCellValueChanged;

        FillGrid();
        UpdateAcceptState();
    }

    /// <summary>
    /// The entries the user kept, in display order. Empty until the window is accepted.
    /// </summary>
    public IReadOnlyList<VariableFileEntry> SelectedEntries { get; private set; } = [];

    /// <summary>
    /// One grid row per entry, with its comparison and its default selection.
    /// </summary>
    private void FillGrid()
    {
        entriesGrid.Rows.Clear();

        foreach (var entry in _entries)
        {
            var index = entriesGrid.Rows.Add(
                IsSelectedByDefault(entry),
                entry.Scope,
                entry.Name,
                entry.Value,
                entry.CurrentValue ?? string.Empty,
                DescribeStatus(entry.Status));

            var row = entriesGrid.Rows[index];

            row.Tag = entry;
            row.Cells[nameColumn.Index].ToolTipText = entry.Name;
            row.Cells[valueColumn.Index].ToolTipText = entry.Value;
            row.Cells[currentValueColumn.Index].ToolTipText = entry.CurrentValue ?? string.Empty;
            StyleStatus(row.Cells[statusColumn.Index], entry.Status);

            // A skipped or elevation-gated entry is visible but cannot be chosen.
            if (IsSelectable(entry))
                continue;

            row.Cells[selectedColumn.Index].ReadOnly = true;
            row.DefaultCellStyle.ForeColor = ThemePalette.MutedText;
        }
    }

    /// <summary>
    /// Whether the entry starts checked: on export everything does; on import only what changes
    /// nothing (new or identical) is pre-selected, so replacing a value is opt-in.
    /// </summary>
    private bool IsSelectedByDefault(VariableFileEntry entry) =>
        _direction == VariableFileDirection.Export
        || entry.Status is VariableFileEntryStatus.New or VariableFileEntryStatus.Unchanged;

    /// <summary>
    /// Whether the entry may be checked at all. A skipped entry would be discarded by the merge and
    /// an elevation-gated one cannot be written, so both are disabled.
    /// </summary>
    private bool IsSelectable(VariableFileEntry entry) =>
        _direction == VariableFileDirection.Export
        || entry.Status is not (VariableFileEntryStatus.Skipped or VariableFileEntryStatus.MachineSkipped);

    /// <summary>
    /// The status column's text; empty on an export review.
    /// </summary>
    private static string DescribeStatus(VariableFileEntryStatus? status) => status switch
    {
        VariableFileEntryStatus.New => "New",
        VariableFileEntryStatus.Overwrites => "Overwrites",
        VariableFileEntryStatus.Unchanged => "Unchanged",
        VariableFileEntryStatus.Skipped => "Skipped",
        VariableFileEntryStatus.MachineSkipped => "Machine skipped",
        _ => string.Empty,
    };

    /// <summary>
    /// Colours the status the way the main grid colours a change, from the active theme palette:
    /// green for a new row, yellow for one it replaces, the grid background for anything that is
    /// not carried.
    /// </summary>
    /// <param name="cell">The status cell to style.</param>
    /// <param name="status">The entry's status.</param>
    private static void StyleStatus(DataGridViewCell cell, VariableFileEntryStatus? status)
    {
        cell.Style.BackColor = ThemePalette.FileStatusBackground(status);

        if (status is VariableFileEntryStatus.Unchanged
            or VariableFileEntryStatus.Skipped
            or VariableFileEntryStatus.MachineSkipped)
            cell.Style.ForeColor = ThemePalette.MutedText;
    }

    /// <summary>
    /// Commits a checkbox click at once, so <see cref="GridCellValueChanged"/> sees the new value
    /// without the cell entering edit mode.
    /// </summary>
    private void GridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (entriesGrid.IsCurrentCellDirty)
            entriesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }

    /// <summary>
    /// Keeps the footer count and the accept button in step with the checked rows.
    /// </summary>
    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (!_updatingSelection && e.RowIndex >= 0 && e.ColumnIndex == selectedColumn.Index)
            UpdateAcceptState();
    }

    /// <summary>
    /// Checks every entry the window allows to be selected.
    /// </summary>
    private void SelectAllClicked(object? sender, EventArgs e) => SetSelection(selected: true);

    /// <summary>
    /// Clears the selection. Entries that cannot be selected are already clear and stay untouched.
    /// </summary>
    private void SelectNoneClicked(object? sender, EventArgs e) => SetSelection(selected: false);

    /// <summary>
    /// Applies a bulk selection to the selectable rows, then refreshes the footer once.
    /// </summary>
    /// <param name="selected">True to check the rows, false to clear them.</param>
    private void SetSelection(bool selected)
    {
        entriesGrid.EndEdit();
        _updatingSelection = true;

        try
        {
            foreach (var row in entriesGrid.Rows.Cast<DataGridViewRow>())
            {
                if (row.Tag is VariableFileEntry entry && IsSelectable(entry))
                    row.Cells[selectedColumn.Index].Value = selected;
            }
        }
        finally
        {
            _updatingSelection = false;
        }

        UpdateAcceptState();
    }

    /// <summary>
    /// Writes the selection count and refuses to accept a review with nothing selected.
    /// </summary>
    private void UpdateAcceptState()
    {
        var selected = entriesGrid.Rows
            .Cast<DataGridViewRow>()
            .Count(row => row.Cells[selectedColumn.Index].Value is true);

        var selectable = _entries.Count(IsSelectable);

        selectionLabel.Text = selectable == 0
            ? "No entries can be selected."
            : selected == 0
                ? "Select at least one variable."
                : $"{selected} of {selectable} selected";

        acceptButton.Enabled = selected > 0;
        selectAllButton.Enabled = selected < selectable;
        selectNoneButton.Enabled = selected > 0;
    }

    /// <summary>
    /// Captures the checked entries; the button's <see cref="DialogResult"/> then closes the window.
    /// </summary>
    private void AcceptButtonClicked(object? sender, EventArgs e) =>
        SelectedEntries =
        [
            .. entriesGrid.Rows
                .Cast<DataGridViewRow>()
                .Where(row => row.Tag is VariableFileEntry && row.Cells[selectedColumn.Index].Value is true)
                .Select(row => (VariableFileEntry)row.Tag!),
        ];
}
