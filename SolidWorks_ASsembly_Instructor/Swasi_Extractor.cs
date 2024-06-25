using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using System.Xml.Xsl;

namespace SolidWorks_ASsembly_Instructor
{
    internal class Swasi_Extractor
    {
        private const string SWASI_IDENTIFIER = "SWASI_";
        private const string SWASI_ORIGIN_IDENTIFIER = "SWASI_Origin_";

        private SldWorks app = null;
        private ModelDoc2 activeDoc = null;
        private ModelDoc2 currentDoc = null;

        private string componentsPath;
        private string assembliesPath;
        string fileExportPath;
        private string mainAssamblyName;

        // Event zur Aktualisierung von Lognachrichten
        public event Action<string> DebugLogMessageUpdated;
        public event Action<string> LogMessageUpdated;
        public event Action<string> WarningLogMessageUpdated;
        public event Action<string> ErrorLogMessageUpdated;

        public Swasi_Extractor(SldWorks app, string componentsPath, string assembliesPath)
        {
            this.app = app;
            this.componentsPath = componentsPath;
            this.assembliesPath = assembliesPath;

            //app.SendMsgToUser("blubb");
        }

        /// <summary>
        /// Runs the export process for SolidWorks assemblies and components.
        /// </summary>
        /// <returns>Returns true if the export process is successful, otherwise false.</returns>
        public bool Run()
        {
            // Müsste das hier nicht eigentlich auch in die For-Each-Schleife?
            AssemblyDescription mainAssembly;   // Representation of the Assembly given in SolidWorks
            ComponentDescription Component;
            object ExportObject;

            mainAssembly = new AssemblyDescription();

            Component = new ComponentDescription();
            try
            {
                // Check if AssemblyDocument has been loaded.
                activeDoc = app.ActiveDoc as ModelDoc2;     // Top-level Assembly
                if (activeDoc == null)
                {
                    app.SendMsgToUser("No active document found.");
                }
                activeDoc = app.ActiveDoc as ModelDoc2;

                ModelDoc2[] modelDoc2s;

                // Check if the active document is an assembly
                if (activeDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    //app.SendMsgToUser("Active document is an assembly.");
                    IAssemblyDoc assemblyDoc = (IAssemblyDoc)activeDoc;

                    // Get Sub components
                    object[] componentsObj = assemblyDoc.GetComponents(true);
                    Component2[] components = componentsObj.Cast<Component2>().ToArray();

                    modelDoc2s = new ModelDoc2[components.Length + 1]; // Größe um 1 für activeDoc erhöhen
                    modelDoc2s[0] = activeDoc; // activeDoc am Anfang des Arrays setzen
                    int index = 1;
                    foreach (Component2 component in components)
                    {
                        ModelDoc2 componentModelDoc = component.GetModelDoc2();
                        if (componentModelDoc != null)
                        {
                            modelDoc2s[index] = componentModelDoc;
                            index++;
                        }
                    }
                }
                // if not an assemby
                else
                {
                    //app.SendMsgToUser("Active document is not an assembly.");
                    modelDoc2s = new ModelDoc2[1];
                    modelDoc2s[0] = activeDoc;
                }

                // Check each subcomponent
                Dictionary<string, string> guidMap = new Dictionary<string, string>();
                foreach (ModelDoc2 ModDoc in modelDoc2s)
                {
                    if (!guidMap.ContainsKey(ModDoc.GetTitle().Split('.')[0]))
                    {
                        guidMap.Add(ModDoc.GetTitle().Split('.')[0], Guid.NewGuid().ToString());
                    }
                }

                int iterator = 0;
                foreach (ModelDoc2 ModDoc in modelDoc2s)
                {
                    currentDoc = ModDoc;
                    mainAssamblyName = currentDoc.GetTitle().Split('.')[0];    // Name of folder
                    int componentType = currentDoc.GetType();

                    if (componentType == (int)swDocumentTypes_e.swDocPART || componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        FeatureManager swFeatureManager = currentDoc.FeatureManager;
                        MountingDescription mountingDescription = new MountingDescription();
                        // Get all features in the feature manager
                        object[] features = (object[])swFeatureManager.GetFeatures(false);

                        var Output = ExtractOrigin(features);
                        // If extraction of Swasi origin is successful
                        if (Output.Item1)
                        {
                            CoordinateSystemDescription Origin = Output.Item2;
                            mountingDescription.mountingReferences.spawningOrigin = Output.Item2.name;
                            mountingDescription.mountingReferences.ref_planes.AddRange(ExtractRefPlanes(features, Output.Item2.GetInverted4x4Matrix()));
                            mountingDescription.mountingReferences.ref_frames.AddRange(ExtractRefPoints(features, Output.Item2.GetInverted4x4Matrix()));
                            mountingDescription.mountingReferences.ref_frames.AddRange(ExtractRefFrames(features, Output.Item2.GetInverted4x4Matrix()));
                            mountingDescription.mountingReferences.ref_axes.AddRange(ExtractRefAxes(features));

                            //Save STL
                            string exportPath = ((int)currentDoc.GetType() == (int)swDocumentTypes_e.swDocPART) ? componentsPath : assembliesPath;
                            bool saveSuccess = SaveToSTL(app, currentDoc, exportPath, mainAssamblyName, Origin.name, (int)swLengthUnit_e.swMETER);
                            iterator = iterator + 1;
                            Log($"{iterator}");
                            if (!saveSuccess)
                            {
                                Log($"Error saving STL for SWASI Origin: {Origin.name}", "Error");
                            }

                            // If an assembly, extract assembly mates
                            if (componentType == (int)swDocumentTypes_e.swDocASSEMBLY)
                            {
                                fileExportPath = assembliesPath;
                                AssemblyDoc swAssembly = (AssemblyDoc)ModDoc;
                                object[] Components = (Object[])swAssembly.GetComponents(true);
                                Component2 swComponent;

                                foreach (Object SingleComponent in Components)
                                {
                                    swComponent = (Component2)SingleComponent;
                                }
                                //AssemblyDescription assemblyDescription = new AssemblyDescription();
                                mountingDescription = ExtractAssemblyComponents(mountingDescription, ModDoc, Origin.GetInverted4x4Matrix()); //ToDo: Was muss hier übergeben werden?
                                mountingDescription = ExtractAssemblyMates(mountingDescription, features);
                                mainAssembly.mountingDescription = mountingDescription;
                                mainAssembly.name = mainAssamblyName;
                                mainAssembly.cadPath = mainAssamblyName + ".STL";
                                mainAssembly.guid = Guid.Parse(guidMap[mainAssamblyName]);
                                ExportObject = mainAssembly;
                                //List<AssemblyConstraintDescription> assemblyConstraints = ExtractAssemblyMates(features);
                            }
                            // else it must be a component
                            else
                            {
                                fileExportPath = componentsPath;
                                Component.mountingDescription = mountingDescription;
                                Component.cadPath = mainAssamblyName + ".STL";
                                Component.name = mainAssamblyName;
                                Component.guid = Guid.Parse(guidMap[mainAssamblyName]);
                                ExportObject = Component;
                            }

                            // Serialize the main model to JSON
                            // string json = JsonConvert.SerializeObject(mainAssembly, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                            string json = null;
                            string jsonNew = JsonConvert.SerializeObject(ExportObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                            string pathToComponents = "mountingDescription.components";
                            JToken tmpJson = JObject.Parse(jsonNew);
                            JArray componentsArray = tmpJson.SelectToken(pathToComponents) as JArray;

                            foreach (JObject comp in componentsArray)
                            {
                                string currentName = comp.SelectToken("name").ToString();
                                // shorten the name. cutting off "-*"
                                int dashIndex = currentName.LastIndexOf('-');
                                if (dashIndex >= 0)
                                {
                                    currentName = currentName.Substring(0, dashIndex);
                                }
                                if (guidMap.ContainsKey(currentName))
                                {
                                    comp["guid"] = guidMap[currentName];
                                }
                            }


                            tmpJson.SelectToken(pathToComponents).Replace(componentsArray);
                            jsonNew = tmpJson.ToString();


                            // Create filePath
                            string filePath = Path.Combine(fileExportPath, mainAssamblyName + ".json");
                            string jsonExist;

                            //check, if a file with this name is already existing
                            // if yes, merge both objects
                            if (JsonPathExtractor.CheckFileExists(filePath, out jsonExist))
                            {
                                Debug.Write($"{mainAssamblyName}\r\n");

                                JObject obj1 = JObject.Parse(jsonNew);
                                JObject obj2 = JObject.Parse(jsonExist);

                                JToken output = obj1 as JToken;
                                JsonPathValidator.ValidateAndAddMissingProperties(obj2, ref output);
                                json = output.ToString();
                                Log($"{filePath} File already exists");
                                Log("---------------------------------------------------------");
                            }
                            //if not, create new one
                            else
                            {
                                json = jsonNew;
                            }
                            // Write the JSON to a file in the Assembly folder
                            using (StreamWriter file = File.CreateText(filePath))
                            {
                                file.WriteLine(json);
                                Log($"Json exported for {mainAssamblyName}");
                                Log("---------------------------------------------------------");
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message, "Error");
                Log(ex.StackTrace, "Error");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the normal vector of a reference plane in the transformed coordinate system.
        /// </summary>
        /// <param name="referencePlane">The reference plane for which to get the normal vector.</param>
        /// <param name="relTransform">The relative transformation matrix applied to the reference plane.</param>
        /// <returns>The normal vector of the reference plane in the transformed coordinate system.</returns>
        private Vector3 GetReferencePlaneNormal(IRefPlane referencePlane, Matrix4x4 relTransform)
        {
            // Get the transformation matrix of the reference plane
            MathTransform transform = referencePlane.Transform;

            // Extract the normal vector from the transformation matrix
            Vector3 normalVector = new Vector3();
            normalVector.X = (float)transform.ArrayData[6];  // X component
            normalVector.Y = (float)transform.ArrayData[7];  // Y component
            normalVector.Z = (float)transform.ArrayData[8];  // Z component

            // Create a 4D vector for transformation
            Vector4 normalVector4 = new Vector4(normalVector, 1f);

            // Transform the normal vector using the relative transformation matrix
            Vector4 resultVector = new Vector4();

            Matrix4x4 InvertedTransform;
            Matrix4x4.Invert(relTransform, out InvertedTransform);
            resultVector = Vector4.Transform(normalVector4, InvertedTransform);

            // Update the normal vector with the transformed values
            normalVector.X = resultVector.X;
            normalVector.Y = resultVector.Y;
            normalVector.Z = resultVector.Z;

            return normalVector;
        }

        /// <summary>
        /// Extracts the origin information from the specified features, which should include a reference coordinate system.
        /// </summary>
        /// <param name="features">Array of features to search for the origin.</param>
        /// <returns>
        /// A tuple containing a boolean indicating whether the extraction was successful and the extracted coordinate system description.
        /// </returns>
        public (bool, CoordinateSystemDescription) ExtractOrigin(object[] features)
        {
            Log($"Extracting {(currentDoc is AssemblyDoc ? "Assembly" : "Component")} {currentDoc.GetTitle()}");
            CoordinateSystemDescription swasiOrigin = new CoordinateSystemDescription();
            bool extractSuccess = false;

            foreach (object featureObj in features)
            {
                // Check if the feature is a reference coordinate system
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    if (feature.GetTypeName2() == "CoordSys" && feature.Name.Contains(SWASI_ORIGIN_IDENTIFIER))
                    {
                        if (!extractSuccess)
                        {
                            ICoordinateSystemFeatureData coordSys = (ICoordinateSystemFeatureData)feature.GetDefinition();
                            coordSys.AccessSelections(currentDoc, null);

                            // Extract the coordinate system information
                            swasiOrigin.fromArrayData(coordSys.Transform.ArrayData);
                            swasiOrigin.name = feature.Name.Replace(SWASI_ORIGIN_IDENTIFIER, "");
                            Log($"Found SWASI Origin: {swasiOrigin.name}");

                            coordSys.ReleaseSelectionAccess();
                            extractSuccess = true;

                            //Moved to somewhere else
                            //string exportPath = ((int)currentDoc.GetType() == (int)swDocumentTypes_e.swDocPART) ? componentsPath : assembliesPath;

                            //bool saveSuccess = SaveToSTL(app, currentDoc, exportPath, mainAssamblyName, feature.Name, (int)swLengthUnit_e.swMETER);

                            //if (!saveSuccess)
                            //{
                            //    Log($"Error saving STL for SWASI Origin: {feature.Name}", "Error");
                            //}
                        }
                        else
                        {
                            // The for loop should only find one SWASI origin. If it finds two, it should return false
                            Log($"Error finding SWASI Origin. There have been defined two origins with the naming convention.", "Error");
                            return (false, swasiOrigin);
                        }
                    }
                }
            }
            if (!extractSuccess)
            {
                Log("No SWASI Origin found", "Warning");
            }
            return (extractSuccess, swasiOrigin);
        }

        /// <summary>
        /// Extracts reference frames from the given array of features, applying the specified relative transformation.
        /// </summary>
        /// <param name="_features">Array of features to search for reference frames.</param>
        /// <param name="RelTransform">Relative transformation to apply to the extracted reference frames.</param>
        /// <returns>A list of extracted reference frame descriptions.</returns>
        public List<RefFrameDescription> ExtractRefFrames(object[] _features, Matrix4x4 RelTransform)
        {
            List<RefFrameDescription> refFrames = new List<RefFrameDescription>();

            foreach (object featureObj in _features)
            {
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    var output = ExtractRefFrame(feature, RelTransform);

                    if (output.Item1)
                    {
                        refFrames.Add(output.Item2);
                    }
                }
            }

            return refFrames;
        }


        /// <summary>
        /// Extracts a reference frame from the given feature, applying the specified relative transformation.
        /// </summary>
        /// <param name="feature">The feature to extract a reference frame from.</param>
        /// <param name="RelTransform">The relative transformation to apply to the extracted reference frame.</param>
        /// <returns>A tuple indicating extraction success and the extracted reference frame description.</returns>
        public (bool, RefFrameDescription) ExtractRefFrame(Feature feature, Matrix4x4 RelTransform)
        {
            RefFrameDescription refFrame = new RefFrameDescription();
            refFrame.type = "frame";

            // Check if the feature is a coordinate system
            if (feature.GetTypeName2() != "CoordSys")
            {
                return (false, refFrame);
            }

            // Check if the frame is tagged with the SWASI_IDENTIFIER or SWASI_ORIGIN_IDENTIFIER
            //if (!feature.Name.Contains(SWASI_IDENTIFIER) || !feature.Name.Contains(SWASI_ORIGIN_IDENTIFIER))
            //{
            //    // pops up on SW-Frames
            //    Log($"RefFrame ({feature.Name}) was ignored. Wrong Identifier?", "warning");
            //    return (false, refFrame);
            //}


            if (!feature.Name.Contains(SWASI_IDENTIFIER) || feature.Name.Contains(SWASI_ORIGIN_IDENTIFIER))
            {
                // Pops up on SW-Frames
                Log($"RefFrame ({feature.Name}) was ignored. Either Identifier is required or is Origin Identifier", "warning");
                return (false, refFrame);
            }


            ICoordinateSystemFeatureData coordSys = (ICoordinateSystemFeatureData)feature.GetDefinition();
            coordSys.AccessSelections(currentDoc, null);

            // Set the transformation from Array Data in Solidworks Coordinate System
            refFrame.transformation.fromArrayData(coordSys.Transform.ArrayData);

            // Set the transformation after transforming it to the SWASI origin
            refFrame.transformation.fromMatrix4x4(Matrix4x4.Multiply(RelTransform, refFrame.transformation.AsMatrix4x4()));

            coordSys.ReleaseSelectionAccess();
            refFrame.name = feature.Name.Replace(SWASI_IDENTIFIER, "");

            Log($"Extracted data for: {refFrame.name}");

            return (true, refFrame);
        }


        /// <summary>
        /// Extracts reference points from an array of SolidWorks features.
        /// </summary>
        /// <param name="_features">Array of SolidWorks features to extract reference points from.</param>
        /// <param name="RelTransform">Transformation matrix relative to the SWASI origin.</param>
        /// <returns>List of RefFrameDescriptions representing the extracted reference points.</returns>
        public List<RefFrameDescription> ExtractRefPoints(object[] _features, Matrix4x4 RelTransform)
        {
            List<RefFrameDescription> refPoints = new List<RefFrameDescription>();

            // Iterate through each feature in the array
            foreach (object featureObj in _features)
            {
                // Check if the feature is of type Feature
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    // Extract the reference point information
                    var output = ExtractRefPoint(feature, RelTransform);

                    // Check if the extraction was successful
                    if (output.Item1)
                    {
                        // Add the extracted reference point to the list
                        refPoints.Add(output.Item2);
                    }
                }
            }

            // Return the list of extracted reference points
            return refPoints;
        }

        /// <summary>
        /// Extracts reference point information from a SolidWorks RefPoint feature.
        /// </summary>
        /// <param name="point_feature">SolidWorks RefPoint feature to extract information from.</param>
        /// <param name="RelTransform">Transformation matrix relative to the SWASI origin.</param>
        /// <returns>
        /// A tuple indicating the success of the extraction and a RefFrameDescription representing
        /// the extracted reference point.
        /// </returns>
        public (bool, RefFrameDescription) ExtractRefPoint(Feature point_feature, Matrix4x4 RelTransform)
        {
            RefFrameDescription refPointDescription = new RefFrameDescription();
            refPointDescription.type = "point";
            bool extractSuccess = true;

            // Check if the feature is of type RefPoint
            if (point_feature.GetTypeName2() != "RefPoint")
            {
                return (false, refPointDescription);
            }

            // Check if RefPoint is tagged with the SWASI_IDENTIFIER
            if (!point_feature.Name.Contains(SWASI_IDENTIFIER))
            {
                Log($"RefPoint ({point_feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refPointDescription);
            }

            // Get the specific feature as RefPoint
            IFeature specificFeature = point_feature.GetSpecificFeature2();
            IRefPoint refPoint = (IRefPoint)specificFeature;
            MathPoint mathRefPoint = refPoint.GetRefPoint();

            // Set the translation values from the MathPoint (converted to millimeters)
            refPointDescription.transformation.translation.X = (float)mathRefPoint.ArrayData[0] * 1000;
            refPointDescription.transformation.translation.Y = (float)mathRefPoint.ArrayData[1] * 1000;
            refPointDescription.transformation.translation.Z = (float)mathRefPoint.ArrayData[2] * 1000;

            // Set the transformation after transforming it to the SWASI origin
            refPointDescription.transformation.fromMatrix4x4(Matrix4x4.Multiply(RelTransform, refPointDescription.transformation.AsMatrix4x4()));

            // Reset the quaternion
            refPointDescription.transformation.rotation = Quaternion.Identity;

            refPointDescription.name = point_feature.Name.Replace(SWASI_IDENTIFIER, "");
            Log($"Extracted data for: {refPointDescription.name}");

            return (extractSuccess, refPointDescription);
        }


        /// <summary>
        /// Extracts assembly components from a SolidWorks assembly model and populates the MountingDescription.
        /// </summary>
        /// <param name="MountingDesc">The MountingDescription to be populated with assembly components.</param>
        /// <param name="assemblyModel">The SolidWorks assembly model to extract components from.</param>
        /// <returns>The updated MountingDescription.</returns>
        public MountingDescription ExtractAssemblyComponents(MountingDescription MountingDesc, ModelDoc2 assemblyModel, Matrix4x4 RelTransform)
        {
            // Execute this only if the document is an assembly
            if (assemblyModel == null)
            {
                Log("Can't extract assembly components, because document is not an assembly", "warning");
                return MountingDesc;
            }

            // Check if the current Assembly is actually an assembly
            if (!(assemblyModel is IAssemblyDoc assemblyDoc))
            {
                Log("Current assembly is not an assembly", "warning");
                return MountingDesc;
            }

            Matrix4x4 SA_T_OA = RelTransform; // Transformation from the Origin of the Assembly to the SWASI Origin of the Assembly

            // For logging only
            //CoordinateSystemDescription SA_T_OA_D = new CoordinateSystemDescription();
            //SA_T_OA_D.fromMatrix4x4(SA_T_OA);
            //Log($"-----Transformation Assembly Origin - SWASI Assembly Origin---------");
            //Log($"{SA_T_OA_D}");

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

                // Set component type based on SolidWorks document type
                componentDescription.type = (componentType == (int)swDocumentTypes_e.swDocASSEMBLY) ? "assembly" : "component";
                componentDescription.name = component.Name;

                // ################### Currently, this is not the correct transformation. We need the transformation from the Assembly SWASI Origin to the SWASI Origin of the individual components
                // ##############################################

                //Ursprung(Baugruppe) -> SWASI_Origin (Baugruppe) - Ursprung(componente) -> SWASI_Origin_Gonio
                MathTransform swXForm = component.Transform2;
                CoordinateSystemDescription OA_T_OB = new CoordinateSystemDescription();

                OA_T_OB.fromArrayData(swXForm.ArrayData);
                Matrix4x4 OA_T_OB_matrix = OA_T_OB.AsMatrix4x4();

                //Log($"-----Transformation Assembly Origin - Bauteil Origin {component.Name}---------");
                //Log($"{OA_T_OB}"); // ist richtig oder invertieren???

                FeatureManager swFeatureManager = componentModel.FeatureManager;
                // Get all features in the feature manager
                object[] features = (object[])swFeatureManager.GetFeatures(false);

                // Get Origin
                var Output = ExtractOrigin(features);
                CoordinateSystemDescription OB_T_SB = new CoordinateSystemDescription();
                Matrix4x4 OB_T_SB_matrix = new Matrix4x4();
                // If extraction of Swasi origin is successful
                if (Output.Item1)
                {
                    OB_T_SB = Output.Item2;
                    OB_T_SB_matrix = OB_T_SB.AsMatrix4x4();
                    //log
                    //Log($"-----Transformation Bauteil Origin - Swasi Bauteil Origin {component.Name}---------");
                    //Log($"{Output.Item2}");
                }
                else
                {
                    // In this case we can't extract the origin, the transformation will just be emtpy
                }

                //Matrix4x4 OA_T_SB_matrix = Matrix4x4.Multiply(OA_T_OB_matrix, OB_T_SB_matrix);
                //Matrix4x4 SA_T_SB_matrix = Matrix4x4.Multiply(SA_T_OA, OA_T_SB_matrix);

                Matrix4x4 SA_T_OB_matrix = Matrix4x4.Multiply(SA_T_OA, OA_T_OB_matrix);
                Matrix4x4 SA_T_SB_matrix = Matrix4x4.Multiply(SA_T_OB_matrix, OB_T_SB_matrix);

                //CoordinateSystemDescription OB_T_SB = new CoordinateSystemDescription(Output.Item2.AsMatrix4x4());
                // Print the transformation matrix
                //Log($"Transformation Matrix from Origin of the Assembly to the Origin of the Component: {OA_T_OB_Matrix}");

                // Set the transformation after transforming it to the SWASI origin
                //componentDescription.transformation.fromMatrix4x4(Matrix4x4.Multiply(RelTransform, componentDescription.transformation.AsMatrix4x4()));
                componentDescription.transformation.fromMatrix4x4(SA_T_SB_matrix);

                //componentDescription.transformation.fromArrayData(swXForm.ArrayData);

                // Insert the component description at the beginning of the list
                MountingDesc.components.Insert(0, componentDescription);
            }

            return MountingDesc;
        }

        /// <summary>
        /// Extracts assembly mates from a SolidWorks assembly model and populates the MountingDescription.
        /// </summary>
        /// <param name="MountingDesc">The MountingDescription to be populated with assembly mates.</param>
        /// <param name="_Features">The array of features to extract mates from.</param>
        /// <returns>The updated MountingDescription.</returns>
        public MountingDescription ExtractAssemblyMates(MountingDescription MountingDesc, object[] _Features)
        {
            foreach (object Feat in _Features)
            {
                if (!(Feat is Feature))
                {
                    continue;
                }

                Feature Feature = (Feature)Feat;
                if (!Feature.Name.Contains(SWASI_IDENTIFIER))
                {
                    continue;
                }

                // Currently only 'MateDistanceDim' and 'MateCoincident' are supported!
                if (Feature.GetTypeName2() == "MateDistanceDim" || Feature.GetTypeName2() == "MateCoincident")
                {
                    // Extract the name of the components that are associated with the mate
                    Mate2 FeatureMate = (Mate2)Feature.GetSpecificFeature2();
                    int entityCount = FeatureMate.GetMateEntityCount();
                    int index;
                    float planeOffset = 0;
                    int PlaneMatchIndex = -1;
                    bool flip_normal_vector = false;    // in Solidworks, it is always the component behind the first component that is aligned to the "earlier" one.
                    bool SetSuccess = false;
                    string[] ComponentNames = new string[] { null, null };

                    // Entity count should normally be two
                    for (index = 0; index < entityCount; index++)
                    {
                        IMateEntity2 MateEntity = (IMateEntity2)FeatureMate.MateEntity(index);
                        // Get component of the MateEntity
                        Component2 ComponentMateEntity = (Component2)MateEntity.ReferenceComponent;
                        // Set the component name. The following method deals with the correct setting of names
                        ComponentNames[index] = ComponentMateEntity.Name2.Replace(SWASI_IDENTIFIER, "");
                    }

                    if (ComponentNames[0] == null || ComponentNames[1] == null)
                    {
                        Log($"Error while extracting feature {Feature.Name}", "Error");
                        return MountingDesc;
                    }

                    // Try to add the components to one of the three constraints, the function returns the index at which the constraints should be added, this is important if there are more than two parts in SolidWorks
                    int ConstraintIndex = MountingDesc.AddComponentsAssemblyConstraint(ComponentNames[0], ComponentNames[1]);

                    // This method extracts if component 1 or component 2 should be moved, it does so by looking at the transformations in the components list and returns true if component 1 has a higher z-value, else false
                    // Make sure that the MountingDescription holds the components before you call ExtractAssemblyMates
                    try
                    {
                        bool moveComponent_1 = IdentifyComponent1MovingPart(MountingDesc.components, ComponentNames[0], ComponentNames[1]);
                        MountingDesc.assemblyConstraints[ConstraintIndex].moveComponent_1 = moveComponent_1;
                    }
                    catch (Exception ex)
                    {
                        Log($"Can't determin moveComponent_1, because of {ex.Message}", "Error");
                    }

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

                        // Set the plane names to the Description, returns the index at which the Plane match has been inserted (could only be 1, 2, 3, zero in case it could not be added)
                        var Output = MountingDesc.assemblyConstraints[ConstraintIndex].description.SetPlaneMatch(PlaneNames[0].Replace(SWASI_IDENTIFIER, ""), PlaneNames[1].Replace(SWASI_IDENTIFIER, ""));
                        PlaneMatchIndex = Output.Item1;
                        SetSuccess = Output.Item2;

                        // Get sign of distance
                        int sign = swMate.FlipDimension ? -1 : 1;
                        planeOffset = (float)swMate.Distance * 1000 * sign;

                        if (swMate.MateAlignment == 1)
                        {
                            flip_normal_vector = true;
                        }

                        if (!SetSuccess)
                        {
                            Log($"Error while extracting feature {Feature.Name}", "Error");
                            return MountingDesc;
                        }
                    }

                    // If feature is an ICoincidentMateFeatureData
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

                        if (PlaneNames[0].Contains(SWASI_IDENTIFIER) && PlaneNames[0].Contains(SWASI_IDENTIFIER))
                        {
                            var Output = MountingDesc.assemblyConstraints[ConstraintIndex].description.SetPlaneMatch(PlaneNames[0].Replace(SWASI_IDENTIFIER, ""), PlaneNames[1].Replace(SWASI_IDENTIFIER, ""));
                            PlaneMatchIndex = Output.Item1;
                            SetSuccess = Output.Item2;
                        }

                        if (!SetSuccess)
                        {
                            Log($"Error while extracting feature {Feature.Name}", "Error");
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

            if (!ExtractSuccess)
            {
                Log("Error extracting assembly constraints", "Error");
            }

            return MountingDesc;
        }

        /// <summary>
        /// Identifies whether Component 1 or Component 2 should be considered as the moving part in an assembly.
        /// </summary>
        /// <param name="AssemblyComponentDescriptions">The list of AssemblyComponentDescriptions.</param>
        /// <param name="NameComponent_1">The name of Component 1.</param>
        /// <param name="NameComponent_2">The name of Component 2.</param>
        /// <returns>True if Component 1 has a higher Z-value, indicating it should be considered as the moving part; otherwise, false.</returns>
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

            return zValue1 >= zValue2;
        }

        /// <summary>
        /// Extracts reference planes from the given array of features.
        /// </summary>
        /// <param name="_features">The array of features to extract reference planes from.</param>
        /// <param name="RelTransform">The relative transformation matrix.</param>
        /// <returns>A list of RefPlaneDescriptions representing the extracted reference planes.</returns>
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
                        Log($"Extracted Plane: {output.Item2.name}");
                    }
                }
            }

            return RefPlaneDescriptions;
        }


