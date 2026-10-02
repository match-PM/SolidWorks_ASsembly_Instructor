using SolidWorks.Interop.sldworks;
using SolidWorks_ASsembly_Instructor.Properties;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    [ProgId(TaskpaneIntegration.SWTASKPANE_PROGID)]
    public partial class TaskpaneHostUI : UserControl
    {
        public SldWorks app = null; // hanndle to Solid works

        #region Path variables
        string componentsPath;
        string assembliesPath;

        string savepath; // Save in settings
        #endregion

        /// <summary>
        /// Constructor of the UI-Element
        /// </summary>
        public TaskpaneHostUI()
        {
            InitializeComponent();
            savepath = Settings.Default.savePath;
            tb_BrowseFolder.Text = savepath;
            _CombineOutputPaths(savepath);
        }

        #region Debug Log
        /// <summary>
        /// Loggt Debug-Nachrichten in das RichTextBox-Steuerelement.
        /// </summary>
        /// <param name="message">Die zu loggende Nachricht.</param>
        public void LogDebug(string message)
        {
            AppendTextToRTB(message + "\r\n", Color.DarkGray);
        }
        public void Log(string message)
        {
           AppendTextToRTB(message + "\r\n", Color.Black);
        }
        public void WarningLog(string message)
        {
            AppendTextToRTB(message + "\r\n", Color.Orange);
        }
        public void ErrorLog(string message)
        {
            AppendTextToRTB(message + "\r\n", Color.Red);
        }

        public void AppendTextToRTB(string text, Color color, bool addNewLine = false)
        {
            rtDebug.SuspendLayout();
            rtDebug.SelectionColor = color;
            rtDebug.AppendText(addNewLine
                ? $"{text}{System.Environment.NewLine}"
                : text);
            rtDebug.ScrollToCaret();
            rtDebug.ResumeLayout();
        }
        #endregion

        #region UI Events
        /// <summary>
        /// Ereignisbehandlung für die Aktualisierung der Debug-Ausgabe.
        /// </summary>
        private void btn_clearLog_Click(object sender, EventArgs e)
        {
            rtDebug.Clear();
        }

        /// <summary>
        /// Ereignisbehandlung für das Durchsuchen von Ordnern.
        /// </summary>
        private void tb_BrowseFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.SelectedPath = tb_BrowseFolder.Text;
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                tb_BrowseFolder.Text = folderBrowserDialog1.SelectedPath;
                Settings.Default.savePath = folderBrowserDialog1.SelectedPath;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Ereignisbehandlung für den Export von JSON-Daten.
        /// </summary>
        private void exportJson_Click(object sender, EventArgs e)
        {
            rtDebug.Clear();
            exportJson.Enabled = false;
            try
            {
                if (!CreateFolder()) return;
                var coordinator = new ExportCoordinator(app, componentsPath, assembliesPath, Report);
                var result = coordinator.Run();
                foreach (var item in result.Items)
                    if (item.Status != ExportStatus.Succeeded)
                        Report($"{item.Name}: {item.Message}", item.Status == ExportStatus.Failed ? "error" : "warning");
                Report(result.ToString(), result.IsCompleteSuccess ? "log" : "warning");
            }
            catch (Exception ex) { ErrorLog(ex.Message); }
            finally { exportJson.Enabled = true; }
        }

        private void Report(string message, string level)
        {
            switch (level.ToLowerInvariant())
            {
                case "error": ErrorLog(message); break;
                case "warning": WarningLog(message); break;
                case "debug": LogDebug(message); break;
                default: Log(message); break;
            }
        }

        private void tb_BrowseFolder_TextChanged(object sender, EventArgs e)
        {
            savepath = tb_BrowseFolder.Text;
            _CombineOutputPaths(savepath);
            Settings.Default.savePath = savepath;
            Settings.Default.Save();
        }

        #endregion

        #region Utilities
        public bool CreateFolder()
        {
            // Überprüfen, ob der ausgewählte Ordner existiert
            if (!Directory.Exists(tb_BrowseFolder.Text))
            {
                DialogResult result = MessageBox.Show("Der Ausgabeordner existiert nicht. Möchten Sie ihn erstellen?", "Ordner erstellen", MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    // Erstellen Sie den Ausgabeordner
                    Directory.CreateDirectory(tb_BrowseFolder.Text);
                }
                else
                {
                    // Der Benutzer hat "Nein" ausgewählt, brechen Sie ab
                    app.SendMsgToUser("Vorgang abgebrochen.");
                    return false;
                }
            }
            return true;
        }

        private void _CombineOutputPaths(string savepath)
        {
            componentsPath = Path.Combine(savepath, "components");
            assembliesPath = Path.Combine(savepath, "assemblies");
        }

        private void TaskpaneHostUI_Load(object sender, EventArgs e)
        {
            Version ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            lbl_Version_No.Text = "V" + ver.Major + "." + ver.Minor + "." + ver.Build;
        }
    }

    #endregion

}
