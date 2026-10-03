using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class LogWindow : Form
    {
        public LogWindow(IEnumerable<LogEntry> entries)
        {
            Text = "SWASI export log"; Width = 760; Height = 480; StartPosition = FormStartPosition.CenterParent;
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9F), BackColor = Color.White };
            foreach (var entry in entries)
            {
                text.SelectionColor = entry.Level.ToLowerInvariant() == "error" ? Color.Firebrick
                    : entry.Level.ToLowerInvariant() == "warning" ? Color.DarkOrange : Color.Black;
                text.AppendText(entry + System.Environment.NewLine);
            }
            var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 32, DialogResult = DialogResult.OK };
            Controls.Add(text); Controls.Add(close); AcceptButton = close;
        }
    }
}
