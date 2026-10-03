using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class CopyableMessageDialog : Form
    {
        public CopyableMessageDialog(string title, string message, string details = null)
        {
            Text = title;
            Size = new Size(620, 320);
            MinimumSize = new Size(400, 220);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false;
            var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both, BackColor = SystemColors.Window, Text = message,
                SelectionStart = 0, SelectionLength = 0 };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true };
            var copy = new Button { Text = "Copy", AutoSize = true };
            var status = new Label { AutoSize = true, Padding = new Padding(3, 6, 3, 0) };
            copy.Click += (sender, args) =>
            {
                try { Clipboard.SetText(details ?? message); status.Text = "Copied"; }
                catch (ExternalException) { status.Text = "Clipboard busy. Try Copy again."; }
            };
            text.KeyDown += (sender, args) =>
            {
                if (args.KeyData == (Keys.Control | Keys.A))
                { text.SelectAll(); args.SuppressKeyPress = true; }
            };
            buttons.Controls.Add(close); buttons.Controls.Add(copy); buttons.Controls.Add(status);
            Controls.Add(text); Controls.Add(buttons);
            AcceptButton = close; CancelButton = close;
        }

        public static void ShowMessage(IWin32Window owner, string title, string message, string details = null)
        {
            using (var dialog = new CopyableMessageDialog(title, message, details)) dialog.ShowDialog(owner);
        }
    }
}
