namespace enviro.Forms
{
    partial class UpdateForm
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
            headerLayout = new TableLayoutPanel();
            appIcon = new PictureBox();
            headerTextLayout = new TableLayoutPanel();
            titleLabel = new Label();
            versionLabel = new Label();
            releaseNotesLabel = new Label();
            releaseNotesTextBox = new TextBox();
            buttonLayout = new TableLayoutPanel();
            openReleaseButton = new Button();
            closeButton = new Button();
            tableLayoutPanel1.SuspendLayout();
            headerLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)appIcon).BeginInit();
            headerTextLayout.SuspendLayout();
            buttonLayout.SuspendLayout();
            SuspendLayout();
            //
            // tableLayoutPanel1
            //
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(headerLayout, 0, 0);
            tableLayoutPanel1.Controls.Add(releaseNotesLabel, 0, 1);
            tableLayoutPanel1.Controls.Add(releaseNotesTextBox, 0, 2);
            tableLayoutPanel1.Controls.Add(buttonLayout, 0, 3);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.Padding = new Padding(12);
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.Size = new Size(560, 440);
            tableLayoutPanel1.TabIndex = 0;
            //
            // headerLayout
            //
            headerLayout.AutoSize = true;
            headerLayout.ColumnCount = 2;
            headerLayout.ColumnStyles.Add(new ColumnStyle());
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.Controls.Add(appIcon, 0, 0);
            headerLayout.Controls.Add(headerTextLayout, 1, 0);
            headerLayout.Dock = DockStyle.Fill;
            headerLayout.Location = new Point(12, 12);
            headerLayout.Margin = new Padding(0, 0, 0, 10);
            headerLayout.Name = "headerLayout";
            headerLayout.RowCount = 1;
            headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerLayout.Size = new Size(536, 48);
            headerLayout.TabIndex = 0;
            //
            // appIcon
            //
            appIcon.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            appIcon.Image = Properties.Resources.pen_square_svgrepo_com;
            appIcon.Location = new Point(0, 0);
            appIcon.Margin = new Padding(0, 0, 12, 0);
            appIcon.Name = "appIcon";
            appIcon.Size = new Size(48, 48);
            appIcon.SizeMode = PictureBoxSizeMode.Zoom;
            appIcon.TabIndex = 0;
            appIcon.TabStop = false;
            //
            // headerTextLayout
            //
            headerTextLayout.AutoSize = true;
            headerTextLayout.ColumnCount = 1;
            headerTextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerTextLayout.Controls.Add(titleLabel, 0, 0);
            headerTextLayout.Controls.Add(versionLabel, 0, 1);
            headerTextLayout.Dock = DockStyle.Fill;
            headerTextLayout.Location = new Point(60, 0);
            headerTextLayout.Margin = new Padding(0);
            headerTextLayout.Name = "headerTextLayout";
            headerTextLayout.RowCount = 2;
            headerTextLayout.RowStyles.Add(new RowStyle());
            headerTextLayout.RowStyles.Add(new RowStyle());
            headerTextLayout.Size = new Size(476, 48);
            headerTextLayout.TabIndex = 1;
            //
            // titleLabel
            //
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            titleLabel.Location = new Point(0, 0);
            titleLabel.Margin = new Padding(0, 0, 0, 4);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(124, 21);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Update available";
            //
            // versionLabel
            //
            versionLabel.AutoSize = true;
            versionLabel.Location = new Point(0, 25);
            versionLabel.Margin = new Padding(0);
            versionLabel.Name = "versionLabel";
            versionLabel.Size = new Size(100, 15);
            versionLabel.TabIndex = 1;
            versionLabel.Text = "Version information";
            //
            // releaseNotesLabel
            //
            releaseNotesLabel.AutoSize = true;
            releaseNotesLabel.Location = new Point(12, 70);
            releaseNotesLabel.Margin = new Padding(0, 0, 0, 4);
            releaseNotesLabel.Name = "releaseNotesLabel";
            releaseNotesLabel.Size = new Size(78, 15);
            releaseNotesLabel.TabIndex = 1;
            releaseNotesLabel.Text = "Release notes";
            //
            // releaseNotesTextBox
            //
            releaseNotesTextBox.AccessibleDescription = "Release notes for the available version";
            releaseNotesTextBox.AccessibleName = "Release notes";
            releaseNotesTextBox.BackColor = SystemColors.Window;
            releaseNotesTextBox.Dock = DockStyle.Fill;
            releaseNotesTextBox.HideSelection = false;
            releaseNotesTextBox.Location = new Point(12, 89);
            releaseNotesTextBox.Margin = new Padding(0, 0, 0, 10);
            releaseNotesTextBox.Multiline = true;
            releaseNotesTextBox.Name = "releaseNotesTextBox";
            releaseNotesTextBox.ReadOnly = true;
            releaseNotesTextBox.ScrollBars = ScrollBars.Vertical;
            releaseNotesTextBox.Size = new Size(536, 299);
            releaseNotesTextBox.TabIndex = 2;
            releaseNotesTextBox.WordWrap = true;
            //
            // buttonLayout
            //
            buttonLayout.AutoSize = true;
            buttonLayout.ColumnCount = 3;
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonLayout.ColumnStyles.Add(new ColumnStyle());
            buttonLayout.ColumnStyles.Add(new ColumnStyle());
            buttonLayout.Controls.Add(openReleaseButton, 1, 0);
            buttonLayout.Controls.Add(closeButton, 2, 0);
            buttonLayout.Dock = DockStyle.Fill;
            buttonLayout.Location = new Point(12, 398);
            buttonLayout.Margin = new Padding(0);
            buttonLayout.Name = "buttonLayout";
            buttonLayout.RowCount = 1;
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            buttonLayout.Size = new Size(536, 30);
            buttonLayout.TabIndex = 3;
            //
            // openReleaseButton
            //
            openReleaseButton.AutoSize = true;
            openReleaseButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            openReleaseButton.Location = new Point(293, 0);
            openReleaseButton.Margin = new Padding(6, 0, 0, 0);
            openReleaseButton.MinimumSize = new Size(75, 23);
            openReleaseButton.Name = "openReleaseButton";
            openReleaseButton.Size = new Size(75, 23);
            openReleaseButton.TabIndex = 0;
            openReleaseButton.Text = "&Open release page";
            openReleaseButton.UseVisualStyleBackColor = true;
            openReleaseButton.Click += openReleaseButton_Click;
            //
            // closeButton
            //
            closeButton.AutoSize = true;
            closeButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            closeButton.Location = new Point(449, 0);
            closeButton.Margin = new Padding(6, 0, 0, 0);
            closeButton.MinimumSize = new Size(75, 23);
            closeButton.Name = "closeButton";
            closeButton.Size = new Size(75, 23);
            closeButton.TabIndex = 1;
            closeButton.Text = "Close";
            closeButton.UseVisualStyleBackColor = true;
            closeButton.Click += closeButton_Click;
            //
            // UpdateForm
            //
            AcceptButton = openReleaseButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = closeButton;
            ClientSize = new Size(560, 440);
            Controls.Add(tableLayoutPanel1);
            MinimumSize = new Size(480, 340);
            MinimizeBox = false;
            Name = "UpdateForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Update available";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            headerLayout.ResumeLayout(false);
            headerLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)appIcon).EndInit();
            headerTextLayout.ResumeLayout(false);
            headerTextLayout.PerformLayout();
            buttonLayout.ResumeLayout(false);
            buttonLayout.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private TableLayoutPanel headerLayout;
        private PictureBox appIcon;
        private TableLayoutPanel headerTextLayout;
        private Label titleLabel;
        private Label versionLabel;
        private Label releaseNotesLabel;
        private TextBox releaseNotesTextBox;
        private TableLayoutPanel buttonLayout;
        private Button openReleaseButton;
        private Button closeButton;
    }
}
