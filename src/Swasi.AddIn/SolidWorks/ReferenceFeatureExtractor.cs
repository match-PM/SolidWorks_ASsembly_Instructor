using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class ReferenceFeatureExtractor
    {
        private readonly Action<string, string> log;
        public ReferenceFeatureExtractor(Action<string, string> log) { this.log = log; }
        private void Log(string message, string level = "Log") { log(message, level); }

        private static Vector3 GetReferencePlaneNormal(IRefPlane referencePlane, Matrix4x4 relativeTransform)
        {
            var values = (double[])referencePlane.Transform.ArrayData;
            return CoordinateTransforms.TransformPlaneNormal(
                new Vector3((float)values[6], (float)values[7], (float)values[8]), relativeTransform);
        }

        public (bool, CoordinateSystemDescription) ExtractOrigin(ModelDoc2 document, object[] features)
        {
            Log($"Extracting {(document is AssemblyDoc ? "Assembly" : "Component")} {document.GetTitle()}");
            CoordinateSystemDescription swasiOrigin = new CoordinateSystemDescription();
            bool extractSuccess = false;

            foreach (object featureObj in features)
            {
                // Check if the feature is a reference coordinate system
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    // Check for CoordSys origin
                    if (feature.GetTypeName2() == "CoordSys" && FeatureNameRules.IsOrigin(feature.Name))
                    {
                        if (!extractSuccess)
                        {
                            ICoordinateSystemFeatureData coordSys = (ICoordinateSystemFeatureData)feature.GetDefinition();
                            if (!coordSys.AccessSelections(document, null)) throw new InvalidOperationException("Could not access origin selections.");
                            try
                            {

                                // Extract the coordinate system information
                                swasiOrigin.FromMatrix4x4(CoordinateTransforms.FromCadArray((double[])coordSys.Transform.ArrayData));
                                swasiOrigin.name = feature.Name.Replace(FeatureNameRules.OriginPrefix, "");
                                Log($"Found SWASI Origin: {swasiOrigin.name}");

                            }
                            finally { coordSys.ReleaseSelectionAccess(); }
                            extractSuccess = true;
                        }
                        else
                        {
                            // Multiple origins found - error condition
                            Log($"Error finding SWASI Origin. There have been defined two origins with the naming convention.", "Error");
                            return (false, swasiOrigin);
                        }
                    }
                    // Check for OriginProfileFeature origin
                    else if (feature.GetTypeName2() == "OriginProfileFeature" && FeatureNameRules.IsOrigin(feature.Name))
                    {
                        if (!extractSuccess)
                        {
                            try
                            {
                                // For OriginProfileFeature, attempt to get the transform from the feature itself
                                // or from the reference coordinate system it may contain
                                IFeature specificFeature = feature.GetSpecificFeature2();

                                // Try to get transform data - OriginProfileFeature may have associated geometry
                                MathTransform transform = null;

                                // Check if the feature has a coordinate system associated with it
                                if (specificFeature != null && specificFeature is IRefPlane)
                                {
                                    IRefPlane refPlane = (IRefPlane)specificFeature;
                                    transform = refPlane.Transform;
                                }
                                else
                                {
                                    // Try to get from the feature definition if available
                                    object featureDef = feature.GetDefinition();

                                    // For origin profile features, we may need to extract position differently
                                    // This is a fallback - the actual transform should come from the profile geometry
                                    if (featureDef != null)
                                    {
                                        try
                                        {
                                            // Attempt to access transform through reflection or dynamic typing
                                            dynamic defDynamic = featureDef;
                                            transform = defDynamic.Transform as MathTransform;
                                        }
                                        catch
                                        {
                                            // If Transform property doesn't exist, log a warning
                                            Log($"Warning: Could not extract transform from OriginProfileFeature '{feature.Name}' - using default identity transform", "warning");
                                            // Use identity transform as fallback
                                            transform = null;
                                        }
                                    }
                                }

                                if (transform != null)
                                {
                                    swasiOrigin.FromMatrix4x4(CoordinateTransforms.FromCadArray((double[])transform.ArrayData));
                                }
                                else
                                {
                                    // Use identity transform if no transform could be extracted
                                    Log($"Using identity transform for OriginProfileFeature '{feature.Name}'", "debug");
                                }

                                swasiOrigin.name = feature.Name.Replace(FeatureNameRules.OriginPrefix, "");
                                Log($"Found SWASI Origin (OriginProfileFeature): {swasiOrigin.name}");

                                extractSuccess = true;
                            }
                            catch (Exception ex)
                            {
                                Log($"Error extracting OriginProfileFeature: {ex.Message}", "Error");
                                return (false, swasiOrigin);
                            }
                        }
                        else
                        {
                            // Multiple origins found - error condition
                            Log($"Error finding SWASI Origin. There have been defined two origins with the naming convention.", "Error");
                            return (false, swasiOrigin);
                        }
                    }
                }
            }
            if (!extractSuccess)
            {
                Log($"No SWASI Origin found in '{document.GetTitle()}'! JSON will not be created!", "error");
            }
            return (extractSuccess, swasiOrigin);
        }

        public List<RefFrameDescription> ExtractRefFrames(ModelDoc2 document, object[] features, Matrix4x4 relativeTransform)
        {
            List<RefFrameDescription> refFrames = new List<RefFrameDescription>();

            foreach (object featureObj in features)
            {
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    var output = ExtractRefFrame(document, feature, relativeTransform);

                    if (output.Item1)
                    {
                        refFrames.Add(output.Item2);
                    }
                }
            }

            return refFrames;
        }

        public (bool, RefFrameDescription) ExtractRefFrame(ModelDoc2 document, Feature feature, Matrix4x4 relativeTransform)
        {
            RefFrameDescription refFrame = new RefFrameDescription();
            refFrame.type = "frame";

            // Check if the feature is a coordinate system
            if (feature.GetTypeName2() != "CoordSys")
            {
                return (false, refFrame);
            }

            if (!FeatureNameRules.IsReference(feature.Name) || FeatureNameRules.IsOrigin(feature.Name))
            {
                // Pops up on SW-Frames
                Log($"RefFrame ({feature.Name}) was ignored. Either Identifier is required or is Origin Identifier", "warning");
                return (false, refFrame);
            }

            // Set properties based on feature name identifiers
            FeatureNameRules.ApplyProperties(refFrame, feature.Name);

            ICoordinateSystemFeatureData coordSys = (ICoordinateSystemFeatureData)feature.GetDefinition();
            if (!coordSys.AccessSelections(document, null)) throw new InvalidOperationException("Could not access frame selections.");
            try
            {

                // Set the transformation from Array Data in Solidworks Coordinate System
                refFrame.transformation.FromMatrix4x4(CoordinateTransforms.FromCadArray((double[])coordSys.Transform.ArrayData));

                // Set the transformation after transforming it to the SWASI origin
                refFrame.transformation.FromMatrix4x4(Matrix4x4.Multiply(relativeTransform, refFrame.transformation.AsMatrix4x4()));

            }
            finally { coordSys.ReleaseSelectionAccess(); }
            refFrame.name = feature.Name.Replace(FeatureNameRules.Prefix, "");

            Log($"Extracted data for: {refFrame.name}");

            return (true, refFrame);
        }

        public List<RefFrameDescription> ExtractRefPoints(object[] features, Matrix4x4 relativeTransform)
        {
            List<RefFrameDescription> refPoints = new List<RefFrameDescription>();

            // Iterate through each feature in the array
            foreach (object featureObj in features)
            {
                // Check if the feature is of type Feature
                if (featureObj is Feature)
                {
                    Feature feature = (Feature)featureObj;

                    // Extract the reference point information
                    var output = ExtractRefPoint(feature, relativeTransform);

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

        public (bool, RefFrameDescription) ExtractRefPoint(Feature point_feature, Matrix4x4 relativeTransform)
        {
            RefFrameDescription refPointDescription = new RefFrameDescription();
            refPointDescription.type = "point";
            bool extractSuccess = true;

            // Check if the feature is of type RefPoint
            if (point_feature.GetTypeName2() != "RefPoint")
            {
                return (false, refPointDescription);
            }

            // Check if RefPoint is tagged with the FeatureNameRules.Prefix
            if (!FeatureNameRules.IsReference(point_feature.Name))
            {
                Log($"RefPoint ({point_feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refPointDescription);
            }

            // Set properties based on feature name identifiers
            FeatureNameRules.ApplyProperties(refPointDescription, point_feature.Name);

            // Get the specific feature as RefPoint
            IFeature specificFeature = point_feature.GetSpecificFeature2();
            IRefPoint refPoint = (IRefPoint)specificFeature;
            MathPoint mathRefPoint = refPoint.GetRefPoint();

            // Set the translation values from the MathPoint (converted to millimeters)
            refPointDescription.transformation.translation.X = (float)mathRefPoint.ArrayData[0] * 1000;
            refPointDescription.transformation.translation.Y = (float)mathRefPoint.ArrayData[1] * 1000;
            refPointDescription.transformation.translation.Z = (float)mathRefPoint.ArrayData[2] * 1000;

            // Set the transformation after transforming it to the SWASI origin
            refPointDescription.transformation.FromMatrix4x4(Matrix4x4.Multiply(relativeTransform, refPointDescription.transformation.AsMatrix4x4()));

            // Reset the quaternion
            refPointDescription.transformation.rotation = Quaternion.Identity;

            refPointDescription.name = point_feature.Name.Replace(FeatureNameRules.Prefix, "");
            Log($"Extracted data for: {refPointDescription.name}");

            return (extractSuccess, refPointDescription);
        }

        public List<RefPlaneDescription> ExtractRefPlanes(ModelDoc2 document, object[] features, Matrix4x4 relativeTransform)
        {
            List<RefPlaneDescription> RefPlaneDescriptions = new List<RefPlaneDescription>();

            foreach (object featureobj in features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefPlane(document, _featureobj, relativeTransform);

                    if (output.Item1)
                    {
                        RefPlaneDescriptions.Add(output.Item2);
                        Log($"Extracted Plane: {output.Item2.name}");
                    }
                }
            }

            return RefPlaneDescriptions;
        }

        public (bool, RefPlaneDescription) ExtractRefPlane(ModelDoc2 document, Feature plane_feature, Matrix4x4 relativeTransform)
        {
            RefPlaneDescription refPlaneDescription = new RefPlaneDescription();
            bool extractSuccess = true;

            // Test if feature is actually a RefPlane
            if (plane_feature.GetTypeName2() != "RefPlane")
            {
                return (false, refPlaneDescription);
            }

            // Check if RefPlane is Tagged with the FeatureNameRules.Prefix
            if (!FeatureNameRules.IsReference(plane_feature.Name))
            {
                Log($"RefPlane ({plane_feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refPlaneDescription);
            }

            refPlaneDescription.name = plane_feature.Name.Replace(FeatureNameRules.Prefix, "");

            // Get normal vector for plane
            IFeature specificFeature = plane_feature.GetSpecificFeature2();
            IRefPlane referencePlane = (IRefPlane)specificFeature;
            refPlaneDescription.normalVector = GetReferencePlaneNormal(referencePlane, relativeTransform);

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
                    Log($"Plane is tagged with '{FeatureNameRules.Prefix}' but the given reference constraints are currently not supported!", "Error");
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



            // Access the features that define the RefPlane
            bool accessSuccess = swRefPlaneFeatureData.AccessSelections(document, null);

            if (!accessSuccess)
            {
                Log($"Error! Could not access: {plane_feature.Name}!", "Error");
                return (false, refPlaneDescription);
            }
            else
            {
                try
                {
                    object[] plane_sub_features = swRefPlaneFeatureData.Selections;
                    int pointIndex = 0;
                    int axisIndex = 0;

                    foreach (object plane_sub_feature in plane_sub_features)
                    {
                        // Check if the feature is a reference plane
                        if (plane_sub_feature is Feature)
                        {
                            Feature _plane_sub_feature = (Feature)plane_sub_feature;

                            // CHeck if the name is valid
                            if (!FeatureNameRules.IsReference(_plane_sub_feature.Name))
                            {
                                Log($"Plane ({plane_feature.Name}) is defined by ({_plane_sub_feature.Name}). Defining Axes and Points must have SWASI_Identifier ({FeatureNameRules.Prefix}), too!", "Warning");
                                return (false, refPlaneDescription);
                            }

                            // Append if point
                            if (_plane_sub_feature.GetTypeName2() == "RefPoint")
                            {
                                refPlaneDescription.refPointNames[pointIndex] = _plane_sub_feature.Name.Replace(FeatureNameRules.Prefix, "");
                                pointIndex++;
                            }

                            //Append if axis
                            if (_plane_sub_feature.GetTypeName2() == "RefAxis")
                            {
                                refPlaneDescription.refAxisNames[axisIndex] = _plane_sub_feature.Name.Replace(FeatureNameRules.Prefix, "");
                                axisIndex++;
                            }
                        }
                    }

                    if (planeType == 3 && pointIndex != 3)
                    {
                        Log($"Error! Plane '{plane_feature.Name}' is defined by three points, but not all of the points adhere to the swasi naming convention!", "Error");
                        return (false, refPlaneDescription);
                    }

                    if (planeType == 2 && axisIndex != 1 && pointIndex != 1)
                    {
                        Log($"Error! Plane '{plane_feature.Name}' is defined by one point and an axis, but neither the point nor the axis adhere to the swasi naming convention!", "Error");
                        return (false, refPlaneDescription);
                    }
                }
                finally { swRefPlaneFeatureData.ReleaseSelectionAccess(); }
            }


            return (extractSuccess, refPlaneDescription);
        }

        public List<RefAxisDescription> ExtractRefAxes(ModelDoc2 document, object[] features)
        {
            List<RefAxisDescription> refAxes = new List<RefAxisDescription>();

            foreach (object featureobj in features)
            {
                if (featureobj is Feature)
                {
                    Feature _featureobj = (Feature)featureobj;
                    var output = ExtractRefAxis(document, _featureobj);

                    if (output.Item1)
                    {
                        refAxes.Add(output.Item2);
                        Log($"Extracted axis: {output.Item2.name}");
                    }
                }
            }

            return refAxes;
        }

        public (bool, RefAxisDescription) ExtractRefAxis(ModelDoc2 document, Feature feature)
        {
            RefAxisDescription refAxisDescription = new RefAxisDescription();

            // Test if feature is actually a RefAxis
            if (feature.GetTypeName2() != "RefAxis")
            {
                return (false, refAxisDescription);
            }

            // Check if RefAxis is tagged with the FeatureNameRules.Prefix
            if (!FeatureNameRules.IsReference(feature.Name))
            {
                Log($"RefAxis ({feature.Name}) was ignored. Wrong Identifier?", "warning");
                return (false, refAxisDescription);
            }

            refAxisDescription.name = feature.Name.Replace(FeatureNameRules.Prefix, "");

            // Get axis definition
            RefAxisFeatureData swRefAxisFeatureData = (RefAxisFeatureData)feature.GetDefinition();

            // Access the features that define the RefAxis
            // If a part
            bool accessSuccess = swRefAxisFeatureData.AccessSelections(document, null);

            // if an assembly
            // AccessSelections need to be modified if an assembly
            // https://help.solidworks.com/2018/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IRefPlaneFeatureData~IAccessSelections.html

            if (!accessSuccess)
            {
                Log($"Error! Could not access: {feature.Name}!", "Error");
                return (false, refAxisDescription);
            }
            else
            {
                // There is this tutorial, but that's not what we want to extract
                // https://help.solidworks.com/2019/english/api/sldworksapi/get_selections_for_reference_axis_feature_example_csharp.htm

                object types = null;
                object[] obj;
                try { obj = (object[])swRefAxisFeatureData.GetSelections(out types); }
                finally { swRefAxisFeatureData.ReleaseSelectionAccess(); }

                // Release the access to the features from swRefAxisFeatureData!

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
                        if (!FeatureNameRules.IsReference(_feat.Name))
                        {
                            Log($"Axis ({feature.Name}) is defined by '{_feat.Name}'. Defining Points must have SWASI_Identifier ({FeatureNameRules.Prefix}), too!", "warning");
                            return (false, refAxisDescription);
                        }

                        // Append if point
                        if (_feat.GetTypeName2() == "RefPoint")
                        {
                            refAxisDescription.refPointNames[pointInd] = _feat.Name.Replace(FeatureNameRules.Prefix, "");
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
    }
}
