using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class AssemblyMatchesDialog : Form
    {
        private readonly ModelDoc2 document;
        private readonly AssemblyMatchManager manager = new AssemblyMatchManager();
        private readonly DataGridView grid = new DataGridView();
        private readonly List<AssemblyFrameCandidate> candidates;
        private readonly Dictionary<string, AssemblyFrameEndpoint> endpoints = new Dictionary<string, AssemblyFrameEndpoint>();
        private readonly string configuration;

        public AssemblyMatchesDialog(ModelDoc2 document)
        {
            this.document = document;
            configuration = AssemblyMatchManager.Configuration(document);
            Text = "Assembly Matches — " + configuration; Size = new Size(1050, 520); MinimumSize = new Size(760, 350);
            StartPosition = FormStartPosition.CenterParent;
            var warnings = new List<string>();
            candidates = manager.ReadCandidates(document, warnings);
            var matches = manager.ReadMatches(document);
            grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
            grid.Columns.Add("Name", "Match name");
            foreach (var role in new[] { SwasiFrameRole.Assembly, SwasiFrameRole.Target })
            {
                var column = new DataGridViewComboBoxColumn { Name = role.ToString(), HeaderText = role + " frame", FillWeight = 180,
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Standard };
                column.Items.Add("");
                var choices = candidates.Where(c => c.Option.role.HasFlag(role)).Select(c => c.Option.endpoint)
                    .Concat(matches.Select(m => role == SwasiFrameRole.Assembly ? m.assemblyFrame : m.targetFrame).Where(e => e != null));
                foreach (var endpoint in choices.GroupBy(e => e.Key).Select(g => g.First()))
                {
                    string label = endpoint.ToString();
                    endpoints[label] = endpoint;
                    column.Items.Add(label);
                }
                grid.Columns.Add(column);
            }
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", ReadOnly = true, FillWeight = 140 });
            grid.DataError += (s, e) => { e.ThrowException = false; if (e.RowIndex >= 0) grid.Rows[e.RowIndex].ErrorText = "Select a frame from the list."; };
            foreach (var match in matches)
            {
                int index = grid.Rows.Add(match.name, match.assemblyFrame?.ToString() ?? "", match.targetFrame?.ToString() ?? "",
                    manager.Status(document, match, candidates));
                grid.Rows[index].Tag = match;
            }
            new AssemblyMatchChoices(grid,
                candidates.Where(c => c.Option.role.HasFlag(SwasiFrameRole.Assembly)).Select(c => c.Option.endpoint.ToString()),
                candidates.Where(c => c.Option.role.HasFlag(SwasiFrameRole.Target)).Select(c => c.Option.endpoint.ToString()));
            var header = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8),
                Text = "Match coordinate frames on first-level components. Origins and all axes will coincide.\n" +
                    "Each target and assembly frame can be used once. Removing a row removes its generated mate on Apply." +
                    "\nComponent 1 = Assembly component (moves to target); component 2 = Target component." +
                    (warnings.Count == 0 ? "" : "\n" + string.Join("\n", warnings)) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            var add = new Button { Text = "Add match", AutoSize = true };
            var remove = new Button { Text = "Remove selected", AutoSize = true };
            var apply = new Button { Text = "Apply", AutoSize = true };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            add.Click += (s, e) =>
            {
                var used = new HashSet<string>(grid.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells[0].Value)));
                int n = 1; while (used.Contains("Match_" + n)) n++;
                int index = grid.Rows.Add("Match_" + n, "", "", "New");
                grid.Rows[index].Tag = new AssemblyFrameMatch(); grid.CurrentCell = grid.Rows[index].Cells[0];
            };
            remove.Click += (s, e) => { if (grid.CurrentRow != null) grid.Rows.Remove(grid.CurrentRow); };
            apply.Click += (s, e) =>
            {
                try
                {
                    if (AssemblyMatchManager.Configuration(document) != configuration)
                        throw new InvalidOperationException("The active configuration changed. Reopen Assembly Matches.");
                    grid.EndEdit();
                    var requested = new List<AssemblyFrameMatch>();
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        var match = ((AssemblyFrameMatch)row.Tag).Clone();
                        match.name = Convert.ToString(row.Cells[0].Value).Trim();
                        endpoints.TryGetValue(Convert.ToString(row.Cells[1].Value), out match.assemblyFrame);
                        endpoints.TryGetValue(Convert.ToString(row.Cells[2].Value), out match.targetFrame);
                        requested.Add(match);
                    }
                    manager.Apply(document, requested);
                    DialogResult = DialogResult.OK; Close();
                }
                catch (Exception ex) { CopyableMessageDialog.ShowMessage(this, "Assembly Matches", ex.Message, ex.ToString()); }
            };
            buttons.Controls.AddRange(new Control[] { add, remove, apply, cancel });
            Controls.Add(grid); Controls.Add(header); Controls.Add(buttons); CancelButton = cancel;
        }
    }
}
