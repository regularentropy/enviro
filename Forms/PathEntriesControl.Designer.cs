namespace enviro.Forms;

partial class PathEntriesControl
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        mainLayout = new TableLayoutPanel();
        segmentsGrid = new DataGridView();
        valueColumn = new DataGridViewTextBoxColumn();
        existenceColumn = new DataGridViewTextBoxColumn();
        actionsLayout = new TableLayoutPanel();
        addButton = new Button();
        removeButton = new Button();
        moveUpButton = new Button();
        moveDownButton = new Button();
        openButton = new Button();
        mainLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)segmentsGrid).BeginInit();
        actionsLayout.SuspendLayout();
        SuspendLayout();
        //
        // mainLayout
        //
        mainLayout.ColumnCount = 2;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainLayout.Controls.Add(segmentsGrid, 0, 0);
        mainLayout.Controls.Add(actionsLayout, 1, 0);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.RowCount = 1;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Size = new Size(459, 220);
        mainLayout.TabIndex = 0;
        //
        // segmentsGrid
        //
        segmentsGrid.AllowUserToAddRows = false;
        segmentsGrid.AllowUserToDeleteRows = false;
        segmentsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        segmentsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        segmentsGrid.Columns.AddRange(new DataGridViewColumn[] { valueColumn, existenceColumn });
        segmentsGrid.Dock = DockStyle.Fill;
        segmentsGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        segmentsGrid.Location = new Point(3, 3);
        segmentsGrid.Margin = new Padding(3, 3, 8, 3);
        segmentsGrid.MultiSelect = false;
        segmentsGrid.Name = "segmentsGrid";
        segmentsGrid.RowHeadersVisible = false;
        segmentsGrid.RowTemplate.Height = 25;
        segmentsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        segmentsGrid.Size = new Size(320, 214);
        segmentsGrid.TabIndex = 0;
        //
        // valueColumn
        //
        valueColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        valueColumn.HeaderText = "Entry";
        valueColumn.Name = "valueColumn";
        valueColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // existenceColumn
        //
        existenceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        existenceColumn.HeaderText = "Exists";
        existenceColumn.Name = "existenceColumn";
        existenceColumn.ReadOnly = true;
        existenceColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        existenceColumn.Width = 90;
        //
        // actionsLayout
        //
        actionsLayout.AutoSize = true;
        actionsLayout.ColumnCount = 1;
        actionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        actionsLayout.Controls.Add(addButton, 0, 0);
        actionsLayout.Controls.Add(removeButton, 0, 1);
        actionsLayout.Controls.Add(moveUpButton, 0, 2);
        actionsLayout.Controls.Add(moveDownButton, 0, 3);
        actionsLayout.Controls.Add(openButton, 0, 4);
        actionsLayout.Dock = DockStyle.Top;
        actionsLayout.Location = new Point(331, 3);
        actionsLayout.Name = "actionsLayout";
        actionsLayout.RowCount = 5;
        actionsLayout.RowStyles.Add(new RowStyle());
        actionsLayout.RowStyles.Add(new RowStyle());
        actionsLayout.RowStyles.Add(new RowStyle());
        actionsLayout.RowStyles.Add(new RowStyle());
        actionsLayout.RowStyles.Add(new RowStyle());
        actionsLayout.Size = new Size(125, 160);
        actionsLayout.TabIndex = 1;
        //
        // addButton
        //
        addButton.Location = new Point(3, 3);
        addButton.Name = "addButton";
        addButton.Size = new Size(119, 26);
        addButton.TabIndex = 0;
        addButton.Text = "Add folder…";
        addButton.UseVisualStyleBackColor = true;
        addButton.Click += AddButtonClicked;
        //
        // removeButton
        //
        removeButton.Location = new Point(3, 35);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(119, 26);
        removeButton.TabIndex = 1;
        removeButton.Text = "Remove";
        removeButton.UseVisualStyleBackColor = true;
        removeButton.Click += RemoveButtonClicked;
        //
        // moveUpButton
        //
        moveUpButton.Location = new Point(3, 67);
        moveUpButton.Name = "moveUpButton";
        moveUpButton.Size = new Size(119, 26);
        moveUpButton.TabIndex = 2;
        moveUpButton.Text = "Move up";
        moveUpButton.UseVisualStyleBackColor = true;
        moveUpButton.Click += MoveUpButtonClicked;
        //
        // moveDownButton
        //
        moveDownButton.Location = new Point(3, 99);
        moveDownButton.Name = "moveDownButton";
        moveDownButton.Size = new Size(119, 26);
        moveDownButton.TabIndex = 3;
        moveDownButton.Text = "Move down";
        moveDownButton.UseVisualStyleBackColor = true;
        moveDownButton.Click += MoveDownButtonClicked;
        //
        // openButton
        //
        openButton.Location = new Point(3, 131);
        openButton.Name = "openButton";
        openButton.Size = new Size(119, 26);
        openButton.TabIndex = 4;
        openButton.Text = "Open in Explorer";
        openButton.UseVisualStyleBackColor = true;
        openButton.Click += OpenButtonClicked;
        //
        // PathEntriesControl
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(mainLayout);
        MinimumSize = new Size(0, 160);
        Name = "PathEntriesControl";
        Size = new Size(459, 220);
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)segmentsGrid).EndInit();
        actionsLayout.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel mainLayout;
    private DataGridView segmentsGrid;
    private DataGridViewTextBoxColumn valueColumn;
    private DataGridViewTextBoxColumn existenceColumn;
    private TableLayoutPanel actionsLayout;
    private Button addButton;
    private Button removeButton;
    private Button moveUpButton;
    private Button moveDownButton;
    private Button openButton;
}
