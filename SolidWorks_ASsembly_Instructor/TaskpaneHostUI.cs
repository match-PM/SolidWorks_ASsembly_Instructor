using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks_ASsembly_Instructor.Properties;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{

    /// TODO:
    /// Resourcedaten anlegen Speicherpfad gleichbelibenlassen
    /// Relative Coordinaten jetzt alle zum eigenen Assembly. Noch zum gesamten Origen berechnen!
    /// 
    /// 
    /// Nice introduction: https://cadbooster.com/solidworks-api-basics-sldworks-modeldoc2/
    /// </summary>


    [ProgId(TaskpaneIntegration.SWTASKPANE_PROGID)]
    public partial class TaskpaneHostUI : UserControl
    {
        ModelDoc2 activeDoc;
        public SldWorks app = null;


        string componentsPath;
        string assembliesPath;
        string fileExportPath;
        #region // Variablen zum Spiechern später
        string savepath;
        string partsPath;
        string assamblyPath;
        string mainAssamblyName;
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
        Swasi_Extractor Swasi_Extractor = null;


        AssemblyDescription mainAssembly;   // Representation of the Assembly given in SolidWorks
        ComponentDescription Component;
        object ExportObject;

        #region Debug Log
        /// <summary>
        /// Loggt Debug-Nachrichten in das RichTextBox-Steuerelement.
        /// </summary>
        /// <param name="message">Die zu loggende Nachricht.</param>
        public void logDebug(string message)
        {
            rtDebug.AppendText(message + "\r\n");
        }
        #endregion

        #region UI Events
        /// <summary>
        /// Ereignisbehandlung für die Aktualisierung der Debug-Ausgabe.
        /// </summary>
        private void refreshPartList_Click(object sender, EventArgs e)
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
            // Clear log
            rtDebug.Clear();

            // Auf gültigen Speicherort prüfen und gegebenenfalls schon die Unterordner erstellen.
            if (!createFolder()) { logDebug("Error: Can not extract json because the root folder is not set correctly!"); return; }

            //erstelle Extractor
            Swasi_Extractor = new Swasi_Extractor(app, componentsPath, assembliesPath);
            Swasi_Extractor.LogMessageUpdated += logDebug;

            if (Swasi_Extractor.Run())
            {
                // Loggen Sie eine Erfolgsmeldung in der Debug-Ausgabe
                logDebug("Json erfolgreich geschrieben");
            }
            else
            {
                logDebug("Fehler beim erstellen des JSON strings");
            }
            Swasi_Extractor = null;
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
        public bool createFolder()
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
            // Create assemblies directory
            if (!Directory.Exists(assembliesPath))
            {
                Directory.CreateDirectory(assembliesPath);
            }
            // Create parts directory
            if (!Directory.Exists(componentsPath))
            {
                Directory.CreateDirectory(componentsPath);
            }
            return true;
        }

        private void _CombineOutputPaths(string savepath)
        {
            componentsPath = Path.Combine(savepath, "components");
            assembliesPath = Path.Combine(savepath, "assemblies");
        }

    }

    #endregion

}
