namespace enviro.Forms
{
    partial class VariableFileForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            introLabel = new Label();
            hintLabel = new Label();
            entriesGrid = new DataGridView();
            selectedColumn = new DataGridViewCheckBoxColumn();
            scopeColumn = new DataGridViewTextBoxColumn();
            nameColumn = new DataGridViewTextBoxColumn();
            valueColumn = new DataGridViewTextBoxColumn();
            currentValueColumn = new DataGridViewTextBoxColumn();
            statusColumn = new DataGridViewTextBoxColumn();
            tableLayoutPanel2 = new TableLayoutPanel();
            selectionLabel = new Label();
            selectAllButton = new Button();
            selectNoneButton = new Button();
            acceptButton = new Button();
            cancelButton = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)entriesGrid).BeginInit();
            tableLayoutPanel2.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(introLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(hintLabel, 0, 1);
            tableLayoutPanel1.Controls.Add(entriesGrid, 0, 2);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 3);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.Padding = new Padding(16, 14, 16, 12);
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.Size = new Size(780, 500);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // introLabel
            // 
            introLabel.AutoSize = true;
            introLabel.Dock = DockStyle.Fill;
            introLabel.Location = new Point(16, 14);
            introLabel.Margin = new Padding(0, 0, 0, 6);
            introLabel.Name = "introLabel";
            introLabel.Size = new Size(748, 15);
            introLabel.TabIndex = 0;
            introLabel.Text = "Choose the variables the file carries.";
            // 
            // hintLabel
            // 
            hintLabel.AutoSize = true;
            hintLabel.Dock = DockStyle.Fill;
            hintLabel.ForeColor = SystemColors.GrayText;
            hintLabel.Location = new Point(16, 35);
            hintLabel.Margin = new Padding(0, 0, 0, 10);
            hintLabel.MaximumSize = new Size(748, 0);
            hintLabel.Name = "hintLabel";
            hintLabel.Size = new Size(748, 15);
            hintLabel.TabIndex = 1;
            // 
            // entriesGrid
            // 
            entriesGrid.AllowUserToAddRows = false;
            entriesGrid.AllowUserToDeleteRows = false;
            entriesGrid.AllowUserToResizeRows = false;
            entriesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            entriesGrid.BackgroundColor = Color.White;
            entriesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            entriesGrid.Columns.AddRange(new DataGridViewColumn[] { selectedColumn, scopeColumn, nameColumn, valueColumn, currentValueColumn, statusColumn });
            entriesGrid.Dock = DockStyle.Fill;
            entriesGrid.Location = new Point(16, 63);
            entriesGrid.Margin = new Padding(0);
            entriesGrid.MultiSelect = false;
            entriesGrid.Name = "entriesGrid";
            entriesGrid.RowHeadersVisible = false;
            entriesGrid.RowTemplate.Height = 23;
            entriesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            entriesGrid.Size = new Size(748, 395);
            entriesGrid.TabIndex = 2;
            // 
            // selectedColumn
            // 
            selectedColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            selectedColumn.HeaderText = "";
            selectedColumn.MinimumWidth = 36;
            selectedColumn.Name = "selectedColumn";
            selectedColumn.Resizable = DataGridViewTriState.False;
            selectedColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            selectedColumn.Width = 36;
            // 
            // scopeColumn
            // 
            scopeColumn.FillWeight = 12F;
            scopeColumn.HeaderText = "Scope";
            scopeColumn.MinimumWidth = 70;
            scopeColumn.Name = "scopeColumn";
            scopeColumn.ReadOnly = true;
            scopeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // nameColumn
            // 
            nameColumn.FillWeight = 25F;
            nameColumn.HeaderText = "Name";
            nameColumn.MinimumWidth = 120;
            nameColumn.Name = "nameColumn";
            nameColumn.ReadOnly = true;
            nameColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // valueColumn
            // 
            valueColumn.FillWeight = 40F;
            valueColumn.HeaderText = "Value";
            valueColumn.MinimumWidth = 160;
            valueColumn.Name = "valueColumn";
            valueColumn.ReadOnly = true;
            valueColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // currentValueColumn
            // 
            currentValueColumn.FillWeight = 40F;
            currentValueColumn.HeaderText = "Current value";
            currentValueColumn.MinimumWidth = 160;
            currentValueColumn.Name = "currentValueColumn";
            currentValueColumn.ReadOnly = true;
            currentValueColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // statusColumn
            // 
            statusColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            statusColumn.HeaderText = "Status";
            statusColumn.MinimumWidth = 110;
            statusColumn.Name = "statusColumn";
            statusColumn.ReadOnly = true;
            statusColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.AutoSize = true;
            tableLayoutPanel2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel2.ColumnCount = 5;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel2.Controls.Add(selectionLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(selectAllButton, 1, 0);
            tableLayoutPanel2.Controls.Add(selectNoneButton, 2, 0);
            tableLayoutPanel2.Controls.Add(acceptButton, 3, 0);
            tableLayoutPanel2.Controls.Add(cancelButton, 4, 0);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(16, 458);
            tableLayoutPanel2.Margin = new Padding(0, 10, 0, 0);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel2.Size = new Size(748, 30);
            tableLayoutPanel2.TabIndex = 3;
            // 
            // selectionLabel
            // 
            selectionLabel.Anchor = AnchorStyles.Left;
            selectionLabel.AutoSize = true;
            selectionLabel.ForeColor = SystemColors.GrayText;
            selectionLabel.Location = new Point(0, 7);
            selectionLabel.Margin = new Padding(0, 0, 8, 0);
            selectionLabel.Name = "selectionLabel";
            selectionLabel.Size = new Size(0, 15);
            selectionLabel.TabIndex = 0;
            // 
            // selectAllButton
            // 
            selectAllButton.Anchor = AnchorStyles.Right;
            selectAllButton.AutoSize = true;
            selectAllButton.Location = new Point(345, 0);
            selectAllButton.Margin = new Padding(0, 0, 8, 0);
            selectAllButton.MinimumSize = new Size(75, 23);
            selectAllButton.Name = "selectAllButton";
            selectAllButton.Size = new Size(75, 23);
            selectAllButton.TabIndex = 1;
            selectAllButton.Text = "Select all";
            selectAllButton.UseVisualStyleBackColor = true;
            selectAllButton.Click += SelectAllClicked;
            // 
            // selectNoneButton
            // 
            selectNoneButton.Anchor = AnchorStyles.Right;
            selectNoneButton.AutoSize = true;
            selectNoneButton.Location = new Point(443, 0);
            selectNoneButton.Margin = new Padding(0, 0, 16, 0);
            selectNoneButton.MinimumSize = new Size(75, 23);
            selectNoneButton.Name = "selectNoneButton";
            selectNoneButton.Size = new Size(75, 23);
            selectNoneButton.TabIndex = 2;
            selectNoneButton.Text = "Select none";
            selectNoneButton.UseVisualStyleBackColor = true;
            selectNoneButton.Click += SelectNoneClicked;
            // 
            // acceptButton
            // 
            acceptButton.Anchor = AnchorStyles.Right;
            acceptButton.AutoSize = true;
            acceptButton.DialogResult = DialogResult.OK;
            acceptButton.Location = new Point(549, 0);
            acceptButton.Margin = new Padding(0, 0, 8, 0);
            acceptButton.MinimumSize = new Size(75, 23);
            acceptButton.Name = "acceptButton";
            acceptButton.Size = new Size(75, 23);
            acceptButton.TabIndex = 3;
            acceptButton.Text = "Export";
            acceptButton.UseVisualStyleBackColor = true;
            acceptButton.Click += AcceptButtonClicked;
            // 
            // cancelButton
            // 
            cancelButton.Anchor = AnchorStyles.Right;
            cancelButton.AutoSize = true;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(647, 0);
            cancelButton.Margin = new Padding(0);
            cancelButton.MinimumSize = new Size(75, 23);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(75, 23);
            cancelButton.TabIndex = 4;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            // 
            // VariableFileForm
            // 
            AcceptButton = acceptButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = cancelButton;
            ClientSize = new Size(780, 500);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "VariableFileForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Variables";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)entriesGrid).EndInit();
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Label introLabel;
        private Label hintLabel;
        private DataGridView entriesGrid;
        private DataGridViewCheckBoxColumn selectedColumn;
        private DataGridViewTextBoxColumn scopeColumn;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn valueColumn;
        private DataGridViewTextBoxColumn currentValueColumn;
        private DataGridViewTextBoxColumn statusColumn;
        private TableLayoutPanel tableLayoutPanel2;
        private Label selectionLabel;
        private Button selectAllButton;
        private Button selectNoneButton;
        private Button acceptButton;
        private Button cancelButton;
    }
}
