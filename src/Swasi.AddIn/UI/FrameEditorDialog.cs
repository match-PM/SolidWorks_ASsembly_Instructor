using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class FrameEditorDialog : Form
    {
        private readonly string frameName;
        private readonly string[] referenceNames;
        private readonly FlowLayoutPanel rolePanel = new FlowLayoutPanel();
        private readonly PropertyGrid propertyGrid = new PropertyGrid();
        private readonly Label constraintSummary = new Label();
        private readonly Button editConstraintButton = new Button();
        private readonly Button inPlaneButton = new Button();
        private readonly Button removeInPlaneButton = new Button();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Dictionary<SwasiFrameRole, Button> roleButtons = new Dictionary<SwasiFrameRole, Button>();

        public SwasiFrameMetadata FrameMetadata { get; private set; }

        public FrameEditorDialog(string name, string geometryType, SwasiFrameMetadata metadata,
            IEnumerable<string> availableReferences)
        {
            frameName = name;
            referenceNames = (availableReferences ?? Enumerable.Empty<string>()).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            FrameMetadata = (metadata ?? new SwasiFrameMetadata()).Clone();

            Text = "Edit " + geometryType.ToLowerInvariant() + " - " + name;
            Width = 560; Height = 650; MinimumSize = new Size(440, 520);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 8 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(new Label { Text = geometryType + ": " + name, AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) }, 0, 0);
            root.Controls.Add(new Label { Text = "Frame type", AutoSize = true }, 0, 1);
            rolePanel.AutoSize = true; rolePanel.Dock = DockStyle.Fill; rolePanel.WrapContents = true;
            root.Controls.Add(rolePanel, 0, 2);
            BuildRoleButtons();
            foreach (var pair in roleButtons)
                pair.Value.Enabled = AssemblyMatchRules.CanAssignRole(geometryType, pair.Key);
            if (geometryType != "Frame")
                FrameMetadata.role &= ~(SwasiFrameRole.Assembly | SwasiFrameRole.Target);
            FrameMetadataApplicator.ApplyRole(FrameMetadata.properties, FrameMetadata.role);

            propertyGrid.Dock = DockStyle.Fill; propertyGrid.ToolbarVisible = false; propertyGrid.HelpVisible = true;
            propertyGrid.SelectedObject = new FramePropertyEditorModel(frameName, FrameMetadata);
            root.Controls.Add(propertyGrid, 0, 3);

            constraintSummary.AutoSize = true; constraintSummary.Margin = new Padding(3, 8, 3, 4);
            root.Controls.Add(constraintSummary, 0, 4);
            var constraintActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            editConstraintButton.Text = "Edit creation constraint..."; editConstraintButton.AutoSize = true;
            editConstraintButton.Click += EditConstraint_Click;
            inPlaneButton.AutoSize = true; inPlaneButton.Click += EditInPlane_Click;
            removeInPlaneButton.Text = "Remove in-plane constraint"; removeInPlaneButton.AutoSize = true;
            removeInPlaneButton.Click += RemoveInPlane_Click;
            constraintActions.Controls.Add(editConstraintButton); constraintActions.Controls.Add(inPlaneButton);
            constraintActions.Controls.Add(removeInPlaneButton); root.Controls.Add(constraintActions, 0, 5);

            var destructiveActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            if (FrameMetadata.isConstraintFrame)
            {
                var delete = new Button { Text = "Delete constraint frame", AutoSize = true, ForeColor = Color.DarkRed };
                delete.Click += (sender, args) => { DialogResult = DialogResult.Abort; Close(); };
                destructiveActions.Controls.Add(delete);
            }
            root.Controls.Add(destructiveActions, 0, 6);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 7);
            AcceptButton = save; CancelButton = cancel;
            UpdateConstraintState(); UpdateRoleSelection();
        }

        private void BuildRoleButtons()
        {
            AddRoleButton(SwasiFrameRole.None, "default_frame.png"); AddRoleButton(SwasiFrameRole.Vision, "vision.png");
            AddRoleButton(SwasiFrameRole.Laser, "laser.png"); AddRoleButton(SwasiFrameRole.Gripping, "robotic-hand.png");
            AddRoleButton(SwasiFrameRole.Target, "target.png"); AddRoleButton(SwasiFrameRole.Assembly, "assembly.png");
            AddRoleButton(SwasiFrameRole.Glue, "glue.png");
        }

        private void AddRoleButton(SwasiFrameRole role, string fileName)
        {
            var button = new Button { Width = 48, Height = 48, Tag = role, Text = role == SwasiFrameRole.None ? "X" : string.Empty };
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "media", "frame-types", fileName);
            try { if (File.Exists(path)) button.Image = new Bitmap(new Bitmap(path), new Size(30, 30)); }
            catch { button.Text = role == SwasiFrameRole.None ? "X" : role.ToString().Substring(0, 1); }
            button.Click += RoleButton_Click;
            toolTip.SetToolTip(button, role == SwasiFrameRole.None ? "Clear frame type" : role.ToString());
            roleButtons[role] = button; rolePanel.Controls.Add(button);
        }

        private void RoleButton_Click(object sender, EventArgs e)
        {
            var role = (SwasiFrameRole)((Button)sender).Tag;
            FrameMetadata.role = role == SwasiFrameRole.None
                ? SwasiFrameRole.None
                : (FrameMetadata.role.HasFlag(role) ? FrameMetadata.role & ~role : FrameMetadata.role | role);
            FrameMetadataApplicator.ApplyRole(FrameMetadata.properties, FrameMetadata.role);
            propertyGrid.SelectedObject = new FramePropertyEditorModel(frameName, FrameMetadata);
            UpdateRoleSelection();
        }

        private void UpdateRoleSelection()
        {
            foreach (var pair in roleButtons)
            {
                bool selected = pair.Key == SwasiFrameRole.None
                    ? FrameMetadata.role == SwasiFrameRole.None
                    : FrameMetadata.role.HasFlag(pair.Key);
                pair.Value.FlatStyle = selected ? FlatStyle.Flat : FlatStyle.Standard;
                pair.Value.FlatAppearance.BorderSize = selected ? 3 : 1;
                pair.Value.BackColor = selected ? SystemColors.Highlight : SystemColors.Control;
            }
        }

        private void EditConstraint_Click(object sender, EventArgs e)
        {
            using (var dialog = new ConstraintEditorDialog(FrameMetadata.constraintKind, referenceNames, frameName, FrameMetadata))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                FrameMetadata = dialog.FrameMetadata;
                propertyGrid.SelectedObject = new FramePropertyEditorModel(frameName, FrameMetadata);
                UpdateConstraintState(); UpdateRoleSelection();
            }
        }

        private void EditInPlane_Click(object sender, EventArgs e)
        {
            using (var dialog = ConstraintEditorDialog.ForInPlane(referenceNames, frameName,
                FrameMetadata.constraints.inPlane, FrameMetadata.inPlaneToleranceMm))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                FrameMetadata.constraints.inPlane = dialog.InPlaneConstraint;
                FrameMetadata.inPlaneToleranceMm = dialog.InPlaneToleranceMm;
                UpdateConstraintState();
            }
        }

        private void RemoveInPlane_Click(object sender, EventArgs e)
        { FrameMetadata.constraints.inPlane = new RefFrameInPlaneConstraint(); UpdateConstraintState(); }

        private void UpdateConstraintState()
        {
            bool hasInPlane = FrameMetadata.constraints?.inPlane?.refFrameNames?.Count > 0;
            constraintSummary.Text = "Constraints: " + (FrameMetadata.isConstraintFrame
                ? FrameMetadata.constraintKind.ToString() : "no creation constraint")
                + (hasInPlane ? ", in-plane" : string.Empty);
            editConstraintButton.Visible = FrameMetadata.isConstraintFrame;
            inPlaneButton.Text = hasInPlane ? "Edit in-plane constraint..." : "Add in-plane constraint...";
            removeInPlaneButton.Visible = hasInPlane;
        }

    }
}
