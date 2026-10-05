namespace SolidWorks_ASsembly_Instructor
{
    partial class TaskpaneHostUI
    {
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.TextBox outputPathTextBox;
        private System.Windows.Forms.Button browseButton;
        private System.Windows.Forms.ComboBox originComboBox;
        private System.Windows.Forms.Button assignOriginButton;
        private System.Windows.Forms.Button colorButton;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Button assemblyMatchesButton;
        private System.Windows.Forms.DataGridView frameGrid;
        private System.Windows.Forms.Button addConstraintButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Label versionLabel;
        private System.Windows.Forms.ToolTip toolTip;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachDocumentEvents();
                frameContextMenu?.Dispose(); addConstraintMenu?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            toolTip = new System.Windows.Forms.ToolTip(components);
            var root = new System.Windows.Forms.TableLayoutPanel();
            var pathPanel = new System.Windows.Forms.TableLayoutPanel();
            var originPanel = new System.Windows.Forms.TableLayoutPanel();
            var actionPanel = new System.Windows.Forms.FlowLayoutPanel();
            var title = new System.Windows.Forms.Label();
            var outputLabel = new System.Windows.Forms.Label();
            var originLabel = new System.Windows.Forms.Label();
            var frameLabel = new System.Windows.Forms.Label();
            outputPathTextBox = new System.Windows.Forms.TextBox(); browseButton = new System.Windows.Forms.Button();
            originComboBox = new System.Windows.Forms.ComboBox(); assignOriginButton = new System.Windows.Forms.Button();
            colorButton = new System.Windows.Forms.Button(); refreshButton = new System.Windows.Forms.Button();
            assemblyMatchesButton = new System.Windows.Forms.Button();
            frameGrid = new System.Windows.Forms.DataGridView(); addConstraintButton = new System.Windows.Forms.Button();
            exportButton = new System.Windows.Forms.Button();
            versionLabel = new System.Windows.Forms.Label();

            SuspendLayout();
            root.Dock = System.Windows.Forms.DockStyle.Fill; root.Padding = new System.Windows.Forms.Padding(6);
            root.ColumnCount = 1; root.RowCount = 9;
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

            title.Text = "SWASI Frame Editor"; title.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            title.AutoSize = true; title.Dock = System.Windows.Forms.DockStyle.Fill; title.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            root.Controls.Add(title, 0, 0);

            outputLabel.Text = "Output folder (local or network/UNC):"; outputLabel.AutoSize = true; root.Controls.Add(outputLabel, 0, 1);
            pathPanel.ColumnCount = 2; pathPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            pathPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            pathPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            outputPathTextBox.Dock = System.Windows.Forms.DockStyle.Fill; outputPathTextBox.TextChanged += outputPathTextBox_TextChanged;
            browseButton.Text = "Browse..."; browseButton.AutoSize = true; browseButton.Click += browseButton_Click;
            pathPanel.Controls.Add(outputPathTextBox, 0, 0); pathPanel.Controls.Add(browseButton, 1, 0); root.Controls.Add(pathPanel, 0, 2);

            originLabel.Text = "SWASI origin:"; originLabel.AutoSize = true; root.Controls.Add(originLabel, 0, 3);
            originPanel.ColumnCount = 2; originPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            originPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            originPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            originComboBox.Dock = System.Windows.Forms.DockStyle.Fill; originComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            originComboBox.Items.AddRange(new object[] {
                "Gonio_Right_Part_1_Origin",
                "Gonio_Right_Part_2_Origin",
                "Gonio_Left_Part_Origin",
                "Smarpod_Part_Spawn",
                "Smarpod_Part_Spawn_Center"
            });
            assignOriginButton.Text = "Assign"; assignOriginButton.AutoSize = true; assignOriginButton.Click += assignOriginButton_Click;
            originPanel.Controls.Add(originComboBox, 0, 0); originPanel.Controls.Add(assignOriginButton, 1, 0); root.Controls.Add(originPanel, 0, 4);

            actionPanel.Dock = System.Windows.Forms.DockStyle.Fill; actionPanel.AutoSize = true;
            refreshButton.Text = "Update frames"; refreshButton.AutoSize = true; refreshButton.Click += refreshButton_Click;
            colorButton.Text = "Component color"; colorButton.AutoSize = true; colorButton.Click += colorButton_Click;
            actionPanel.Controls.Add(refreshButton); actionPanel.Controls.Add(colorButton); root.Controls.Add(actionPanel, 0, 5);
            assemblyMatchesButton.Text = "Assembly Matches"; assemblyMatchesButton.AutoSize = true; assemblyMatchesButton.Enabled = false;
            assemblyMatchesButton.Click += assemblyMatchesButton_Click; actionPanel.Controls.Add(assemblyMatchesButton);

            frameLabel.Text = "SWASI points and frames:"; frameLabel.AutoSize = true; root.Controls.Add(frameLabel, 0, 6);
            frameGrid.Dock = System.Windows.Forms.DockStyle.Fill; frameGrid.AllowUserToAddRows = false; frameGrid.AllowUserToDeleteRows = false;
            frameGrid.AllowUserToResizeRows = false; frameGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            frameGrid.BackgroundColor = System.Drawing.SystemColors.Window; frameGrid.MultiSelect = false; frameGrid.ReadOnly = true;
            frameGrid.RowHeadersVisible = false; frameGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            frameGrid.Columns.Add("Name", "Name"); frameGrid.Columns.Add("Geometry", "Geometry");
            frameGrid.Columns.Add("Role", "Type"); frameGrid.Columns.Add("Constraint", "Constraint");
            frameGrid.Columns["Constraint"].DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            frameGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            frameGrid.CellMouseDown += frameGrid_CellMouseDown;
            frameGrid.CellDoubleClick += frameGrid_CellDoubleClick;
            root.Controls.Add(frameGrid, 0, 7);

            addConstraintButton.Text = "Add constraint frame..."; addConstraintButton.AutoSize = true;
            addConstraintButton.Dock = System.Windows.Forms.DockStyle.Fill; addConstraintButton.Height = 32;
            addConstraintButton.Click += addConstraintButton_Click; root.Controls.Add(addConstraintButton, 0, 8);

            exportButton.Text = "Export JSON and STL"; exportButton.Dock = System.Windows.Forms.DockStyle.Bottom; exportButton.Height = 36;
            exportButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); exportButton.Click += exportButton_Click;
            versionLabel.Text = "V1.1.0"; versionLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            versionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight; versionLabel.Height = 18;
            Controls.Add(root); Controls.Add(versionLabel); Controls.Add(exportButton);
            Name = "TaskpaneHostUI"; MinimumSize = new System.Drawing.Size(340, 620); Size = new System.Drawing.Size(420, 760);
            ResumeLayout(false);
        }
    }
}