        /// <summary>
        /// Extracts reference plane information from the given plane feature and relative transformation matrix.
        /// </summary>
        /// <param name="plane_feature">The feature representing the reference plane.</param>
        /// <param name="RelTransform">The relative transformation matrix.</param>
        /// <returns>
        /// A tuple containing a boolean indicating the success of the extraction and a RefPlaneDescription
        /// representing the extracted reference plane information.
        /// </returns>
        public (bool, RefPlaneDescription) ExtractRefPlane(Feature plane_feature, Matrix4x4 RelTransform)
        {
            RefPlaneDescription refPlaneDescription = new RefPlaneDescription();
            bool extract_success = true;

            // Test if feature is actually a RefPlane
            if (plane_feature.GetTypeName2() != "RefPlane")
            {
                return (false, refPlaneDescription);
            }

            // Check if RefPlane is Tagged with the SWASI_IDENTIFIER
            if (!plane_feature.Name.Contains(SWASI_IDENTIFIER))
            {
                Log($"RefPlane ({plane_feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refPlaneDescription);
            }

            refPlaneDescription.name = plane_feature.Name.Replace(SWASI_IDENTIFIER, "");

            // Get normal vector for plane
            IFeature specificFeature = plane_feature.GetSpecificFeature2();
            IRefPlane referencePlane = (IRefPlane)specificFeature;
            refPlaneDescription.normalVector = GetReferencePlaneNormal(referencePlane, RelTransform);

            // Check if the plane is defined by valid constraints
            RefPlaneFeatureData swRefPlaneFeatureData = (RefPlaneFeatureData)plane_feature.GetDefinition();
            int selection_count = swRefPlaneFeatureData.GetSelectionsCount();
            int constraint_type;

            for (int i = 0; i <= selection_count - 1; i++)
            {
                constraint_type = swRefPlaneFeatureData.Constraint[i];
                if (!(constraint_type != (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Coincident ||
                    constraint_type != (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Perpendicular))
                {
                    Log($"Plane is tagged with '{SWASI_IDENTIFIER}' but the given reference constraints are currently not supported!", "Error");
                    return (false, refPlaneDescription);
                }
            }

            //Check Plane type
            int planeType2 = swRefPlaneFeatureData.Type2;
            if (planeType2 == 11)
            {
                // This should normally be the case
            }
            else
            {
                Log("Unexpected! Not Constraint-based", "Error");
            }

            int planeType = swRefPlaneFeatureData.Type;

            // if plane is a Line Point definition - see documentation for other types of definitions
            if (planeType == 2)
            {
                //Log("Line Point", "debug");
            }

            // if plane is a three points definition - see documentation for other types of definitions
            if (planeType == 3)
            {
                //Log("Three Points", "debug");
            }

            bool reverse_direction = swRefPlaneFeatureData.ReversedReferenceDirection[0];

            // Access the features that define the RefPlane
            bool access_success = swRefPlaneFeatureData.AccessSelections(currentDoc, null);

            if (!access_success)
            {
                Log($"Error! Could not access: {plane_feature.Name}!", "Error");
                swRefPlaneFeatureData.ReleaseSelectionAccess();
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
                        Feature _plane_sub_feature = (Feature)plane_sub_feature;

                        // CHeck if the name is valid
                        if (!_plane_sub_feature.Name.Contains(SWASI_IDENTIFIER))
                        {
                            Log($"Plane ({plane_feature.Name}) is defined by ({_plane_sub_feature.Name}). Defining Axes and Points must have SWASI_Identifier ({SWASI_IDENTIFIER}), too!", "Warning");
                            return (false, refPlaneDescription);
                        }

                        // Append if point
                        if (_plane_sub_feature.GetTypeName2() == "RefPoint")
                        {
                            refPlaneDescription.refPointNames[points_ind] = _plane_sub_feature.Name.Replace(SWASI_IDENTIFIER, "");
                            points_ind++;
                        }

                        //Append if axis
                        if (_plane_sub_feature.GetTypeName2() == "RefAxis")
                        {
                            refPlaneDescription.refAxisNames[axis_ind] = _plane_sub_feature.Name.Replace(SWASI_IDENTIFIER, "");
                            axis_ind++;
                        }
                    }
                }

                if (planeType == 3 && points_ind != 3)
                {
                    Log($"Error! Plane '{plane_feature.Name}' is defined by three points, but not all of the points adhere to the swasi naming convention!", "Error");
                    return (false, refPlaneDescription);
                }

                if (planeType == 2 && axis_ind != 1 && points_ind != 1)
                {
                    Log($"Error! Plane '{plane_feature.Name}' is defined by one point and an axis, but neither the point nor the axis adhere to the swasi naming convention!", "Error");
                    return (false, refPlaneDescription);
                }
            }

            // Release the access to the features from PlaneFeatureData! 
            swRefPlaneFeatureData.ReleaseSelectionAccess();

            return (extract_success, refPlaneDescription);
        }


        /// <summary>
        /// Extracts information about reference axes from the given array of features.
        /// </summary>
        /// <param name="_features">The array of features to extract reference axes from.</param>
        /// <returns>A list of RefAxisDescription objects representing the extracted reference axes.</returns>
        public List<RefAxisDescription> ExtractRefAxes(object[] _features)
        {
            List<RefAxisDescription> RefAxes = new List<RefAxisDescription>();

            foreach (object featureobj in _features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefAxis(_featureobj);

                    if (output.Item1)
                    {
                        RefAxes.Add(output.Item2);
                        Log($"Extracted axis: {output.Item2.name}");
                    }
                }
            }

            return RefAxes;
        }

