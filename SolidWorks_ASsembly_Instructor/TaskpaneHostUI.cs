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
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Xml.Linq;
using System.Reflection;
using static System.Windows.Forms.AxHost;

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
        string swasi_identificator = "SWASI_";
        string swasi_origin_identificator = "SWASI_Origin_";
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
            partList = new List<PartDescription>();
            assamblyList = new List<AssemblyDescription>();
            savepath = Settings.Default.savePath;
            tb_BrowseFolder.Text = savepath;
            componentsPath = Path.Combine(tb_BrowseFolder.Text, "components");
            assembliesPath = Path.Combine(tb_BrowseFolder.Text, "assemblies");
            
        }

        AssemblyDescription mainAssembly;   // Representation of the Assembly given in SolidWorks
        ComponentDescription Component;
        object ExportObject;
        /// <summary>
        /// Is called when the "Export" button is pressed.
        /// <c>MainWork</c> saves the Assembly with all Subassemblies in the global MainAssembly.
        /// </summary>
        private void MainWork()
        {
            if (!createFolder())
            {
                logDebug("Error: Can not extract json because the root folder is not set correctly!");
                return;
            }
            mainAssembly = new AssemblyDescription();
            MountingDescription mountingDescription = new MountingDescription();
            Component = new ComponentDescription();
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

                if (componentType == (int)swDocumentTypes_e.swDocPART || componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    FeatureManager swFeatureManager = activeDoc.FeatureManager;

                    // Get all features in the feature manager
                    object[] features = (object[])swFeatureManager.GetFeatures(false);

                    var Output =  ExtractOrigin(features);
                    // If extraction of Swasi origin is successfull
                    if (Output.Item1)
                    {
                        CoordinateSystemDescription Origin = Output.Item2;
                        mountingDescription.mountingReferences.spawningOrigin = Output.Item2.name;
                        mountingDescription.mountingReferences.ref_planes.AddRange(ExtractRefPlanes(features, Output.Item2.GetInverted4x4Matrix()));
                        mountingDescription.mountingReferences.ref_frames.AddRange(ExtractRefPoints(features, Output.Item2.GetInverted4x4Matrix()));
                        mountingDescription.mountingReferences.ref_frames.AddRange(ExtractRefFrames(features, Output.Item2.GetInverted4x4Matrix()));
                        mountingDescription.mountingReferences.ref_axes.AddRange(ExtractRefAxes(features));

                        // If an assembly, extract assembly mates
                        if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                        {
                            AssemblyDoc swAssembly = (AssemblyDoc)activeDoc;
                            object[] Components = (Object[])swAssembly.GetComponents(true);
                            Component2 swComponent;
                            
                            foreach (Object SingleComponent in Components)
                            {
                                swComponent = (Component2)SingleComponent;
                            }
                            //AssemblyDescription assemblyDescription = new AssemblyDescription();
                            mountingDescription = ExtractAssemblyComponents(mountingDescription, activeDoc);
                            mountingDescription = ExtractAssemblyMates(mountingDescription, features);
                            mainAssembly.mountingDescription = mountingDescription;
                            mainAssembly.name = mainAssamblyName;
                            mainAssembly.cadPath = mainAssamblyName + ".STL";
                            ExportObject = mainAssembly;
                            //List<AssemblyConstraintDescription> assemblyConstraints = ExtractAssemblyMates(features);
                        }
                        // else it must be a component
                        else
                        {
                            Component.mountingDescription = mountingDescription;
                            Component.name = mainAssamblyName + ".STL";
                            ExportObject = Component;
                        }
                    }
                }

                //if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                //{
                //    ChildrenDescription newChild = ExtractAssambley(activeDoc);
                //    newChild.assemblyTransform = new CoordinateSystemDescription();
                //    mainAssembly = newChild.ToMainAssambly();
                //    mainAssembly.Description = "test";
                //    //mainAssembly.origin = GetOrigin(activeDoc);
                //    //mainAssembly.children.Add(newChild);// ExtractAssambley(activeDoc));
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                rtDebug.Text = ex.Message;
                rtDebug.Text += ex.StackTrace;
            }

        }
        public bool SaveToSTL(SldWorks swApp, ModelDoc2 modelDoc, string filePath, string fileName, string exportCSName, int lengthUnit)
        {
            if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                // Set Pref to export all stl into a single file
                swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
            }

            //IMateReference matref = (IMateReference)feature.GetSpecificFeature2();
            //logDebug($"{matref.Name}");

            int errors = 0;
            int warnings = 0;
            // Change STL Binary Format
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            // Change STL TranslateToPositive
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, true);
            // Change STL Export Units
            swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, lengthUnit);
            // Change STL Export Quality
            //swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Fine);
            string filepath = $"{filePath}\\{fileName}.stl";
            // Change Export CS
            bool change_frame_success = modelDoc.SetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem, exportCSName);

            // Save File
            bool save_success = modelDoc.Extension.SaveAs(filepath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

            if (save_success)
            {
                logDebug($"STL exported to: {filepath}");
            }
            else
            {
                logDebug($"Error saving file to: {filepath}");
            }

            return save_success;
        }

        public Vector3 GetReferencePlaneNormal(IRefPlane referencePlane, Matrix4x4 RelTransform)
        {
            // Get the transformation matrix of the reference plane
            MathTransform transform = referencePlane.Transform;
            // Extract the normal vector from the transformation matrix
            Vector3 normalVector = new Vector3();
            normalVector.X = (float)transform.ArrayData[6];  // X component
            normalVector.Y = (float)transform.ArrayData[7]; // Y component
            normalVector.Z = (float)transform.ArrayData[8]; // Z component
            Vector4 normalVector4 = new Vector4(normalVector, 1f);
            Vector4 ResultVector = new Vector4();
            ResultVector = Vector4.Transform(normalVector4, RelTransform);
            
            normalVector.X = ResultVector.X;
            normalVector.Y = ResultVector.Y;
            normalVector.Z = ResultVector.Z;
            return normalVector;
        }

        public (bool, CoordinateSystemDescription) ExtractOrigin (object[] features)
        {
            CoordinateSystemDescription SwasiOrigin = new CoordinateSystemDescription();
            bool ExtractSuccess = false;
            foreach (object featureobj in features)
            {
                // check if the feature is a reference plane
                if (featureobj is Feature)
                {
                    Feature _feature = (Feature)featureobj;
                    if (_feature.GetTypeName2() == "CoordSys" && _feature.Name.Contains(swasi_origin_identificator))
                    {
                        if (!ExtractSuccess)
                        {
                            ICoordinateSystemFeatureData CoordSys = (ICoordinateSystemFeatureData)_feature.GetDefinition();
                            CoordSys.AccessSelections(activeDoc, null);
                            SwasiOrigin.fromArrayData(CoordSys.Transform.ArrayData);
                            SwasiOrigin.name = _feature.Name.Replace(swasi_origin_identificator, "");
                            CoordSys.ReleaseSelectionAccess();
                            ExtractSuccess = true;
                            string exportPath;
                            if ((int)activeDoc.GetType() == (int)swDocumentTypes_e.swDocPART)
                            {
                                exportPath = componentsPath;
                            }
                            else
                            {
                                exportPath = assembliesPath;
                            }
                            
                            //bool save_success = SaveToSTL(app, activeDoc, assamblyPath, mainAssamblyName, _feature.Name, (int)swLengthUnit_e.swMM);
                            bool save_success = SaveToSTL(app, activeDoc, exportPath, mainAssamblyName, _feature.Name, (int)swLengthUnit_e.swMETER);
                        }
                        else
                        {
                            // The for loop should only find one swasi origin. If it finds two it should return false
                            logDebug($"Error finding SWASI Origin. There have been defined two origins with the naming convention.");
                            return (false, SwasiOrigin);
                        }
                    }
                }
            }
            logDebug($"Found SWASI Origin: {SwasiOrigin.name}");
            return (ExtractSuccess, SwasiOrigin);
        }

        public List<RefFrameDescription> ExtractRefFrames(object[] _features,Matrix4x4 RelTransform)
        {
            List<RefFrameDescription> RefFrames = new List<RefFrameDescription>();
            foreach (object featureobj in _features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefFrame(_featureobj, RelTransform);
                    if (output.Item1)
                    {
                        RefFrames.Add(output.Item2);
                    }
                }
            }
            return RefFrames;
        }

        public (bool, RefFrameDescription) ExtractRefFrame(Feature feature, Matrix4x4 RelTransform) 
        { 
            RefFrameDescription refFrame = new RefFrameDescription();
            refFrame.type = "frame";
            if (feature.GetTypeName2() != "CoordSys")
            {
                return (false, refFrame);
            }
            // check if Frame is Tagged with the swasi_identificator
            if (!feature.Name.Contains(swasi_identificator))
            {
                return (false, refFrame);
            }
            if (feature.Name.Contains(swasi_origin_identificator))
            {
                return (false, refFrame);
            }
            
            ICoordinateSystemFeatureData CoordSys = (ICoordinateSystemFeatureData) feature.GetDefinition();
            CoordSys.AccessSelections(activeDoc, null);
            // Set the transformation from Array Data in Solidworks Coordinate System
            refFrame.transformation.fromArrayData(CoordSys.Transform.ArrayData);
            // Set the transformation after transforming it to the SWASI origin
            refFrame.transformation.fromMatrix4x4(Matrix4x4.Multiply(RelTransform, refFrame.transformation.AsMatrix4x4()));
            CoordSys.ReleaseSelectionAccess();
            refFrame.name = feature.Name.Replace(swasi_identificator, "");
            logDebug($"Extracted data for: {refFrame.name}");

            return (true, refFrame);
        }

        public List<RefFrameDescription> ExtractRefPoints(object[] _features, Matrix4x4 RelTransform)
        {
            List<RefFrameDescription> RefPoints = new List<RefFrameDescription>();
            foreach (object featureobj in _features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefPoint(_featureobj, RelTransform);
                    if (output.Item1)
                    {
                        RefPoints.Add(output.Item2);
                    }
                }
            }
            return RefPoints;
        }

        public (bool, RefFrameDescription) ExtractRefPoint(Feature point_feature, Matrix4x4 RelTransform)
        {
            RefFrameDescription refPointDescription = new RefFrameDescription();
            refPointDescription.type = "point";
            bool extract_success = true;
            if (point_feature.GetTypeName2() != "RefPoint")
            {
                return (false, refPointDescription);
            }
            // check if RefPoint is Tagged with the swasi_identificator
            if (!point_feature.Name.Contains(swasi_identificator))
            {
                return (false, refPointDescription);
            }
            IFeature specificFeature = point_feature.GetSpecificFeature2();
            IRefPoint refPoint = (IRefPoint)specificFeature;
            MathPoint mathRefPoint = refPoint.GetRefPoint();
            refPointDescription.transformation.translation.X = (float)mathRefPoint.ArrayData[0] * 1000;
            refPointDescription.transformation.translation.Y = (float)mathRefPoint.ArrayData[1] * 1000;
            refPointDescription.transformation.translation.Z = (float)mathRefPoint.ArrayData[2] * 1000;
            //logDebug($"Before {refPointDescription.transformation}"); ;
            // Set the transformation after transforming it to the SWASI origin
            refPointDescription.transformation.fromMatrix4x4(Matrix4x4.Multiply(RelTransform, refPointDescription.transformation.AsMatrix4x4()));
            //Reset the quaterionen
            refPointDescription.transformation.rotation = Quaternion.Identity;
            //logDebug($"After {refPointDescription.transformation}");
            refPointDescription.name = point_feature.Name.Replace(swasi_identificator, "");
            logDebug($"Extracted data for: {refPointDescription.name}");
            return (extract_success, refPointDescription);
        }

        public MountingDescription ExtractAssemblyComponents (MountingDescription MountingDesc, ModelDoc2 assemblyModel)
        {
            // Execute this only if the document is an assembly
            if (assemblyModel == null)
            {
                return MountingDesc;
            }

            // Check if the current Assembly is actually an assembly
            if (!(assemblyModel is IAssemblyDoc assemblyDoc))
            {
                return MountingDesc;
            }
            // Get Sub components
            object[] componentsObj = assemblyDoc.GetComponents(true);
            Component2[] components = componentsObj.Cast<Component2>().ToArray();

            // Check each subcomponent
            foreach (Component2 component in components)
            {
                AssemblyComponentDescription componentDescription = new AssemblyComponentDescription();
                
                ModelDoc2 componentModel = component.GetModelDoc2();
                if (componentModel == null)
                {
                    continue;
                }

                int componentType = componentModel.GetType();   // Is it an assembly or a part?
                if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    componentDescription.type = "assembly";
                }
                else
                {
                    componentDescription.type = "component";
                }
                    componentDescription.name = component.Name;
                //GetOrigin(component);
                // ################### Currently this is not the correct transfromation, we need the transformation from the Assembly SWASI Origin to the SWASI Origin of the individual components
                // ##############################################
                MathTransform swXForm = component.Transform2;
                componentDescription.transformation.fromArrayData(swXForm.ArrayData);
                MountingDesc.components.Insert(0, componentDescription);
            }                
            return MountingDesc;
        }
        public MountingDescription ExtractAssemblyMates(MountingDescription MountingDesc, object[] _Features)
        {
            foreach (object Feat in _Features)
            {
                if (!(Feat is Feature))
                {
                    continue;
                }

                Feature Feature = (Feature)Feat;
                if (!Feature.Name.Contains(swasi_identificator))
                {
                    continue;
                }

                // Currently only 'MateDistanceDim' and 'MateCoincident' are supported!
                if (Feature.GetTypeName2() == "MateDistanceDim" || Feature.GetTypeName2() == "MateCoincident")
                {
                    // Extract the name of the component that are associated with the mate
                    Mate2 FeatureMate = (Mate2)Feature.GetSpecificFeature2();
                    int entityCount = FeatureMate.GetMateEntityCount();
                    int index;
                    float planeOffset = 0;
                    int PlaneMatchIndex = -1;
                    bool flip_normal_vector = false;    // in Solidworks it is always the component behind the first component that is alignt to the "earlier" one.
                    bool SetSuccess = false;
                    string[] ComponentNames = new string[] { null, null};
                    // Entity count should normaly be two
                    for (index = 0; index < entityCount; index++)
                    {
                        IMateEntity2 MateEntity = (IMateEntity2)FeatureMate.MateEntity(index);
                        // Get component of the MateEntity
                        Component2 ComponentMateEntity = (Component2)MateEntity.ReferenceComponent;
                        //Set the component name. The following method deals with the correct setting of names
                        ComponentNames[index] = ComponentMateEntity.Name2.Replace(swasi_identificator, "");
                    }

                    if (ComponentNames[0] == null || ComponentNames[1] == null)
                    {
                        logDebug($"Error while extracting feature {Feature.Name}");
                        return MountingDesc;
                    }
                    
                    // Try to add the components to one of the three constraints, the function returns the index at which the constraints should be added, this is important if there are more than two parts in solidwors
                    int ConstraintIndex = MountingDesc.AddComponentsAssemblyConstraint(ComponentNames[0], ComponentNames[1]);
                    
                    // this method extracts if component 1 or component 2 should be moved, it does so by looking at the transformations in the components list and returns true if component 1 has a higher z-value, else false
                    // make shure the the MountingDescription holds the components before you call ExtractAssemblyMates
                    bool moveComponent_1 = IdentifyComponent1MovingPart(MountingDesc.components, ComponentNames[0], ComponentNames[1]);
                    MountingDesc.assemblyConstraints[ConstraintIndex].moveComponent_1 = moveComponent_1;

                    object _Feature = Feature.GetDefinition();
                    string[] PlaneNames = new string[] { null, null };
                    // If feature is a IDistanceMateFeatureData
                    if (_Feature is IDistanceMateFeatureData)
                    {

                        IDistanceMateFeatureData swMate = (IDistanceMateFeatureData)_Feature;
                        object[] FeatureMates = swMate.EntitiesToMate;
                        for (int i = 0; i < FeatureMates.Length; i++)
                        {
                            if (FeatureMates[i] != null && FeatureMates[i] is Feature)
                            {
                                Feature _FeatureMate = (Feature)FeatureMates[i];
                                PlaneNames[i] = _FeatureMate.Name;
                            }
                        }

                        // Set the plane names to the Description, returns the index at which the Plane match has been inserted (could only be 1,2,3, zero in case it could not be added)
                        var Output = MountingDesc.assemblyConstraints[ConstraintIndex].description.SetPlaneMatch(PlaneNames[0], PlaneNames[1]);
                        PlaneMatchIndex = Output.Item1;
                        SetSuccess = Output.Item2;
                        //logDebug($"{swMate.FlipDimension}");
                        // get sign of distance
                        int sign = 1;
                        if (swMate.FlipDimension)
                        {
                            sign = -1;
                        }
                        planeOffset = (float)swMate.Distance * 1000 * sign;
                        if (swMate.MateAlignment==1)
                        {
                            flip_normal_vector = true;
                        }
                        if (!SetSuccess)
                        {
                            logDebug($"Error while extracting feature {Feature.Name}");
                            return MountingDesc;
                        }
                    }
                    // If feature is a ICoincidentMateFeatureData
                    if (_Feature is ICoincidentMateFeatureData)
                    {
                        ICoincidentMateFeatureData swMate = (ICoincidentMateFeatureData)_Feature;
                        object[] FeatureMates = swMate.EntitiesToMate;
                        for (int i = 0; i < FeatureMates.Length; i++)
                        {
                            if (FeatureMates[i] != null && FeatureMates[i] is Feature)
                            {
                                Feature _FeatureMate = (Feature)FeatureMates[i];
                                PlaneNames[i] = _FeatureMate.Name;
                            }
                        }
                        //int PlaneMatchIndex = -1;
                        SetSuccess = false;
                        if (PlaneNames[0].Contains(swasi_identificator) && PlaneNames[0].Contains(swasi_identificator))
                        {
                            var Output = MountingDesc.assemblyConstraints[ConstraintIndex].description.SetPlaneMatch(PlaneNames[0].Replace(swasi_identificator, ""), PlaneNames[1].Replace(swasi_identificator, ""));
                            PlaneMatchIndex = Output.Item1;
                            SetSuccess = Output.Item2;
                        }

                        if (!SetSuccess)
                        {
                            logDebug($"Error while extracting feature {Feature.Name}");
                            return MountingDesc;
                        }
                        if (swMate.MateAlignment == 1)
                        {
                            flip_normal_vector = true;
                        }
                    }
                    // Set the plane offset and the normal vector inversion for the planes
                    if (PlaneMatchIndex == 1)
                    {
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_1.planeOffset = planeOffset;
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_1.invertNormalVector = flip_normal_vector;
                    }
                    if (PlaneMatchIndex == 2)
                    {
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_2.planeOffset = planeOffset;
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_2.invertNormalVector = flip_normal_vector;
                    }
                    if (PlaneMatchIndex == 3)
                    {
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_3.planeOffset = planeOffset;
                        MountingDesc.assemblyConstraints[ConstraintIndex].description.planeMatch_3.invertNormalVector = flip_normal_vector;
                    }



                }
            }

            bool ExtractSuccess = MountingDesc.CompleteAssemblyConstraintDescription();
            if ( !ExtractSuccess )
            {
                logDebug("Error extracting assembly constraints");
            }
            return MountingDesc;         
        }

        public bool IdentifyComponent1MovingPart(List<AssemblyComponentDescription> AssemblyComponentDescriptions, string NameComponent_1, string NameComponent_2)
        {
            float zValue1 = 0;
            float zValue2 = 0;
            for (int i = 0; i < AssemblyComponentDescriptions.Count; i++)
            {
                if (AssemblyComponentDescriptions[i].name == NameComponent_1)
                {
                    zValue1 = AssemblyComponentDescriptions[i].transformation.translation.Z;
                }
                if (AssemblyComponentDescriptions[i].name == NameComponent_2)
                {
                    zValue2 = AssemblyComponentDescriptions[i].transformation.translation.Z;
                }
            }
            if (zValue1>=zValue2)
            {
                return true;
            }
            else
            {
                return false;
            }
            
        }
        public List<RefPlaneDescription> ExtractRefPlanes(object[] _features, Matrix4x4 RelTransform)
        {
            List<RefPlaneDescription> RefPlaneDescriptions = new List<RefPlaneDescription>();
            foreach (object featureobj in _features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefPlane(_featureobj, RelTransform);
                    if (output.Item1)
                    {
                        RefPlaneDescriptions.Add(output.Item2);
                        logDebug($"Found Plane: {output.Item2.name}");
                    }
                }
            }
            return RefPlaneDescriptions;
        }

        public (bool, RefPlaneDescription) ExtractRefPlane(Feature plane_feature, Matrix4x4 RelTransform)
        {
            RefPlaneDescription refPlaneDescription = new RefPlaneDescription();
            bool extract_success = true;

            // Test if feature is actually a RefPlane
            if (plane_feature.GetTypeName2() != "RefPlane")
            {
                return (false, refPlaneDescription);
            }
            // check if RefFrame is Tagged with the swasi_identificator
            if (!plane_feature.Name.Contains(swasi_identificator))
            {
                return (false, refPlaneDescription);
            }

            refPlaneDescription.name = plane_feature.Name.Replace(swasi_identificator, "");

            // Get normal vector for plane
            IFeature specificFeature = plane_feature.GetSpecificFeature2();
            IRefPlane referencePlane = (IRefPlane)specificFeature;
            refPlaneDescription.normalVector = GetReferencePlaneNormal(referencePlane, RelTransform);

            //logDebug($"Reference Plane Normal Vector: {refPlaneDescription.NormalVector.X},{refPlaneDescription.NormalVector.Y}, {refPlaneDescription.NormalVector.Z}");

            // Get plane definition
            RefPlaneFeatureData swRefPlaneFeatureData = (RefPlaneFeatureData)plane_feature.GetDefinition();

            int selection_count = swRefPlaneFeatureData.GetSelectionsCount();
            int constraint_type;

            // Check if the plane is defined by valid constraints
            for (int i = 0; i<=selection_count-1; i++)
            {
                constraint_type = swRefPlaneFeatureData.Constraint[i];
                if (!(constraint_type != (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Coincident ||
                    constraint_type != (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Perpendicular))
                {
                    logDebug($"Plane is tagged with 'SWASI_' but the given reference constraints are currently not supported!");
                    return (false, refPlaneDescription);
                }
            }
            //Check Plane type
            int planeType2 = swRefPlaneFeatureData.Type2;
            if (planeType2 == 11)
            {
                // This should normaly be the case
            }
            else
            {
                logDebug("Unexpected! Not Constraint-based");
            }

            int planeType = swRefPlaneFeatureData.Type;
            // if plane is a Line Point definition - see documentation for other types of definitions
            if (planeType == 2)
            {
                //logDebug("Line Point");
            }
            // if plane is a three points definition - see documentation for other types of definitions
            if (planeType == 3)
            {
                //logDebug("Three Points");
            }

            bool reverse_direction = swRefPlaneFeatureData.ReversedReferenceDirection[0];

            // Access the features that define the RefPlane
            // If a part
            bool access_success = swRefPlaneFeatureData.AccessSelections(activeDoc, null);

            // if an assembly
            // AccessSelections need to be modified if an assembly
            // https://help.solidworks.com/2018/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IRefPlaneFeatureData~IAccessSelections.html

            if (!access_success)
            {
                logDebug($"Error! Could not access: {plane_feature.Name}!");
                return (false, refPlaneDescription);
            }
            else
            {
                object[] plane_sub_features = swRefPlaneFeatureData.Selections;
                int points_ind = 0;
                int axis_ind = 0;
                foreach (object plane_sub_feature in plane_sub_features)
                {
                    // Check if the feature is a reference plane
                    if (plane_sub_feature is Feature)
                    {
                        //Feature plane_feature 
                        Feature _plane_sub_feature = (Feature)plane_sub_feature;
                        // CHeck if the name is valid
                        if (!_plane_sub_feature.Name.Contains(swasi_identificator))
                        {
                            logDebug($"Plane '{plane_feature.Name}' is defined by '{_plane_sub_feature.Name}'. However, this does not adhere to the naming convention of SWASI!");
                            return (false, refPlaneDescription);
                        }
                        // Append if point
                        if (_plane_sub_feature.GetTypeName2() == "RefPoint")
                        {
                            refPlaneDescription.refPointNames[points_ind] = _plane_sub_feature.Name.Replace(swasi_identificator, "");
                            points_ind++;
                        }
                        //Append if axis
                        if (_plane_sub_feature.GetTypeName2() == "RefAxis")
                        {
                            refPlaneDescription.refAxisNames[axis_ind] = _plane_sub_feature.Name.Replace(swasi_identificator, "");
                            axis_ind++;
                        }
                    }
                }
                if (planeType == 3 && points_ind != 3)
                {
                    logDebug($"Error! Plane '{plane_feature.Name}' is defined by three points, but not all of the points adhere to the swasi naming convention!");
                    return (false, refPlaneDescription);
                }
                if (planeType == 2 && axis_ind != 1 && points_ind != 1)
                {
                    logDebug($"Error! Plane '{plane_feature.Name}' is defined by one point and an axis, but not eather the point or the axis do not adhere to the swasi naming convention!");
                    return (false, refPlaneDescription);
                }

            }
            // Release the access to the features from PlaneFeatureData! 
            swRefPlaneFeatureData.ReleaseSelectionAccess();

            return (extract_success, refPlaneDescription);
        }

        public List<RefAxisDescription> ExtractRefAxes(object[] _features)
        {
            List<RefAxisDescription> RefAxis = new List<RefAxisDescription>();
            foreach (object featureobj in _features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefAxis(_featureobj);
                    if (output.Item1)
                    {
                        RefAxis.Add(output.Item2);
                        logDebug($"Extracted axis: {output.Item2.name}");
                    }
                }
            }
            return RefAxis;
        }
        public (bool, RefAxisDescription) ExtractRefAxis(Feature feature)
        {
            RefAxisDescription refPlaneAxisDescription = new RefAxisDescription();

            // Test if feature is actually a RefPlane
            if (feature.GetTypeName2() != "RefAxis")
            {
                return (false, refPlaneAxisDescription);
            }
            // check if RefFrame is Tagged with the swasi_identificator
            if (!feature.Name.Contains(swasi_identificator))
            {
                return (false, refPlaneAxisDescription);
            }

            refPlaneAxisDescription.name = feature.Name.Replace(swasi_identificator, "");

            // Get axis definition
            RefAxisFeatureData swRefAxisFeatureData = (RefAxisFeatureData)feature.GetDefinition();

            // Access the features that define the RefPlane
            // If a part
            bool access_success = swRefAxisFeatureData.AccessSelections(activeDoc, null);

            // if an assembly
            // AccessSelections need to be modified if an assembly
            // https://help.solidworks.com/2018/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IRefPlaneFeatureData~IAccessSelections.html

            if (!access_success)
            {
                logDebug($"Error! Could not access: {feature.Name}!");
                return (false, refPlaneAxisDescription);
            }
            else
            {
                // There is this tutorial, but thats not want we want to extract
                // https://help.solidworks.com/2019/english/api/sldworksapi/get_selections_for_reference_axis_feature_example_csharp.htm

                object types = null;
                object[] obj = (object[])swRefAxisFeatureData.GetSelections(out types);
                // Release the access to the features from swRefAxisFeatureData!
                swRefAxisFeatureData.ReleaseSelectionAccess();


                int point_ind = 0;
                foreach (object feat in obj)
                {
                    // Check if the feature is a reference plane
                    if (feat is Feature)
                    {
                        //Feature  
                        Feature _feat = (Feature)feat;
                        if (point_ind == 2)
                        {
                            // This theoratically should never happen
                            logDebug($"Unexprected error happend in extracting axis {_feat.Name}");
                            return (false, refPlaneAxisDescription);
                        }

                        // Check if the name is valid
                        if (!_feat.Name.Contains(swasi_identificator))
                        {
                            logDebug($"Axis '{_feat.Name}' is defined by '{_feat.Name}'. However, this does not adhere to the naming convention of SWASI!");
                            return (false, refPlaneAxisDescription);
                        }
                        // Append if point
                        if (_feat.GetTypeName2() == "RefPoint")
                        {
                            refPlaneAxisDescription.refPointNames[point_ind] = _feat.Name.Replace(swasi_identificator, "");
                            //logDebug($"Defined by: {refPlaneAxisDescription.RefPointNames[point_ind]}");
                            point_ind++;
                        }
                    }
                }
                if (point_ind < 2)
                {
                    logDebug($"Axis could not be extracted. Not enouth Swasi RefPoints given to define axis.");
                    return (false, refPlaneAxisDescription);
                }
                return (true, refPlaneAxisDescription);
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
            SubAssembly.name = assemblyModel.GetTitle().Split('.')[0];

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
                                    SubAssembly.type = ((int)swDocumentTypes_e.swDocASSEMBLY).ToString();
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
                                    SubAssembly.type = ((int)swDocumentTypes_e.swDocPART).ToString();
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
            subpart.name = partModel.GetTitle().Split('.')[0];
            subpart.type = ((int)swDocumentTypes_e.swDocPART).ToString();
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

            Cs.name = comp.Name;
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
            // Clear log
            rtDebug.Clear();
            // Hauptverarbeitung durchführen
            MainWork();

            // Überprüfen, ob der ausgewählte Ordner existiert
            if (!Directory.Exists(tb_BrowseFolder.Text))
            {
                app.SendMsgToUser("Folder does not exist!");
                return;
            }
            createFolder();

            if ((int)activeDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                fileExportPath = assembliesPath;
            }
            else
            {
                fileExportPath = componentsPath;
            }
            // Serialisieren Sie das Hauptmodell in JSON
            //string json = JsonConvert.SerializeObject(mainAssembly, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string json = JsonConvert.SerializeObject(ExportObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            // Schreiben Sie das JSON in eine Datei im Assembly-Unterordner
            using (StreamWriter file = File.CreateText(Path.Combine(fileExportPath, mainAssamblyName + ".json")))
            {
                file.WriteLine(json);
            }

            // Loggen Sie eine Erfolgsmeldung in der Debug-Ausgabe
            logDebug("Json erfolgreich geschrieben");
        }
        #endregion
        public bool createFolder()
        {
            // Überprüfen, ob der ausgewählte Ordner existiert
            if (!Directory.Exists(tb_BrowseFolder.Text))
            {
                app.SendMsgToUser("Error:Folder does not exist!");
                return false;
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
    }


}
