using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class AssemblyExtractor
    {
        private readonly ReferenceFeatureExtractor references;
        private readonly Action<string, string> log;
        public AssemblyExtractor(ReferenceFeatureExtractor references, Action<string, string> log)
        { this.references = references; this.log = log; }
        private void Log(string message, string level = "Log") { log(message, level); }

        public MountingDescription ExtractAssemblyComponents(MountingDescription mountingDescription, ModelDoc2 assemblyModel, Matrix4x4 relativeTransform)
        {
            // Execute this only if the document is an assembly
            if (assemblyModel == null)
            {
                Log("Can't extract assembly components, because document is not an assembly", "warning");
                return mountingDescription;
            }

            // Check if the current Assembly is actually an assembly
            if (!(assemblyModel is IAssemblyDoc assemblyDoc))
            {
                Log("Current assembly is not an assembly", "warning");
                return mountingDescription;
            }

            Matrix4x4 SA_T_OA = relativeTransform; // Transformation from the Origin of the Assembly to the SWASI Origin of the Assembly

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

                MathTransform swXForm = component.Transform2;
                CoordinateSystemDescription OA_T_OB = new CoordinateSystemDescription();

                OA_T_OB.FromMatrix4x4(CoordinateTransforms.FromCadArray((double[])swXForm.ArrayData));
                Matrix4x4 OA_T_OB_matrix = OA_T_OB.AsMatrix4x4();

                FeatureManager swFeatureManager = componentModel.FeatureManager;
                // Get all features in the feature manager
                object[] features = (object[])swFeatureManager.GetFeatures(false);

                // Get Origin
                var Output = references.ExtractOrigin(componentModel, features);
                CoordinateSystemDescription OB_T_SB = new CoordinateSystemDescription();
                Matrix4x4 OB_T_SB_matrix = Matrix4x4.Identity;
                // If extraction of Swasi origin is successful
                if (Output.Item1)
                {
                    OB_T_SB = Output.Item2;
                    OB_T_SB_matrix = OB_T_SB.AsMatrix4x4();
                }
                else
                {
                    Log($"Skipping component {component.Name}: no valid SWASI origin.", "warning");
                    continue;
                }

                Matrix4x4 SA_T_OB_matrix = Matrix4x4.Multiply(SA_T_OA, OA_T_OB_matrix);
                Matrix4x4 SA_T_SB_matrix = Matrix4x4.Multiply(SA_T_OB_matrix, OB_T_SB_matrix);

                // Set the transformation after transforming it to the SWASI origin
                componentDescription.transformation.FromMatrix4x4(SA_T_SB_matrix);

                // Insert the component description at the beginning of the list
                mountingDescription.components.Insert(0, componentDescription);
            }

            return mountingDescription;
        }

        public MountingDescription ExtractAssemblyMates(MountingDescription mountingDescription, object[] _Features)
        {
            foreach (object Feat in _Features)
            {
                if (!(Feat is Feature))
                {
                    continue;
                }

                Feature Feature = (Feature)Feat;
                if (!FeatureNameRules.IsReference(Feature.Name))
                {
                    continue;
                }

                // Currently only 'MateDistanceDim' and 'MateCoincident' are supported!
                if (Feature.GetTypeName2() == "MateDistanceDim" || Feature.GetTypeName2() == "MateCoincident")
                {
                    // Extract the name of the components that are associated with the mate
                    Mate2 FeatureMate = (Mate2)Feature.GetSpecificFeature2();
                    int entityCount = FeatureMate.GetMateEntityCount();
                    if (entityCount != 2) throw new InvalidOperationException("A supported mate must have exactly two entities.");
                    int index;
                    float planeOffset = 0;
                    int planeMatchIndex = -1;
                    bool flip_normal_vector = false;    // in Solidworks, it is always the component behind the first component that is aligned to the "earlier" one.
                    bool setSuccess = false;
                    string[] componentNames = new string[] { null, null };

                    // Entity count should normally be two
                    for (index = 0; index < entityCount; index++)
                    {
                        IMateEntity2 MateEntity = (IMateEntity2)FeatureMate.MateEntity(index);
                        // Get component of the MateEntity
                        Component2 ComponentMateEntity = (Component2)MateEntity.ReferenceComponent;
                        // Set the component name. The following method deals with the correct setting of names
                        componentNames[index] = ComponentMateEntity.Name2.Replace(FeatureNameRules.Prefix, "");
                    }

                    if (componentNames[0] == null || componentNames[1] == null)
                    {
                        Log($"Error while extracting feature {Feature.Name}", "Error");
                        return mountingDescription;
                    }

                    // Components without an origin are intentionally omitted.
                    // Their mates must also be omitted, rather than producing
                    // dangling references or failing the valid parent assembly.
                    if (componentNames.Any(name => !mountingDescription.components.Any(component =>
                        component.name.Replace(FeatureNameRules.Prefix, "") == name)))
                    {
                        Log($"Skipping mate '{Feature.Name}': it references a component excluded from export.", "warning");
                        continue;
                    }

                    // Try to add the components to one of the three constraints, the function returns the index at which the constraints should be added, this is important if there are more than two parts in SolidWorks
                    int constraintIndex = mountingDescription.AddComponentsAssemblyConstraint(componentNames[0], componentNames[1]);

                    // This method extracts if component 1 or component 2 should be moved, it does so by looking at the transformations in the components list and returns true if component 1 has a higher z-value, else false
                    // Make sure that the MountingDescription holds the components before you call ExtractAssemblyMates
                    try
                    {
                        bool moveComponent_1 = IdentifyComponent1MovingPart(mountingDescription.components, componentNames[0], componentNames[1]);
                        mountingDescription.assemblyConstraints[constraintIndex].moveComponent_1 = moveComponent_1;
                    }
                    catch (Exception ex)
                    {
                        Log($"Can't determin moveComponent_1, because of {ex.Message}", "Error");
                    }

                    int component_1_index = mountingDescription.assemblyConstraints[constraintIndex].GetComponentIndex(componentNames[0]);

                    if (component_1_index == 0)
                    {
                        Log($"Error while extracting feature {Feature.Name}, component {componentNames[0]} not found in MountingDescription. Contact Maintainer", "Error");
                        return mountingDescription;
                    }

                    object _Feature = Feature.GetDefinition();
                    string[] planeNames = new string[] { null, null };
                    string plane_1 = "";
                    string plane_2 = "";
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
                                planeNames[i] = _FeatureMate.Name;
                            }
                        }

                        if (component_1_index == 1)
                        {
                            plane_1 = planeNames[0].Replace(FeatureNameRules.Prefix, "");
                            plane_2 = planeNames[1].Replace(FeatureNameRules.Prefix, "");
                        }
                        if (component_1_index == 2)
                        {
                            plane_1 = planeNames[1].Replace(FeatureNameRules.Prefix, "");
                            plane_2 = planeNames[0].Replace(FeatureNameRules.Prefix, "");
                        }

                        // Set the plane names to the Description, returns the index at which the Plane match has been inserted (could only be 1, 2, 3, zero in case it could not be added)
                        var Output = mountingDescription.assemblyConstraints[constraintIndex].description.SetPlaneMatch(plane_1, plane_2);

                        planeMatchIndex = Output.Item1;
                        setSuccess = Output.Item2;

                        // Get sign of distance
                        int sign = swMate.FlipDimension ? -1 : 1;
                        planeOffset = (float)swMate.Distance * 1000 * sign;

                        if (swMate.MateAlignment == 1)
                        {
                            flip_normal_vector = true;
                        }

                        if (!setSuccess)
                        {
                            Log($"Error while extracting feature {Feature.Name}", "Error");
                            return mountingDescription;
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
                                planeNames[i] = _FeatureMate.Name;
                            }
                        }

                        setSuccess = false;

                        if (component_1_index == 1)
                        {
                            plane_1 = planeNames[0].Replace(FeatureNameRules.Prefix, "");
                            plane_2 = planeNames[1].Replace(FeatureNameRules.Prefix, "");
                        }
                        if (component_1_index == 2)
                        {
                            plane_1 = planeNames[1].Replace(FeatureNameRules.Prefix, "");
                            plane_2 = planeNames[0].Replace(FeatureNameRules.Prefix, "");
                        }

                        if (FeatureNameRules.IsReference(planeNames[0]) && FeatureNameRules.IsReference(planeNames[1]))
                        {
                            var Output = mountingDescription.assemblyConstraints[constraintIndex].description.SetPlaneMatch(plane_1, plane_2);

                            planeMatchIndex = Output.Item1;
                            setSuccess = Output.Item2;
                        }

                        if (!setSuccess)
                        {
                            Log($"Error while extracting feature {Feature.Name}", "Error");
                            return mountingDescription;
                        }

                        if (swMate.MateAlignment == 1)
                        {
                            flip_normal_vector = true;
                        }
                    }

                    // Set the plane offset and the normal vector inversion for the planes
                    if (planeMatchIndex == 1)
                    {
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_1.planeOffset = planeOffset;
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_1.invertNormalVector = flip_normal_vector;
                    }

                    if (planeMatchIndex == 2)
                    {
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_2.planeOffset = planeOffset;
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_2.invertNormalVector = flip_normal_vector;
                    }

                    if (planeMatchIndex == 3)
                    {
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_3.planeOffset = planeOffset;
                        mountingDescription.assemblyConstraints[constraintIndex].description.planeMatch_3.invertNormalVector = flip_normal_vector;
                    }
                }
            }

            bool extractSuccess = mountingDescription.CompleteAssemblyConstraintDescription();

            if (!extractSuccess)
            {
                Log("Error extracting assembly constraints", "Error");
            }

            return mountingDescription;
        }

        public bool IdentifyComponent1MovingPart(List<AssemblyComponentDescription> components, string componentName1, string componentName2)
        {
            float zValue1 = 0;
            float zValue2 = 0;

            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].name == componentName1)
                {
                    zValue1 = components[i].transformation.translation.Z;
                }

                if (components[i].name == componentName2)
                {
                    zValue2 = components[i].transformation.translation.Z;
                }
            }

            return zValue1 >= zValue2;
        }
    }
}