        /// <summary>
        /// Extracts information about a reference axis from the given feature.
        /// </summary>
        /// <param name="feature">The feature to extract the reference axis from.</param>
        /// <returns>A tuple containing a boolean indicating the success of the extraction and a RefAxisDescription object representing the extracted reference axis.</returns>
        public (bool, RefAxisDescription) ExtractRefAxis(Feature feature)
        {
            RefAxisDescription refAxisDescription = new RefAxisDescription();

            // Test if feature is actually a RefAxis
            if (feature.GetTypeName2() != "RefAxis")
            {
                return (false, refAxisDescription);
            }

            // Check if RefAxis is tagged with the SWASI_IDENTIFIER
            if (!feature.Name.Contains(SWASI_IDENTIFIER))
            {
                Log($"RefAxis ({feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refAxisDescription);
            }

            refAxisDescription.name = feature.Name.Replace(SWASI_IDENTIFIER, "");

            // Get axis definition
            RefAxisFeatureData swRefAxisFeatureData = (RefAxisFeatureData)feature.GetDefinition();

            // Access the features that define the RefAxis
            // If a part
            bool accessSuccess = swRefAxisFeatureData.AccessSelections(currentDoc, null);

            // if an assembly
            // AccessSelections need to be modified if an assembly
            // https://help.solidworks.com/2018/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IRefPlaneFeatureData~IAccessSelections.html

            if (!accessSuccess)
            {
                Log($"Error! Could not access: {feature.Name}!", "Error");
                swRefAxisFeatureData.ReleaseSelectionAccess();
                return (false, refAxisDescription);
            }
            else
            {
                // There is this tutorial, but that's not what we want to extract
                // https://help.solidworks.com/2019/english/api/sldworksapi/get_selections_for_reference_axis_feature_example_csharp.htm

                object types = null;
                object[] obj = (object[])swRefAxisFeatureData.GetSelections(out types);

                // Release the access to the features from swRefAxisFeatureData!
                swRefAxisFeatureData.ReleaseSelectionAccess();

                int pointInd = 0;
                foreach (object feat in obj)
                {
                    // Check if the feature is a reference point
                    if (feat is Feature)
                    {
                        Feature _feat = (Feature)feat;

                        if (pointInd == 2)
                        {
                            // This theoretically should never happen
                            Log($"Unexpected error happened in extracting axis {_feat.Name}", "Error");
                            return (false, refAxisDescription);
                        }

                        // Check if the name is valid
                        if (!_feat.Name.Contains(SWASI_IDENTIFIER))
                        {
                            Log($"Axis ({feature.Name}) is defined by '{_feat.Name}'. Defining Points must have SWASI_Identifier ({SWASI_IDENTIFIER}), too!", "warning");
                            return (false, refAxisDescription);
                        }

                        // Append if point
                        if (_feat.GetTypeName2() == "RefPoint")
                        {
                            refAxisDescription.refPointNames[pointInd] = _feat.Name.Replace(SWASI_IDENTIFIER, "");
                            //logDebug($"Defined by: {refAxisDescription.RefPointNames[pointInd]}");
                            pointInd++;
                        }
                    }
                }

                if (pointInd < 2)
                {
                    Log($"Axis ({feature.Name}) could not be extracted. Not enough Swasi RefPoints given to define axis.", "Error");
                    return (false, refAxisDescription);
                }

                return (true, refAxisDescription);
            }
        }


        /// <summary>
        /// Saves the model document to an STL file with the specified parameters.
        /// </summary>
        /// <param name="swApp">The SolidWorks application object.</param>
        /// <param name="modelDoc">The model document to be saved.</param>
        /// <param name="filePath">The directory path where the STL file will be saved.</param>
        /// <param name="fileName">The name of the STL file (without extension).</param>
        /// <param name="exportCSName">The name of the coordinate system to be used for export.</param>
        /// <param name="lengthUnit">The unit for length used for export.</param>
        /// <returns>True if the STL file is successfully saved; otherwise, false.</returns>
        private bool SaveToSTL(SldWorks swApp, ModelDoc2 modelDoc, string filePath, string fileName, string exportCSName, int lengthUnit)
        {

            //hier wird irgendwie das Assembly richtg gespeichert, beim Part scheint die EInstellung nicht zu wirken
            switch (modelDoc.GetType())
            {
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
                    break;
                case (int)swDocumentTypes_e.swDocPART:
                    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
                    break;
                default:
                    break;
                    // Set Pref to export all stl into a single file
            }

            int errors = 0;
            int warnings = 0;
            
            // Test
            SelectionMgr manager = modelDoc.SelectionManager;
            SelectData data = manager.CreateSelectData();
            data.Mark = 1; // or -1


            // Change STL Binary Format
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            // Change STL TranslateToPositive
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, true);
            // Change STL Export Units
            swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, lengthUnit);
            // Change STL Export Quality
            swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Fine);

