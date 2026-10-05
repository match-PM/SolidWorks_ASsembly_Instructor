using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class ConstraintImportSelectionDialog : Form
    {
        private readonly CheckedListBox frames = new CheckedListBox();
        public string[] SelectedFrames => frames.CheckedItems.Cast<string>().ToArray();

        public ConstraintImportSelectionDialog(IEnumerable<string> names)
        {
            Text = "Select constrained frames to import";
            Size = new Size(580, 480); MinimumSize = new Size(420, 320);
            StartPosition = FormStartPosition.CenterParent;
            frames.Dock = DockStyle.Fill; frames.CheckOnClick = true; frames.HorizontalScrollbar = true;
            foreach (string name in names) frames.Items.Add(name, true);
            var instructions = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8),
                Text = "Select the frames to import. Existing frames will be kept.\n" +
                    "Dependencies must already exist or also be selected; unresolved frames will be skipped." };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            var all = new Button { Text = "Select all", AutoSize = true };
            var none = new Button { Text = "Select none", AutoSize = true };
            var import = new Button { Text = "Import selected", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            all.Click += (s, e) => SetChecked(true);
            none.Click += (s, e) => SetChecked(false);
            buttons.Controls.AddRange(new Control[] { all, none, import, cancel });
            Controls.Add(frames); Controls.Add(instructions); Controls.Add(buttons);
            AcceptButton = import; CancelButton = cancel;
        }

        private void SetChecked(bool value)
        {
            for (int i = 0; i < frames.Items.Count; i++) frames.SetItemChecked(i, value);
        }
    }
}
