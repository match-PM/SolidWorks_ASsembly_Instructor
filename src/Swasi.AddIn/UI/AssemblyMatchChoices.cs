using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    // Each combo cell owns its choices: a column-wide list cannot account for
    // frames reserved by other rows, including edits not yet applied to CAD.
    internal sealed class AssemblyMatchChoices
    {
        private readonly DataGridView grid;
        private readonly string[][] available;
        private bool refreshing;

        public AssemblyMatchChoices(DataGridView grid, IEnumerable<string> assemblies, IEnumerable<string> targets)
        {
            this.grid = grid;
            available = new[] { assemblies.Distinct(StringComparer.Ordinal).ToArray(), targets.Distinct(StringComparer.Ordinal).ToArray() };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (!refreshing && grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += (s, e) => { if (e.ColumnIndex == 1 || e.ColumnIndex == 2) Refresh(); };
            grid.RowsAdded += (s, e) => Refresh();
            grid.RowsRemoved += (s, e) => Refresh();
            Refresh();
        }

        private void Refresh()
        {
            if (refreshing || grid.IsDisposed || grid.Disposing || grid.Columns.Count < 3) return;
            refreshing = true;
            try
            {
                var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToArray();
                for (int column = 1; column <= 2; column++)
                {
                    foreach (var row in rows)
                    {
                        var cell = (DataGridViewComboBoxCell)row.Cells[column];
                        string current = Convert.ToString(cell.Value);
                        var occupied = new HashSet<string>(rows.Where(r => r != row)
                            .Select(r => Convert.ToString(r.Cells[column].Value)), StringComparer.Ordinal);
                        var choices = new List<string> { "" };
                        choices.AddRange(available[column - 1].Where(value => !occupied.Contains(value) || value == current));
                        // Keep a missing saved endpoint visible only in its own row,
                        // so it can be repaired without becoming selectable elsewhere.
                        if (!choices.Contains(current)) choices.Add(current);
                        if (cell.Items.Cast<string>().SequenceEqual(choices)) continue;
                        cell.Items.Clear();
                        cell.Items.AddRange(choices.Cast<object>().ToArray());
                    }
                }
            }
            finally { refreshing = false; }
        }
    }
}
