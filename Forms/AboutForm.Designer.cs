namespace enviro
{
    partial class AboutForm
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
            rootLayout = new TableLayoutPanel();
            headerLayout = new TableLayoutPanel();
            appIcon = new PictureBox();
            headerTextLayout = new TableLayoutPanel();
            titleLabel = new Label();
            versionLabel = new Label();
            descriptionLabel = new Label();
            copyrightLabel = new Label();
            separator = new Panel();
            footerLayout = new TableLayoutPanel();
            linksLayout = new FlowLayoutPanel();
            repoLink = new LinkLabel();
            issuesLink = new LinkLabel();
            licenseLink = new LinkLabel();
            okButton = new Button();
            rootLayout.SuspendLayout();
            headerLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)appIcon).BeginInit();
            headerTextLayout.SuspendLayout();
            footerLayout.SuspendLayout();
            linksLayout.SuspendLayout();
            SuspendLayout();
            //
            // rootLayout
            //
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(headerLayout, 0, 0);
            rootLayout.Controls.Add(descriptionLabel, 0, 1);
            rootLayout.Controls.Add(copyrightLabel, 0, 2);
            rootLayout.Controls.Add(separator, 0, 3);
            rootLayout.Controls.Add(footerLayout, 0, 5);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(20, 18, 20, 16);
            rootLayout.RowCount = 6;
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.Size = new Size(460, 190);
            rootLayout.TabIndex = 0;
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
            headerLayout.Location = new Point(20, 18);
            headerLayout.Margin = new Padding(0, 0, 0, 14);
            headerLayout.Name = "headerLayout";
            headerLayout.RowCount = 1;
            headerLayout.RowStyles.Add(new RowStyle());
            headerLayout.Size = new Size(420, 48);
            headerLayout.TabIndex = 0;
            //
            // appIcon
            //
            appIcon.AccessibleDescription = "Enviro application icon";
            appIcon.AccessibleName = "Enviro logo";
            appIcon.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            appIcon.Image = Properties.Resources.pen_square_svgrepo_com;
            appIcon.Location = new Point(0, 0);
            appIcon.Margin = new Padding(0, 0, 14, 0);
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
            headerTextLayout.Location = new Point(62, 0);
            headerTextLayout.Margin = new Padding(0);
            headerTextLayout.Name = "headerTextLayout";
            headerTextLayout.RowCount = 2;
            headerTextLayout.RowStyles.Add(new RowStyle());
            headerTextLayout.RowStyles.Add(new RowStyle());
            headerTextLayout.Size = new Size(358, 48);
            headerTextLayout.TabIndex = 1;
            //
            // titleLabel
            //
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            titleLabel.Location = new Point(0, 0);
            titleLabel.Margin = new Padding(0, 0, 0, 2);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(50, 21);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Enviro";
            //
            // versionLabel
            //
            versionLabel.AutoSize = true;
            versionLabel.ForeColor = SystemColors.GrayText;
            versionLabel.Location = new Point(0, 23);
            versionLabel.Margin = new Padding(0);
            versionLabel.Name = "versionLabel";
            versionLabel.Size = new Size(47, 15);
            versionLabel.TabIndex = 1;
            versionLabel.Text = "Version";
            //
            // descriptionLabel
            //
            descriptionLabel.Anchor = AnchorStyles.Left;
            descriptionLabel.AutoSize = true;
            descriptionLabel.Location = new Point(20, 80);
            descriptionLabel.Margin = new Padding(0, 0, 0, 4);
            descriptionLabel.Name = "descriptionLabel";
            descriptionLabel.Size = new Size(295, 15);
            descriptionLabel.TabIndex = 1;
            descriptionLabel.Text = "A lightweight environment variable manager for Windows.";
            //
            // copyrightLabel
            //
            copyrightLabel.Anchor = AnchorStyles.Left;
            copyrightLabel.AutoSize = true;
            copyrightLabel.ForeColor = SystemColors.GrayText;
            copyrightLabel.Location = new Point(20, 99);
            copyrightLabel.Margin = new Padding(0, 0, 0, 14);
            copyrightLabel.Name = "copyrightLabel";
            copyrightLabel.Size = new Size(64, 15);
            copyrightLabel.TabIndex = 2;
            copyrightLabel.Text = "Copyright";
            //
            // separator
            //
            separator.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            separator.BackColor = SystemColors.ControlDark;
            separator.Location = new Point(20, 128);
            separator.Margin = new Padding(0, 0, 0, 12);
            separator.Name = "separator";
            separator.Size = new Size(420, 1);
            separator.TabIndex = 3;
            //
            // footerLayout
            //
            footerLayout.AutoSize = true;
            footerLayout.ColumnCount = 2;
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footerLayout.ColumnStyles.Add(new ColumnStyle());
            footerLayout.Controls.Add(linksLayout, 0, 0);
            footerLayout.Controls.Add(okButton, 1, 0);
            footerLayout.Dock = DockStyle.Fill;
            footerLayout.Location = new Point(20, 151);
            footerLayout.Margin = new Padding(0);
            footerLayout.Name = "footerLayout";
            footerLayout.RowCount = 1;
            footerLayout.RowStyles.Add(new RowStyle());
            footerLayout.Size = new Size(420, 23);
            footerLayout.TabIndex = 4;
            //
            // linksLayout
            //
            linksLayout.Anchor = AnchorStyles.Left;
            linksLayout.AutoSize = true;
            linksLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            linksLayout.Controls.Add(repoLink);
            linksLayout.Controls.Add(issuesLink);
            linksLayout.Controls.Add(licenseLink);
            linksLayout.FlowDirection = FlowDirection.LeftToRight;
            linksLayout.Location = new Point(0, 4);
            linksLayout.Margin = new Padding(0);
            linksLayout.Name = "linksLayout";
            linksLayout.Size = new Size(262, 15);
            linksLayout.TabIndex = 0;
            linksLayout.WrapContents = false;
            //
            // repoLink
            //
            repoLink.AccessibleDescription = "Open the project repository in your browser";
            repoLink.AccessibleName = "Enviro on GitHub";
            repoLink.AutoSize = true;
            repoLink.Location = new Point(0, 0);
            repoLink.Margin = new Padding(0, 0, 18, 0);
            repoLink.Name = "repoLink";
            repoLink.Size = new Size(46, 15);
            repoLink.TabIndex = 0;
            repoLink.TabStop = true;
            repoLink.Text = "GitHub";
            repoLink.LinkClicked += OpenLink;
            //
            // issuesLink
            //
            issuesLink.AccessibleDescription = "Open the project issue tracker in your browser";
            issuesLink.AccessibleName = "Report an issue";
            issuesLink.AutoSize = true;
            issuesLink.Location = new Point(64, 0);
            issuesLink.Margin = new Padding(0, 0, 18, 0);
            issuesLink.Name = "issuesLink";
            issuesLink.Size = new Size(90, 15);
            issuesLink.TabIndex = 1;
            issuesLink.TabStop = true;
            issuesLink.Text = "Report an issue";
            issuesLink.LinkClicked += OpenLink;
            //
            // licenseLink
            //
            licenseLink.AccessibleDescription = "Open the GPL-3.0 license in your browser";
            licenseLink.AccessibleName = "GPL-3.0 license";
            licenseLink.AutoSize = true;
            licenseLink.Location = new Point(172, 0);
            licenseLink.Margin = new Padding(0);
            licenseLink.Name = "licenseLink";
            licenseLink.Size = new Size(90, 15);
            licenseLink.TabIndex = 2;
            licenseLink.TabStop = true;
            licenseLink.Text = "GPL-3.0 license";
            licenseLink.LinkClicked += OpenLink;
            //
            // okButton
            //
            okButton.AutoSize = true;
            okButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            okButton.Location = new Point(345, 0);
            okButton.Margin = new Padding(0);
            okButton.MinimumSize = new Size(75, 23);
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.TabIndex = 1;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            //
            // AboutForm
            //
            AcceptButton = okButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = okButton;
            ClientSize = new Size(460, 190);
            Controls.Add(rootLayout);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "About";
            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            headerLayout.ResumeLayout(false);
            headerLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)appIcon).EndInit();
            headerTextLayout.ResumeLayout(false);
            headerTextLayout.PerformLayout();
            footerLayout.ResumeLayout(false);
            footerLayout.PerformLayout();
            linksLayout.ResumeLayout(false);
            linksLayout.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel rootLayout;
        private TableLayoutPanel headerLayout;
        private PictureBox appIcon;
        private TableLayoutPanel headerTextLayout;
        private Label titleLabel;
        private Label versionLabel;
        private Label descriptionLabel;
        private Label copyrightLabel;
        private Panel separator;
        private TableLayoutPanel footerLayout;
        private FlowLayoutPanel linksLayout;
        private LinkLabel repoLink;
        private LinkLabel issuesLink;
        private LinkLabel licenseLink;
        private Button okButton;
    }
}
