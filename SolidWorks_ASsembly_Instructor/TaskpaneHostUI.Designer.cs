namespace SolidWorks_ASsembly_Instructor
{
    partial class TaskpaneHostUI
    {
        /// <summary> 
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Komponenten-Designer generierter Code

        /// <summary> 
        /// Erforderliche Methode für die Designerunterstützung. 
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TaskpaneHostUI));
            this.exportJson = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.label2 = new System.Windows.Forms.Label();
            this.tb_BrowseFolder = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btn_clearLog = new System.Windows.Forms.Button();
            this.rtDebug = new System.Windows.Forms.RichTextBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.lbl_Version_No = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // exportJson
            // 
            this.exportJson.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.exportJson.Location = new System.Drawing.Point(4, 156);
            this.exportJson.Margin = new System.Windows.Forms.Padding(4);
            this.exportJson.Name = "exportJson";
            this.exportJson.Size = new System.Drawing.Size(444, 28);
            this.exportJson.TabIndex = 0;
            this.exportJson.Text = "Export as Json";
            this.exportJson.UseVisualStyleBackColor = true;
            this.exportJson.Click += new System.EventHandler(this.exportJson_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(4, 106);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(133, 16);
            this.label2.TabIndex = 3;
            this.label2.Text = "Choose output folder:";
            // 
            // tb_BrowseFolder
            // 
            this.tb_BrowseFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tb_BrowseFolder.Location = new System.Drawing.Point(4, 126);
            this.tb_BrowseFolder.Margin = new System.Windows.Forms.Padding(4);
            this.tb_BrowseFolder.Name = "tb_BrowseFolder";
            this.tb_BrowseFolder.Size = new System.Drawing.Size(443, 22);
            this.tb_BrowseFolder.TabIndex = 4;
            this.tb_BrowseFolder.Text = "Browse folder";
            this.tb_BrowseFolder.Click += new System.EventHandler(this.tb_BrowseFolder_Click);
            this.tb_BrowseFolder.TextChanged += new System.EventHandler(this.tb_BrowseFolder_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(0, 0);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(385, 39);
            this.label3.TabIndex = 5;
            this.label3.Text = "SW ASsembly Instructor";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btn_clearLog
            // 
            this.btn_clearLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_clearLog.Location = new System.Drawing.Point(4, 458);
            this.btn_clearLog.Margin = new System.Windows.Forms.Padding(4);
            this.btn_clearLog.Name = "btn_clearLog";
            this.btn_clearLog.Size = new System.Drawing.Size(390, 28);
            this.btn_clearLog.TabIndex = 9;
            this.btn_clearLog.Text = "Clear log";
            this.btn_clearLog.UseVisualStyleBackColor = true;
            this.btn_clearLog.Click += new System.EventHandler(this.btn_clearLog_Click);
            // 
            // rtDebug
            // 
            this.rtDebug.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtDebug.Location = new System.Drawing.Point(4, 192);
            this.rtDebug.Margin = new System.Windows.Forms.Padding(4);
            this.rtDebug.Name = "rtDebug";
            this.rtDebug.Size = new System.Drawing.Size(443, 258);
            this.rtDebug.TabIndex = 10;
            this.rtDebug.Text = "";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(4, 42);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(444, 60);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBox1.TabIndex = 6;
            this.pictureBox1.TabStop = false;
            // 
            // lbl_Version_No
            // 
            this.lbl_Version_No.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl_Version_No.AutoSize = true;
            this.lbl_Version_No.Location = new System.Drawing.Point(401, 470);
            this.lbl_Version_No.Name = "lbl_Version_No";
            this.lbl_Version_No.Size = new System.Drawing.Size(46, 16);
            this.lbl_Version_No.TabIndex = 11;
            this.lbl_Version_No.Text = "V 0.1.0";
            this.lbl_Version_No.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // TaskpaneHostUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lbl_Version_No);
            this.Controls.Add(this.rtDebug);
            this.Controls.Add(this.btn_clearLog);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tb_BrowseFolder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.exportJson);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "TaskpaneHostUI";
            this.Size = new System.Drawing.Size(452, 489);
            this.Load += new System.EventHandler(this.TaskpaneHostUI_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button exportJson;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tb_BrowseFolder;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btn_clearLog;
        private System.Windows.Forms.RichTextBox rtDebug;
        private System.Windows.Forms.Label lbl_Version_No;
    }
}
