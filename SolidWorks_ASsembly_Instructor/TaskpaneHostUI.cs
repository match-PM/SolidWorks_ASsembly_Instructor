using SolidWorks_ASsembly_Instructor.Properties;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
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

        List<PartDescription> partList = null;
        List<AssemblyDescription> assamblyList = null;

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
            partList = new List<PartDescription>();
            assamblyList = new List<AssemblyDescription>();
            savepath = Settings.Default.savePath;
            tb_BrowseFolder.Text = savepath;

        }

        AssemblyDescription mainAssembly;   // Representation of the Assembly given in SolidWorks

        /// <summary>
        /// Is called when the "Export" button is pressed.
        /// <c>MainWork</c> saves the Assembly with all Subassemblies in the global MainAssembly.
        /// </summary>
        private void MainWork()
        {
            mainAssembly = new AssemblyDescription();

            try
            {
                // Check if AssemblyDocument has been loaded.
                activeDoc = app.ActiveDoc as ModelDoc2;     // Top-level Assembly
                if (activeDoc == null)
                {
                    app.SendMsgToUser("No active document found.");
                }
                mainAssamblyName = activeDoc.GetTitle().Split('.')[0];    // Name of folder
                int componentType = activeDoc.GetType();

                if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    ChildrenDescription newChild = ExtractAssambley(activeDoc);
                    newChild.assemblyTransform = new CoordinateSystemDescription();
                    mainAssembly = newChild.ToMainAssambly();
                    mainAssembly.Description = "test";
                    //mainAssembly.origin = GetOrigin(activeDoc);
                    //mainAssembly.children.Add(newChild);// ExtractAssambley(activeDoc));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                rtDebug.Text = ex.Message;
                rtDebug.Text += ex.StackTrace;
            }
        }



        #region Extraction Assembly data and creating objects for Json
        CoordinateSystemDescription currentSystem = new CoordinateSystemDescription();
        /// <summary>
        /// Extracts assembly data to create a hierarchical structure for JSON representation.
        /// </summary>
        /// <param name="assemblyModel">The assembly model to extract data from.</param>
        /// <returns>The ChildrenDescription representing the assembly structure.</returns>
        public ChildrenDescription ExtractAssambley(ModelDoc2 assemblyModel)
        {
            ChildrenDescription SubAssembly = new ChildrenDescription();
            SubAssembly.Name = assemblyModel.GetTitle().Split('.')[0];

            if (assemblyModel != null)
            {
                // Check if the current Assembly is actually an assembly
                if (assemblyModel is IAssemblyDoc assemblyDoc)
                {
                    // Get Sub components
                    object[] componentsObj = assemblyDoc.GetComponents(true);
                    Component2[] components = componentsObj.Cast<Component2>().ToArray();

                    // Check each subcomponent
                    foreach (Component2 component in components)
                    {
                        ModelDoc2 componentModel = component.GetModelDoc2();
                        if (componentModel != null)
                        {
                            int componentType = componentModel.GetType();   // Is it an assembly or a part?

                            GetOrigin(component);

                            if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                            {
                                // Go deeper to find more assemblies
                                ChildrenDescription subSubAssembly = ExtractAssambley(componentModel);

                                if (subSubAssembly != null)
                                {
                                    subSubAssembly.assemblyTransform = GetOrigin(component);
                                    subSubAssembly.originTransform.fromMatrix4x4(Matrix4x4.Multiply(currentSystem.AsMatrix4x4(), subSubAssembly.assemblyTransform.AsMatrix4x4()));
                                    SubAssembly.children.Add(subSubAssembly);
                                    SubAssembly.Type = ((int)swDocumentTypes_e.swDocASSEMBLY).ToString();
                                    currentSystem = subSubAssembly.assemblyTransform;
                                }
                                else
                                {
                                    return SubAssembly;
                                }
                            }
                            else if (componentType == (int)swDocumentTypes_e.swDocPART)
                            {
                                ChildrenDescription subSubPart = ExtractPartShort(componentModel, component);

                                if (subSubPart != null)
                                {
                                    // If it's a part, get the short information about it
                                    subSubPart.originTransform.fromMatrix4x4(Matrix4x4.Multiply(currentSystem.AsMatrix4x4(), subSubPart.assemblyTransform.AsMatrix4x4()));
                                    SubAssembly.Type = ((int)swDocumentTypes_e.swDocPART).ToString();
                                    SubAssembly.children.Add(subSubPart);
                                }
                                else
                                {
                                    return SubAssembly;
                                }
                            }
                        }
                    }
                    currentSystem = SubAssembly.assemblyTransform;
                    return SubAssembly; // Return the current AssemblyDescription
                }
            }
            return null;    // Return null when no more assemblies are available
        }

        /// <summary>
        /// Extracts short information about a part for JSON representation.
        /// </summary>
        /// <param name="partModel">The part model to extract data from.</param>
        /// <param name="component">The component associated with the part.</param>
        /// <returns>The ChildrenDescription representing the short information about the part.</returns>
        public ChildrenDescription ExtractPartShort(ModelDoc2 partModel, Component2 component)
        {
            ChildrenDescription subpart = new ChildrenDescription();
            subpart.Name = partModel.GetTitle().Split('.')[0];
            subpart.Type = ((int)swDocumentTypes_e.swDocPART).ToString();
            subpart.assemblyTransform = GetOrigin(component);
            return subpart;
        }

        /// <summary>
        /// Gets the origin of a component.
        /// </summary>
        /// <param name="comp">The component to get the origin from.</param>
        /// <returns>The CoordinateSystemDescription representing the origin.</returns>
        private CoordinateSystemDescription GetOrigin(Component2 comp)
        {
            CoordinateSystemDescription Cs = new CoordinateSystemDescription();
            MathTransform swXForm = comp.Transform2;

            // Transformation matrix in the format:
            // R1 R4 R7 0
            // R2 R5 R8 0
            // R3 R6 R9 0
            // T1 T2 T3 1
            Matrix4x4 TransformationMatrix = new Matrix4x4(
                       (float)swXForm.ArrayData[0], (float)swXForm.ArrayData[1], (float)swXForm.ArrayData[2], 0.0f,
                       (float)swXForm.ArrayData[3], (float)swXForm.ArrayData[4], (float)swXForm.ArrayData[5], 0.0f,
                       (float)swXForm.ArrayData[6], (float)swXForm.ArrayData[7], (float)swXForm.ArrayData[8], 0.0f,
                       (float)swXForm.ArrayData[9], (float)swXForm.ArrayData[10], (float)swXForm.ArrayData[11], 1.0f);

            Cs.Name = comp.Name;
            Cs.fromMatrix4x4(TransformationMatrix);

            return Cs;
        }

        #endregion


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
            // Hauptverarbeitung durchführen
            MainWork();

            // Überprüfen, ob der ausgewählte Ordner existiert
            if (!Directory.Exists(tb_BrowseFolder.Text))
            {
                app.SendMsgToUser("Folder does not exist!");
            }
            else
            {
                // Erstellen Sie Unterordner für Baugruppe und Teile, falls sie nicht existieren
                assamblyPath = Path.Combine(tb_BrowseFolder.Text, mainAssamblyName, "assembly");
                if (!Directory.Exists(assamblyPath))
                {
                    Directory.CreateDirectory(assamblyPath);
                }
                partsPath = Path.Combine(tb_BrowseFolder.Text, mainAssamblyName, "parts");
                if (!Directory.Exists(partsPath))
                {
                    Directory.CreateDirectory(partsPath);
                }
            }

            // Serialisieren Sie das Hauptmodell in JSON
            string json = JsonConvert.SerializeObject(mainAssembly, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // Schreiben Sie das JSON in eine Datei im Assembly-Unterordner
            using (StreamWriter file = File.CreateText(Path.Combine(assamblyPath, mainAssamblyName + ".json")))
            {
                file.WriteLine(json);
            }

            // Loggen Sie eine Erfolgsmeldung in der Debug-Ausgabe
            logDebug("Json erfolgreich geschrieben");
        }
        #endregion

    }


}
