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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
            tableLayoutPanel1 = new TableLayoutPanel();
            tableLayoutPanel2 = new TableLayoutPanel();
            tableLayoutPanel3 = new TableLayoutPanel();
            versionLabel = new Label();
            copyrightLabel = new Label();
            pictureBox1 = new PictureBox();
            tableLayoutPanel4 = new TableLayoutPanel();
            repoLink = new LinkLabel();
            okButton = new Button();
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            tableLayoutPanel4.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel4, 0, 1);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(tableLayoutPanel2, "tableLayoutPanel2");
            tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 1, 0);
            tableLayoutPanel2.Controls.Add(pictureBox1, 0, 0);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(tableLayoutPanel3, "tableLayoutPanel3");
            tableLayoutPanel3.Controls.Add(versionLabel, 0, 0);
            tableLayoutPanel3.Controls.Add(copyrightLabel, 0, 1);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // versionLabel
            // 
            resources.ApplyResources(versionLabel, "versionLabel");
            versionLabel.Name = "versionLabel";
            // 
            // copyrightLabel
            // 
            resources.ApplyResources(copyrightLabel, "copyrightLabel");
            copyrightLabel.Name = "copyrightLabel";
            // 
            // pictureBox1
            // 
            resources.ApplyResources(pictureBox1, "pictureBox1");
            pictureBox1.Image = Properties.Resources.pen_square_svgrepo_com;
            pictureBox1.Name = "pictureBox1";
            pictureBox1.TabStop = false;
            // 
            // tableLayoutPanel4
            // 
            resources.ApplyResources(tableLayoutPanel4, "tableLayoutPanel4");
            tableLayoutPanel4.Controls.Add(repoLink, 0, 0);
            tableLayoutPanel4.Controls.Add(okButton, 2, 0);
            tableLayoutPanel4.Name = "tableLayoutPanel4";
            // 
            // repoLink
            // 
            resources.ApplyResources(repoLink, "repoLink");
            repoLink.Name = "repoLink";
            repoLink.TabStop = true;
            repoLink.LinkClicked += repoLink_LinkClicked;
            // 
            // okButton
            // 
            resources.ApplyResources(okButton, "okButton");
            okButton.Name = "okButton";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += CloseForm;
            // 
            // AboutForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tableLayoutPanel1);
            Name = "AboutForm";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            tableLayoutPanel4.ResumeLayout(false);
            tableLayoutPanel4.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private TableLayoutPanel tableLayoutPanel2;
        private TableLayoutPanel tableLayoutPanel3;
        private Label copyrightLabel;
        private Label versionLabel;
        private PictureBox pictureBox1;
        private TableLayoutPanel tableLayoutPanel4;
        private LinkLabel repoLink;
        private Button okButton;
    }
}
