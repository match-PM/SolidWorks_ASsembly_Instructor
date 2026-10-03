using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class ConstraintEditorDialog : Form
    {
        private readonly ConstraintKind kind;
        private readonly bool inPlaneMode;
        private readonly TextBox nameBox = new TextBox();
        private readonly CheckedListBox references = new CheckedListBox();
        private readonly ComboBox frame1 = Combo();
        private readonly ComboBox frame2 = Combo();
        private readonly ComboBox frame3 = Combo();
        private readonly ComboBox axis1 = Combo();
        private readonly ComboBox axis2 = Combo();
        private readonly ComboBox units = Combo();
        private readonly NumericUpDown x = Number(); private readonly NumericUpDown y = Number(); private readonly NumericUpDown z = Number();
        private readonly NumericUpDown qx = Number(4); private readonly NumericUpDown qy = Number(4); private readonly NumericUpDown qz = Number(4); private readonly NumericUpDown qw = Number(4);
        private readonly NumericUpDown distance1 = Number(); private readonly NumericUpDown distance2 = Number();
        private readonly NumericUpDown tolerance = Number();
        private readonly SwasiFrameMetadata original;

        public string FrameName => nameBox.Text.Trim();
        public SwasiFrameMetadata FrameMetadata { get; private set; }
        public RefFrameInPlaneConstraint InPlaneConstraint { get; private set; }
        public float InPlaneToleranceMm => (float)tolerance.Value;

        public ConstraintEditorDialog(ConstraintKind kind, IEnumerable<string> frameNames,
            string existingName, SwasiFrameMetadata existing)
            : this(kind, frameNames, existingName, existing, false) { }

        private ConstraintEditorDialog(ConstraintKind kind, IEnumerable<string> frameNames,
            string existingName, SwasiFrameMetadata existing, bool inPlaneMode)
        {
            this.kind = kind; this.inPlaneMode = inPlaneMode; original = existing;
            Text = inPlaneMode ? "Assign in-plane check" : existingName == null ? "Add " + kind + " constraint frame" : "Edit " + kind + " constraint";
            Width = 500; Height = 560; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, AutoScroll = true };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            var names = frameNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            references.Items.AddRange(names); frame1.Items.AddRange(names); frame2.Items.AddRange(names); frame3.Items.AddRange(names);
            axis1.Items.AddRange(new object[] { "x", "y", "z", "-x", "-y", "-z" });
            axis2.Items.AddRange(new object[] { "x", "y", "z", "-x", "-y", "-z" });
            units.Items.AddRange(new object[] { "%", "mm" });

            if (!inPlaneMode)
            {
                Add(root, "Frame name", nameBox); nameBox.Text = existingName ?? string.Empty; nameBox.ReadOnly = existingName != null;
                BuildPrimary(root);
            }
            else BuildInPlane(root, existingName);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Apply", AutoSize = true }; var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            ok.Click += Apply_Click; buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, root.RowCount); root.SetColumnSpan(buttons, 2);
            AcceptButton = ok; CancelButton = cancel;
        }

        public static ConstraintEditorDialog ForCopy(IEnumerable<string> frameNames, string suggestedName,
            SwasiFrameMetadata source)
        {
            var dialog = new ConstraintEditorDialog(source.constraintKind, frameNames, null, source.Clone());
            dialog.Text = "Paste constraint frame";
            dialog.nameBox.Text = suggestedName;
            return dialog;
        }

        public static ConstraintEditorDialog ForInPlane(IEnumerable<string> frameNames, string targetName,
            RefFrameInPlaneConstraint existing, float toleranceMm = .01f)
        {
            var metadata = new SwasiFrameMetadata(); metadata.constraints.inPlane = existing ?? new RefFrameInPlaneConstraint();
            metadata.inPlaneToleranceMm = toleranceMm;
            return new ConstraintEditorDialog(ConstraintKind.None, frameNames, targetName, metadata, true);
        }

        private void BuildPrimary(TableLayoutPanel root)
        {
            switch (kind)
            {
                case ConstraintKind.Centroid:
                    Add(root, "Reference frames", references); references.Height = 170;
                    Add(root, "Local offset X [mm]", x); Add(root, "Local offset Y [mm]", y); Add(root, "Local offset Z [mm]", z);
                    if (original != null)
                    {
                        Check(original.constraints.centroid.refFrameNames);
                        SetVector(original.constraints.centroid.offsetValues, x, y, z);
                    }
                    break;
                case ConstraintKind.Orthogonal:
                    Add(root, "Frame 1", frame1); Add(root, "Frame 2", frame2); Add(root, "Frame 3", frame3);
                    Add(root, "Distance from frame 1", distance1); Add(root, "Distance unit", units);
                    Add(root, "Orthogonal distance [mm]", distance2); Add(root, "Orthogonal axis", axis1); Add(root, "Plane-normal axis", axis2);
                    units.SelectedItem = "%"; axis1.SelectedItem = "x"; axis2.SelectedItem = "z";
                    if (original != null)
                    {
                        var o = original.constraints.orthogonal;
                        frame1.SelectedItem = o.frame_1; frame2.SelectedItem = o.frame_2; frame3.SelectedItem = o.frame_3;
                        distance1.Value = DecimalValue(o.distance_from_f1); distance2.Value = DecimalValue(o.distance_from_f1_f2_connection);
                        units.SelectedItem = o.unit_distance_from_f1; axis1.SelectedItem = o.frame_orthogonal_connection_axis; axis2.SelectedItem = o.frame_normal_plane_axis;
                    }
                    break;
                case ConstraintKind.Transform:
                    Add(root, "Reference frame", frame1); Add(root, "Translation X [mm]", x); Add(root, "Translation Y [mm]", y); Add(root, "Translation Z [mm]", z);
                    Add(root, "Quaternion X", qx); Add(root, "Quaternion Y", qy); Add(root, "Quaternion Z", qz); Add(root, "Quaternion W", qw); qw.Value = 1;
                    if (original != null)
                    {
                        var t = original.constraints.transform; frame1.SelectedItem = t.refFrame;
                        x.Value = DecimalValue(t.transform.translation.X); y.Value = DecimalValue(t.transform.translation.Y); z.Value = DecimalValue(t.transform.translation.Z);
                        qx.Value = DecimalValue(t.transform.rotation.X); qy.Value = DecimalValue(t.transform.rotation.Y);
                        qz.Value = DecimalValue(t.transform.rotation.Z); qw.Value = DecimalValue(t.transform.rotation.W);
                    }
                    break;
            }
        }

        private void BuildInPlane(TableLayoutPanel root, string targetName)
        {
            nameBox.Text = targetName;
            Add(root, "Frame", new Label { Text = targetName, AutoSize = true }); Add(root, "Plane references", references); references.Height = 190;
            Add(root, "Plane offset [mm]", distance1); Add(root, "Normal axis", axis1); Add(root, "Warning tolerance [mm]", tolerance);
            axis1.Items.Clear(); axis1.Items.AddRange(new object[] { "x", "y", "z" }); axis1.SelectedItem = "z"; tolerance.Value = .01M;
            var existing = original?.constraints.inPlane;
            if (existing != null)
            { Check(existing.refFrameNames); distance1.Value = DecimalValue(existing.planeOffset); axis1.SelectedItem = existing.normalAxis; }
            if (original != null) tolerance.Value = DecimalValue(original.inPlaneToleranceMm);
        }

        private void Apply_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (var number in new[] { x, y, z, qx, qy, qz, qw, distance1, distance2, tolerance })
                    if (number.Parent != null && !((ConstraintNumberBox)number).TryCommit())
                    {
                        number.Focus();
                        throw new InvalidOperationException("Enter a number between " + number.Minimum + " and " + number.Maximum
                            + ". Use a dot or comma as the decimal separator, without thousands separators.");
                    }
                if (inPlaneMode)
                {
                    var selected = CheckedNames(); if (selected.Count < 3) throw new InvalidOperationException("Select at least three plane references.");
                    InPlaneConstraint = new RefFrameInPlaneConstraint { refFrameNames = selected, planeOffset = (float)distance1.Value, normalAxis = SelectedText(axis1) };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(FrameName)) throw new InvalidOperationException("Enter a frame name.");
                    FrameMetadata = original == null ? new SwasiFrameMetadata() : original.Clone();
                    FrameMetadata.isConstraintFrame = true; FrameMetadata.constraintKind = kind;
                    switch (kind)
                    {
                        case ConstraintKind.Centroid:
                            var selected = CheckedNames(); if (selected.Count == 0) throw new InvalidOperationException("Select at least one reference frame.");
                            FrameMetadata.constraints.centroid = new RefFrameCentroidConstraint { refFrameNames = selected, dim = "xyz",
                                offsetValues = new List<float> { (float)x.Value, (float)y.Value, (float)z.Value } };
                            break;
                        case ConstraintKind.Orthogonal:
                            Require(frame1, frame2, frame3, axis1, axis2, units);
                            if (new[] { SelectedText(frame1), SelectedText(frame2), SelectedText(frame3) }.Distinct().Count() != 3) throw new InvalidOperationException("Choose three different reference frames.");
                            FrameMetadata.constraints.orthogonal = new RefFrameOrthogonalConstraint { frame_1 = SelectedText(frame1), frame_2 = SelectedText(frame2), frame_3 = SelectedText(frame3),
                                distance_from_f1 = (float)distance1.Value, unit_distance_from_f1 = SelectedText(units), distance_from_f1_f2_connection = (float)distance2.Value,
                                frame_orthogonal_connection_axis = SelectedText(axis1), frame_normal_plane_axis = SelectedText(axis2) };
                            break;
                        case ConstraintKind.Transform:
                            Require(frame1); var quaternion = new Quaternion((float)qx.Value, (float)qy.Value, (float)qz.Value, (float)qw.Value);
                            if (quaternion.LengthSquared() < 1e-8f) throw new InvalidOperationException("The transform quaternion cannot be zero.");
                            // Repeated normalization can change the last stored bits
                            // even when the user only edits a name or translation.
                            if (original == null || quaternion != original.constraints.transform.transform.rotation)
                                quaternion = Quaternion.Normalize(quaternion);
                            FrameMetadata.constraints.transform = new RefFrameTransformConstraint { refFrame = SelectedText(frame1),
                                transform = new CoordinateSystemDescription(new Vector3((float)x.Value, (float)y.Value, (float)z.Value), quaternion) };
                            break;
                    }
                }
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { CopyableMessageDialog.ShowMessage(this, "SWASI constraint", ex.Message); }
        }

        private List<string> CheckedNames() => references.CheckedItems.Cast<string>().ToList();
        private void Check(IEnumerable<string> values)
        { var set = new HashSet<string>(values ?? Enumerable.Empty<string>()); for (int i = 0; i < references.Items.Count; i++) references.SetItemChecked(i, set.Contains((string)references.Items[i])); }
        private static void SetVector(IReadOnlyList<float> values, params NumericUpDown[] controls)
        { if (values == null) return; for (int i = 0; i < controls.Length && i < values.Count; i++) controls[i].Value = DecimalValue(values[i]); }
        private static void Add(TableLayoutPanel panel, string label, Control control)
        { int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 8, 3) }, 0, row); control.Dock = DockStyle.Fill; panel.Controls.Add(control, 1, row); }
        private static ComboBox Combo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private static NumericUpDown Number(int decimals = 3) => new ConstraintNumberBox { DecimalPlaces = 9, Minimum = -1000000, Maximum = 1000000, Increment = decimals == 4 ? .01M : .1M };
        private static decimal DecimalValue(float value) => Math.Max(-1000000M, Math.Min(1000000M,
            decimal.Parse(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)));
        private static string SelectedText(ComboBox box) => box.SelectedItem as string ?? string.Empty;
        private static void Require(params ComboBox[] boxes) { if (boxes.Any(b => b.SelectedItem == null)) throw new InvalidOperationException("Complete all selections."); }
    }
}
