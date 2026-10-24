namespace enviro.Forms
{
    partial class AbstractEntryForm
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
            tableLayoutPanel2 = new TableLayoutPanel();
            label1 = new Label();
            nameTextBox = new TextBox();
            tableLayoutPanel3 = new TableLayoutPanel();
            label2 = new Label();
            tableLayoutPanel4 = new TableLayoutPanel();
            pathTextBox = new TextBox();
            browseButton = new Button();
            tableLayoutPanel5 = new TableLayoutPanel();
            entriesButton = new Button();
            addButton = new Button();
            cancelButton = new Button();
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            tableLayoutPanel4.SuspendLayout();
            tableLayoutPanel5.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 1);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel5, 0, 2);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.Padding = new Padding(16, 14, 16, 12);
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.Size = new Size(520, 180);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.AutoSize = true;
            tableLayoutPanel2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel2.ColumnCount = 1;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Controls.Add(label1, 0, 0);
            tableLayoutPanel2.Controls.Add(nameTextBox, 0, 1);
            tableLayoutPanel2.Dock = DockStyle.Top;
            tableLayoutPanel2.Location = new Point(16, 14);
            tableLayoutPanel2.Margin = new Padding(0, 0, 0, 10);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 2;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel2.Size = new Size(488, 42);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(0, 0);
            label1.Margin = new Padding(0, 0, 0, 4);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 0;
            label1.Text = "Name";
            // 
            // nameTextBox
            // 
            nameTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nameTextBox.Location = new Point(0, 19);
            nameTextBox.Margin = new Padding(0);
            nameTextBox.Name = "nameTextBox";
            nameTextBox.Size = new Size(488, 23);
            nameTextBox.TabIndex = 0;
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.AutoSize = true;
            tableLayoutPanel3.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel3.ColumnCount = 1;
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel3.Controls.Add(label2, 0, 0);
            tableLayoutPanel3.Controls.Add(tableLayoutPanel4, 0, 1);
            tableLayoutPanel3.Dock = DockStyle.Fill;
            tableLayoutPanel3.Location = new Point(16, 66);
            tableLayoutPanel3.Margin = new Padding(0);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 2;
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel3.Size = new Size(488, 53);
            tableLayoutPanel3.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(0, 0);
            label2.Margin = new Padding(0, 0, 0, 4);
            label2.Name = "label2";
            label2.Size = new Size(36, 15);
            label2.TabIndex = 0;
            label2.Text = "Value";
            // 
            // tableLayoutPanel4
            // 
            tableLayoutPanel4.AutoSize = true;
            tableLayoutPanel4.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel4.ColumnCount = 2;
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel4.Controls.Add(pathTextBox, 0, 0);
            tableLayoutPanel4.Controls.Add(browseButton, 1, 0);
            tableLayoutPanel4.Dock = DockStyle.Top;
            tableLayoutPanel4.Location = new Point(0, 19);
            tableLayoutPanel4.Margin = new Padding(0);
            tableLayoutPanel4.Name = "tableLayoutPanel4";
            tableLayoutPanel4.RowCount = 1;
            tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel4.Size = new Size(488, 28);
            tableLayoutPanel4.TabIndex = 1;
            // 
            // pathTextBox
            // 
            pathTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            pathTextBox.Location = new Point(0, 3);
            pathTextBox.Margin = new Padding(0, 3, 8, 0);
            pathTextBox.Name = "pathTextBox";
            pathTextBox.Size = new Size(390, 23);
            pathTextBox.TabIndex = 0;
            // 
            // browseButton
            // 
            browseButton.Anchor = AnchorStyles.Left;
            browseButton.Location = new Point(398, 2);
            browseButton.Margin = new Padding(0, 2, 0, 0);
            browseButton.Name = "browseButton";
            browseButton.Size = new Size(90, 25);
            browseButton.TabIndex = 1;
            browseButton.Text = "Browse…";
            browseButton.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel5
            // 
            tableLayoutPanel5.AutoSize = true;
            tableLayoutPanel5.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel5.ColumnCount = 4;
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanel5.Controls.Add(entriesButton, 0, 0);
            tableLayoutPanel5.Controls.Add(addButton, 2, 0);
            tableLayoutPanel5.Controls.Add(cancelButton, 3, 0);
            tableLayoutPanel5.Dock = DockStyle.Top;
            tableLayoutPanel5.Location = new Point(16, 119);
            tableLayoutPanel5.Margin = new Padding(0, 10, 0, 0);
            tableLayoutPanel5.Name = "tableLayoutPanel5";
            tableLayoutPanel5.RowCount = 1;
            tableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel5.Size = new Size(488, 39);
            tableLayoutPanel5.TabIndex = 2;
            //
            // entriesButton
            //
            entriesButton.Anchor = AnchorStyles.Left;
            entriesButton.Location = new Point(0, 5);
            entriesButton.Margin = new Padding(0);
            entriesButton.Name = "entriesButton";
            entriesButton.Size = new Size(110, 29);
            entriesButton.TabIndex = 0;
            entriesButton.Text = "Edit entries";
            entriesButton.UseVisualStyleBackColor = true;
            entriesButton.Visible = false;
            // 
            // addButton
            // 
            addButton.Anchor = AnchorStyles.Right;
            addButton.AutoSize = true;
            addButton.Location = new Point(280, 0);
            addButton.Margin = new Padding(0, 0, 8, 0);
            addButton.MinimumSize = new Size(90, 29);
            addButton.Name = "addButton";
            addButton.Size = new Size(90, 29);
            addButton.TabIndex = 1;
            addButton.Text = "Add";
            addButton.UseVisualStyleBackColor = true;
            addButton.Click += RunAction;
            // 
            // cancelButton
            // 
            cancelButton.Anchor = AnchorStyles.Right;
            cancelButton.AutoSize = true;
            cancelButton.Location = new Point(378, 0);
            cancelButton.Margin = new Padding(0);
            cancelButton.MinimumSize = new Size(90, 29);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(90, 29);
            cancelButton.TabIndex = 2;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            cancelButton.Click += Close;
            // 
            // AbstractEntryForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 180);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AbstractEntryForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Create";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            tableLayoutPanel4.ResumeLayout(false);
            tableLayoutPanel4.PerformLayout();
            tableLayoutPanel5.ResumeLayout(false);
            tableLayoutPanel5.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        protected TableLayoutPanel tableLayoutPanel1;
        protected Label label2;
        protected TextBox nameTextBox;
        protected TableLayoutPanel tableLayoutPanel2;
        protected Label label1;
        protected TableLayoutPanel tableLayoutPanel3;
        protected TableLayoutPanel tableLayoutPanel5;
        protected Button addButton;
        protected Button cancelButton;
        protected TableLayoutPanel tableLayoutPanel4;
        protected TextBox pathTextBox;
        protected Button browseButton;
        protected Button entriesButton;
    }
}
