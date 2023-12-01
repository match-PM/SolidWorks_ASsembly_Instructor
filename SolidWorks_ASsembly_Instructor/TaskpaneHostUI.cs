using SolidWorks_ASsembly_Instructor;
using SolidWorks_ASsembly_Instructor.Properties;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Media;
using System.Drawing.Drawing2D;
using System.Xml.Linq;

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

        AssemblyDescription mainAssembly;   // Representation of the Assambly given in SolidWorks

        /// <summary>
        /// Is called, when "Export"-Button is pressed.
        /// <c>MainWork</c> saves the Assambly with all Subassemblies in the global MainAssembly
        /// </summary>
        private void MainWork()
        {
            mainAssembly = new AssemblyDescription();

            try
            {
                //Check if AssamblyDocument has been loded.
                activeDoc = app.ActiveDoc as ModelDoc2;     //Oberassambley
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
        // Extract assamblyModel to assemblys and parts
        public ChildrenDescription ExtractAssambley(ModelDoc2 assemblyModel)
        {
            ChildrenDescription SubAssembly = new ChildrenDescription();
            SubAssembly.Name = assemblyModel.GetTitle();

            if (assemblyModel != null)
            {
                // Check, if current Assembly is actualy an assambly
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
                            int componentType = componentModel.GetType();   //is it an assembly or a part?

                            CoordinateSystemDescription currentSystem = GetOrigin(component);
                            
                            if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                            {

                                // Go deeper to find more assemblys
                                ChildrenDescription subSubAssembly = ExtractAssambley(componentModel);
                                if (subSubAssembly != null)
                                {
                                    subSubAssembly.assemblyTransform = GetOrigin(component);
                                    subSubAssembly.originTransform.fromMatrix4x4(Matrix4x4.Multiply(currentSystem.AsMatrix4x4(), subSubAssembly.assemblyTransform.AsMatrix4x4()));
                                    SubAssembly.children.Add(subSubAssembly);
                                    SubAssembly.Type = ((int)swDocumentTypes_e.swDocASSEMBLY).ToString();
                                    
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
                                    // if its a part, get the Short Information about it
                                    subSubPart.originTransform.fromMatrix4x4(Matrix4x4.Multiply(currentSystem.AsMatrix4x4(), subSubPart.assemblyTransform.AsMatrix4x4()));
                                    SubAssembly.Type = ((int)swDocumentTypes_e.swDocPART).ToString();
                                    SubAssembly.children.Add(subSubPart);
                                }
                                else { return SubAssembly; }

                            }
                        }
                    }
                    return SubAssembly; // Return the current AssemblyDescrition
                }

            }

            return null;    //return null, when no more assemblys are available
        }

        public ChildrenDescription ExtractPartShort(ModelDoc2 partModel, Component2 component)
        {
            ChildrenDescription subpart = new ChildrenDescription();
            subpart.Name = partModel.GetTitle();
            subpart.Type = ((int)swDocumentTypes_e.swDocPART).ToString();
            subpart.assemblyTransform = GetOrigin(component);
            return subpart;
        }

        private CoordinateSystemDescription GetOrigin(Component2 comp)
        {
            CoordinateSystemDescription Cs = new CoordinateSystemDescription();
            MathTransform swXForm = comp.Transform2;
            /*  R1 R4 R7 0
                R2 R5 R8 0
                R3 R6 R9 0
                T1 T2 T3 1 */
            Matrix4x4 TransformationMatrix = new Matrix4x4(
                       (float)swXForm.ArrayData[0], (float)swXForm.ArrayData[1], (float)swXForm.ArrayData[2], 0.0f,
                       (float)swXForm.ArrayData[3], (float)swXForm.ArrayData[4], (float)swXForm.ArrayData[5], 0.0f,
                       (float)swXForm.ArrayData[6], (float)swXForm.ArrayData[7], (float)swXForm.ArrayData[8], 0.0f,
                       (float)swXForm.ArrayData[9], (float)swXForm.ArrayData[10], (float)swXForm.ArrayData[11], 1.0f);

            //logDebug(TransformationMatrix.ToString());

            Cs.Name = comp.Name;
            Cs.fromMatrix4x4(TransformationMatrix);

            return Cs;
        }

        #endregion

        #region Debug Log
        public void logDebug(string message)
        {
            rtDebug.AppendText(message + "\r\n");
        }
        #endregion

        #region UI Events
        private void refreshPartList_Click(object sender, EventArgs e)
        {
            rtDebug.Clear();
        }

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

        private void exportJson_Click(object sender, EventArgs e)
        {
            MainWork();

            if (!Directory.Exists(tb_BrowseFolder.Text))
            {
                app.SendMsgToUser("Folder does not exist!");
            }
            else
            {
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
            string json = JsonConvert.SerializeObject(mainAssembly, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            using (StreamWriter file = File.CreateText(Path.Combine(assamblyPath, mainAssamblyName + ".json")))
            {
                file.WriteLine(json);
            }
            logDebug("Json erfolgreich geschrieben");
        }
        #endregion
    }


}