            string filepath = $"{filePath}\\{fileName}.stl";
            Log(fileName);
            // Change Export CS
            bool changeFrameSuccess = modelDoc.SetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem, exportCSName);

            Log($"vor: {swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile)}");
            bool saveSuccess = false;
            // Save File
            //saveSuccess = modelDoc.Extension.SaveAs(filepath, 
            //    (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
            //    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
            //    null, 
            //    ref errors, 
            //    ref warnings);
            //saveSuccess = modelDoc.Extension.SaveAs2(
            //    filepath,
            //    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            //    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            //    null,
            //    "",
            //    false,
            //    ref errors,
            //    ref warnings);
            saveSuccess = modelDoc.Extension.SaveAs3(
                filepath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                null,
                ref errors,
                ref warnings);

            //RenameStlFiles(filePath, fileName, fileName);

            Log("Errors: " + errors);
            Log("Warnings: " + warnings);
            Log($"nach: {swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile)}");


            if (saveSuccess)
            {
                Log($"STL exported to: {filepath}");
            }
            else
            {
                Log($"Error saving STL file to: {filepath}", "Error");
            }

            return saveSuccess;
        }

        private void RenameStlFiles(string directoryPath, string searchString, string newString)
        {
            // Get all STL files in the directory
            string[] stlFiles = Directory.GetFiles(directoryPath, "*.stl", SearchOption.TopDirectoryOnly);

            // Iterate through the STL files
            foreach (string filePath in stlFiles)
            {
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                string fileExtension = Path.GetExtension(fileName);

                // Check if the file name contains the search string
                //if (fileName.Contains(searchString))
                if (fileNameWithoutExtension.Length >= searchString.Length + 2 &&
                    fileNameWithoutExtension.Substring(fileNameWithoutExtension.Length - searchString.Length - 2, searchString.Length) == searchString)
                {
                    // Generate the new file name
                    string newFileName = searchString + ".STL";

                    string newFilePath = Path.Combine(directoryPath, newFileName);

                    Log(newFilePath);
                    Log(filePath);

                    if (!File.Exists(newFilePath))
                    {
                        // Rename the file
                        File.Move(filePath, newFilePath);
                    }

                    if (filePath == newFilePath)
                    {
                        Log("File was not renamed!!!!!");
                    }

                    else
                    {
                        // Delete the existing file
                        File.Delete(filePath);
                    }  
                    

                    // Output the renaming action
                    Console.WriteLine($"Renamed: {fileName} to {newFileName}");
                }
                else if (fileName.Contains(searchString)  && !(searchString == fileName))
                {
                    File.Delete(filePath);
                }   
            }
        }

        #region Logging
        private void Log(string message, string logType = "Log")
        {
            // Benachrichtigen Sie über das Event, wenn jemand auf das Event abonniert hat
            switch (logType.ToLower())
            {
                case "log": LogMessageUpdated?.Invoke(message); break;
                case "debug": DebugLogMessageUpdated?.Invoke(message); break;
                case "warning": WarningLogMessageUpdated?.Invoke(message); break;
                case "error": ErrorLogMessageUpdated?.Invoke(message); break;
                default:
                    DebugLogMessageUpdated?.Invoke(message); break;
            }
        }
        #endregion
    }
}
