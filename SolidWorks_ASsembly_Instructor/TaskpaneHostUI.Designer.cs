namespace JsonExporter_2
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
            this.exportJson = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.label2 = new System.Windows.Forms.Label();
            this.tb_BrowseFolder = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.refreshPartList = new System.Windows.Forms.Button();
            this.rtDebug = new System.Windows.Forms.RichTextBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // exportJson
            // 
            this.exportJson.Location = new System.Drawing.Point(3, 157);
            this.exportJson.Name = "exportJson";
            this.exportJson.Size = new System.Drawing.Size(333, 23);
            this.exportJson.TabIndex = 0;
            this.exportJson.Text = "Export as Json";
            this.exportJson.UseVisualStyleBackColor = true;
            this.exportJson.Click += new System.EventHandler(this.exportJson_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 115);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(72, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Save location";
            // 
            // tb_BrowseFolder
            // 
            this.tb_BrowseFolder.Location = new System.Drawing.Point(3, 131);
            this.tb_BrowseFolder.Name = "tb_BrowseFolder";
            this.tb_BrowseFolder.Size = new System.Drawing.Size(333, 20);
            this.tb_BrowseFolder.TabIndex = 4;
            this.tb_BrowseFolder.Text = "Browse folder";
            this.tb_BrowseFolder.Click += new System.EventHandler(this.tb_BrowseFolder_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(339, 31);
            this.label3.TabIndex = 5;
            this.label3.Text = "Json Assambly exporter by";
            // 
            // refreshPartList
            // 
            this.refreshPartList.Location = new System.Drawing.Point(3, 89);
            this.refreshPartList.Name = "refreshPartList";
            this.refreshPartList.Size = new System.Drawing.Size(333, 23);
            this.refreshPartList.TabIndex = 9;
            this.refreshPartList.Text = "Referesh part list";
            this.refreshPartList.UseVisualStyleBackColor = true;
            this.refreshPartList.Click += new System.EventHandler(this.refreshPartList_Click);
            // 
            // rtDebug
            // 
            this.rtDebug.Location = new System.Drawing.Point(3, 186);
            this.rtDebug.Name = "rtDebug";
            this.rtDebug.Size = new System.Drawing.Size(333, 929);
            this.rtDebug.TabIndex = 10;
            this.rtDebug.Text = "";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::JsonExporter_2.Properties.Resources.match_logo;
            this.pictureBox1.Location = new System.Drawing.Point(3, 34);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(333, 49);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBox1.TabIndex = 6;
            this.pictureBox1.TabStop = false;
            // 
            // TaskpaneHostUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.rtDebug);
            this.Controls.Add(this.refreshPartList);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tb_BrowseFolder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.exportJson);
            this.Name = "TaskpaneHostUI";
            this.Size = new System.Drawing.Size(339, 1118);
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
        private System.Windows.Forms.Button refreshPartList;
        private System.Windows.Forms.RichTextBox rtDebug;
    }
}
