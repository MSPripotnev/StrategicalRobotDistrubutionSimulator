namespace TacticalAgro {
    partial class Form1 {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.mapPanel = new System.Windows.Forms.Panel();
            this.infoPanel = new System.Windows.Forms.Panel();
            this.iterationsCountL = new System.Windows.Forms.Label();
            this.timeCountL = new System.Windows.Forms.Label();
            this.collectedObjsCountL = new System.Windows.Forms.Label();
            this.currentObjsCountL = new System.Windows.Forms.Label();
            this.iterationsTextL = new System.Windows.Forms.Label();
            this.timeTextL = new System.Windows.Forms.Label();
            this.collectedObjsTextL = new System.Windows.Forms.Label();
            this.currentObjsTextL = new System.Windows.Forms.Label();
            this.controlPanel = new System.Windows.Forms.Panel();
            this.startB = new System.Windows.Forms.Button();
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.mainCMS = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.newObjectTSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.newObjTSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.newRobotTSMI = new System.Windows.Forms.ToolStripMenuItem();
            this.newBaseTSMI = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.infoPanel.SuspendLayout();
            this.controlPanel.SuspendLayout();
            this.mainCMS.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.mapPanel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.infoPanel);
            this.splitContainer1.Panel2.Controls.Add(this.controlPanel);
            this.splitContainer1.Size = new System.Drawing.Size(951, 512);
            this.splitContainer1.SplitterDistance = 707;
            this.splitContainer1.TabIndex = 0;
            // 
            // mapPanel
            // 
            this.mapPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapPanel.Location = new System.Drawing.Point(0, 0);
            this.mapPanel.Name = "mapPanel";
            this.mapPanel.Size = new System.Drawing.Size(707, 512);
            this.mapPanel.TabIndex = 0;
            this.mapPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.mapPanel_Paint);
            this.mapPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.mapPanel_MouseDown);
            // 
            // infoPanel
            // 
            this.infoPanel.Controls.Add(this.iterationsCountL);
            this.infoPanel.Controls.Add(this.timeCountL);
            this.infoPanel.Controls.Add(this.collectedObjsCountL);
            this.infoPanel.Controls.Add(this.currentObjsCountL);
            this.infoPanel.Controls.Add(this.iterationsTextL);
            this.infoPanel.Controls.Add(this.timeTextL);
            this.infoPanel.Controls.Add(this.collectedObjsTextL);
            this.infoPanel.Controls.Add(this.currentObjsTextL);
            this.infoPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.infoPanel.Location = new System.Drawing.Point(0, 0);
            this.infoPanel.Name = "infoPanel";
            this.infoPanel.Size = new System.Drawing.Size(240, 153);
            this.infoPanel.TabIndex = 1;
            // 
            // iterationsCountL
            // 
            this.iterationsCountL.AutoSize = true;
            this.iterationsCountL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.iterationsCountL.Location = new System.Drawing.Point(170, 115);
            this.iterationsCountL.Name = "iterationsCountL";
            this.iterationsCountL.Size = new System.Drawing.Size(22, 25);
            this.iterationsCountL.TabIndex = 7;
            this.iterationsCountL.Text = "0";
            // 
            // timeCountL
            // 
            this.timeCountL.AutoSize = true;
            this.timeCountL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.timeCountL.Location = new System.Drawing.Point(170, 80);
            this.timeCountL.Name = "timeCountL";
            this.timeCountL.Size = new System.Drawing.Size(35, 25);
            this.timeCountL.TabIndex = 6;
            this.timeCountL.Text = "0 s";
            // 
            // collectedObjsCountL
            // 
            this.collectedObjsCountL.AutoSize = true;
            this.collectedObjsCountL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.collectedObjsCountL.Location = new System.Drawing.Point(170, 45);
            this.collectedObjsCountL.Name = "collectedObjsCountL";
            this.collectedObjsCountL.Size = new System.Drawing.Size(22, 25);
            this.collectedObjsCountL.TabIndex = 5;
            this.collectedObjsCountL.Text = "0";
            // 
            // currentObjsCountL
            // 
            this.currentObjsCountL.AutoSize = true;
            this.currentObjsCountL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.currentObjsCountL.Location = new System.Drawing.Point(170, 10);
            this.currentObjsCountL.Name = "currentObjsCountL";
            this.currentObjsCountL.Size = new System.Drawing.Size(22, 25);
            this.currentObjsCountL.TabIndex = 4;
            this.currentObjsCountL.Text = "0";
            // 
            // iterationsTextL
            // 
            this.iterationsTextL.AutoSize = true;
            this.iterationsTextL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.iterationsTextL.Location = new System.Drawing.Point(5, 115);
            this.iterationsTextL.Name = "iterationsTextL";
            this.iterationsTextL.Size = new System.Drawing.Size(96, 25);
            this.iterationsTextL.TabIndex = 3;
            this.iterationsTextL.Text = "Итераций:";
            // 
            // timeTextL
            // 
            this.timeTextL.AutoSize = true;
            this.timeTextL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.timeTextL.Location = new System.Drawing.Point(5, 80);
            this.timeTextL.Name = "timeTextL";
            this.timeTextL.Size = new System.Drawing.Size(134, 25);
            this.timeTextL.TabIndex = 2;
            this.timeTextL.Text = "Время работы:";
            // 
            // collectedObjsTextL
            // 
            this.collectedObjsTextL.AutoSize = true;
            this.collectedObjsTextL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.collectedObjsTextL.Location = new System.Drawing.Point(5, 45);
            this.collectedObjsTextL.Name = "collectedObjsTextL";
            this.collectedObjsTextL.Size = new System.Drawing.Size(172, 25);
            this.collectedObjsTextL.TabIndex = 1;
            this.collectedObjsTextL.Text = "Объектов собрано:";
            // 
            // currentObjsTextL
            // 
            this.currentObjsTextL.AutoSize = true;
            this.currentObjsTextL.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.currentObjsTextL.Location = new System.Drawing.Point(5, 10);
            this.currentObjsTextL.Name = "currentObjsTextL";
            this.currentObjsTextL.Size = new System.Drawing.Size(171, 25);
            this.currentObjsTextL.TabIndex = 0;
            this.currentObjsTextL.Text = "Объектов на карте:";
            // 
            // controlPanel
            // 
            this.controlPanel.Controls.Add(this.startB);
            this.controlPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.controlPanel.Location = new System.Drawing.Point(0, 390);
            this.controlPanel.Name = "controlPanel";
            this.controlPanel.Size = new System.Drawing.Size(240, 122);
            this.controlPanel.TabIndex = 0;
            // 
            // startB
            // 
            this.startB.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.startB.Location = new System.Drawing.Point(60, 43);
            this.startB.Name = "startB";
            this.startB.Size = new System.Drawing.Size(117, 38);
            this.startB.TabIndex = 0;
            this.startB.Text = "Запуск";
            this.startB.UseVisualStyleBackColor = true;
            this.startB.Click += new System.EventHandler(this.startB_Click);
            // 
            // refreshTimer
            // 
            this.refreshTimer.Interval = 10;
            this.refreshTimer.Tick += new System.EventHandler(this.refreshTimer_Tick);
            // 
            // mainCMS
            // 
            this.mainCMS.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mainCMS.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newObjectTSMI});
            this.mainCMS.Name = "contextMenuStrip1";
            this.mainCMS.Size = new System.Drawing.Size(211, 56);
            // 
            // newObjectTSMI
            // 
            this.newObjectTSMI.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newObjTSMI,
            this.newRobotTSMI,
            this.newBaseTSMI});
            this.newObjectTSMI.Name = "newObjectTSMI";
            this.newObjectTSMI.Size = new System.Drawing.Size(210, 24);
            this.newObjectTSMI.Text = "Добавить";
            // 
            // newObjTSMI
            // 
            this.newObjTSMI.Name = "newObjTSMI";
            this.newObjTSMI.Size = new System.Drawing.Size(224, 26);
            this.newObjTSMI.Tag = "0";
            this.newObjTSMI.Text = "Объект";
            this.newObjTSMI.Click += new System.EventHandler(this.newObjTSMI_Click);
            // 
            // newRobotTSMI
            // 
            this.newRobotTSMI.Name = "newRobotTSMI";
            this.newRobotTSMI.Size = new System.Drawing.Size(224, 26);
            this.newRobotTSMI.Tag = "1";
            this.newRobotTSMI.Text = "Робот";
            this.newRobotTSMI.Click += new System.EventHandler(this.newObjTSMI_Click);
            // 
            // newBaseTSMI
            // 
            this.newBaseTSMI.Name = "newBaseTSMI";
            this.newBaseTSMI.Size = new System.Drawing.Size(224, 26);
            this.newBaseTSMI.Tag = "2";
            this.newBaseTSMI.Text = "База";
            this.newBaseTSMI.Click += new System.EventHandler(this.newObjTSMI_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(951, 512);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.Name = "Form1";
            this.Text = "Form1";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.infoPanel.ResumeLayout(false);
            this.infoPanel.PerformLayout();
            this.controlPanel.ResumeLayout(false);
            this.mainCMS.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private SplitContainer splitContainer1;
        private Panel mapPanel;
        private System.Windows.Forms.Timer refreshTimer;
        private Panel controlPanel;
        private ContextMenuStrip mainCMS;
        private ToolStripMenuItem newObjectTSMI;
        private ToolStripMenuItem newObjTSMI;
        private ToolStripMenuItem newRobotTSMI;
        private ToolStripMenuItem newBaseTSMI;
        private Panel infoPanel;
        private Label timeTextL;
        private Label collectedObjsTextL;
        private Label currentObjsTextL;
        private Button startB;
        private Label iterationsTextL;
        private Label currentObjsCountL;
        private Label iterationsCountL;
        private Label timeCountL;
        private Label collectedObjsCountL;
    }
}