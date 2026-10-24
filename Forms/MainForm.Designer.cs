namespace enviro
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            saveToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            refreshToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            checkForUpdatesToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1 = new MenuStrip();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            AllTab = new TabControl();
            toolStrip1 = new ToolStrip();
            addToolStripButton = new ToolStripButton();
            editToolStripButton = new ToolStripButton();
            deleteToolStripButton = new ToolStripButton();
            applyToolStripButton = new ToolStripButton();
            statusStrip1 = new StatusStrip();
            pendingStatusLabel = new ToolStripStatusLabel();
            applyStatusLabel = new ToolStripStatusLabel();
            corruptedStatusLabel = new ToolStripStatusLabel();
            elevationStatusLabel = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem1 });
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            resources.ApplyResources(saveToolStripMenuItem, "saveToolStripMenuItem");
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(toolStripMenuItem1, "toolStripMenuItem1");
            toolStripMenuItem1.Click += Apply;
            // 
            // viewToolStripMenuItem
            //
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { refreshToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            resources.ApplyResources(viewToolStripMenuItem, "viewToolStripMenuItem");
            //
            // refreshToolStripMenuItem
            //
            refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
            resources.ApplyResources(refreshToolStripMenuItem, "refreshToolStripMenuItem");
            refreshToolStripMenuItem.Click += RefreshEntryClicked;
            //
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { checkForUpdatesToolStripMenuItem, aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(helpToolStripMenuItem, "helpToolStripMenuItem");
            // 
            // checkForUpdatesToolStripMenuItem
            // 
            checkForUpdatesToolStripMenuItem.Name = "checkForUpdatesToolStripMenuItem";
            resources.ApplyResources(checkForUpdatesToolStripMenuItem, "checkForUpdatesToolStripMenuItem");
            checkForUpdatesToolStripMenuItem.Click += checkForUpdatesToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            resources.ApplyResources(aboutToolStripMenuItem, "aboutToolStripMenuItem");
            aboutToolStripMenuItem.Click += AboutButtonClicked;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { saveToolStripMenuItem, viewToolStripMenuItem, optionsToolStripMenuItem, helpToolStripMenuItem });
            resources.ApplyResources(menuStrip1, "menuStrip1");
            menuStrip1.Name = "menuStrip1";
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            resources.ApplyResources(optionsToolStripMenuItem, "optionsToolStripMenuItem");
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { addToolStripButton, editToolStripButton, deleteToolStripButton, applyToolStripButton });
            toolStrip1.Name = "toolStrip1";
            resources.ApplyResources(toolStrip1, "toolStrip1");
            // 
            // addToolStripButton
            // 
            addToolStripButton.Name = "addToolStripButton";
            resources.ApplyResources(addToolStripButton, "addToolStripButton");
            addToolStripButton.Click += CreateEntryClicked;
            // 
            // editToolStripButton
            // 
            editToolStripButton.Name = "editToolStripButton";
            resources.ApplyResources(editToolStripButton, "editToolStripButton");
            editToolStripButton.Click += EditEntryClicked;
            // 
            // deleteToolStripButton
            // 
            deleteToolStripButton.Name = "deleteToolStripButton";
            resources.ApplyResources(deleteToolStripButton, "deleteToolStripButton");
            deleteToolStripButton.Click += DeleteEntryClicked;
            // 
            // applyToolStripButton
            // 
            applyToolStripButton.Name = "applyToolStripButton";
            resources.ApplyResources(applyToolStripButton, "applyToolStripButton");
            applyToolStripButton.Click += Apply;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { pendingStatusLabel, applyStatusLabel, corruptedStatusLabel, elevationStatusLabel });
            statusStrip1.Name = "statusStrip1";
            resources.ApplyResources(statusStrip1, "statusStrip1");
            // 
            // pendingStatusLabel
            //
            pendingStatusLabel.AutoSize = true;
            pendingStatusLabel.Name = "pendingStatusLabel";
            resources.ApplyResources(pendingStatusLabel, "pendingStatusLabel");
            //
            // applyStatusLabel
            //
            applyStatusLabel.AutoSize = true;
            applyStatusLabel.Name = "applyStatusLabel";
            resources.ApplyResources(applyStatusLabel, "applyStatusLabel");
            // 
            // corruptedStatusLabel
            // 
            corruptedStatusLabel.AutoSize = true;
            corruptedStatusLabel.Name = "corruptedStatusLabel";
            resources.ApplyResources(corruptedStatusLabel, "corruptedStatusLabel");
            // 
            // elevationStatusLabel
            // 
            elevationStatusLabel.Alignment = ToolStripItemAlignment.Right;
            elevationStatusLabel.AutoSize = true;
            elevationStatusLabel.Name = "elevationStatusLabel";
            resources.ApplyResources(elevationStatusLabel, "elevationStatusLabel");
            // 
            // AllTab
            // 
            resources.ApplyResources(AllTab, "AllTab");
            AllTab.Name = "AllTab";
            AllTab.SelectedIndex = 0;
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(AllTab);
            Controls.Add(statusStrip1);
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem refreshToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem checkForUpdatesToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private MenuStrip menuStrip1;
        private ToolStrip toolStrip1;
        private ToolStripButton addToolStripButton;
        private ToolStripButton editToolStripButton;
        private ToolStripButton deleteToolStripButton;
        private ToolStripButton applyToolStripButton;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel pendingStatusLabel;
        private ToolStripStatusLabel applyStatusLabel;
        private ToolStripStatusLabel corruptedStatusLabel;
        private ToolStripStatusLabel elevationStatusLabel;
        private TabControl AllTab;
        private ToolStripMenuItem optionsToolStripMenuItem;
    }
}
