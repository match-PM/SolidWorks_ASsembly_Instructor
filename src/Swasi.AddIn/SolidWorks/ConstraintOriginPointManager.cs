using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    // Independent of the numerical coordinate feature: replacing that feature must
    // never delete the point or user planes that depend on it.
    internal static class ConstraintOriginPointManager
    {
        internal const string FolderName = "(auto)_SWASI_helper";
        private const string HelperPrefix = "(auto)_SWASI_helper_";

        internal static IEnumerable<string> FeatureNames(string frameName)
        {
            yield return PointName(frameName);
            yield return HelperPrefix + frameName + "_Axis";
            for (int i = 0; i < 3; i++) yield return HelperPrefix + frameName + "_Plane" + i;
        }

        private static string PointName(string frameName) =>
            FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + frameName + "_Point";

        internal static void Update(ModelDoc2 document, string frameName, Vector3d positionMm)
        {
            // No selection access, ModifyDefinition, visibility edits or rebuild
            // for a complete helper set already at the desired origin.
            var existing = SwasiFeatureCatalog.Features(document).ToDictionary(f => f.Name, StringComparer.Ordinal);
            if (FeatureNames(frameName).All(existing.ContainsKey)
                && existing[PointName(frameName)].GetTypeName2() == "RefPoint")
            {
                var current = (double[])((IRefPoint)existing[PointName(frameName)].GetSpecificFeature2()).GetRefPoint().ArrayData;
                if (ReferencePointGeometry.MatchesPosition(current, positionMm)) return;
            }
            var helpers = new List<Feature>();
            try
            {
                // Traverse in history order, not FeatureManager.GetFeatures order.
                // The first three RefPlanes are the document's standard planes;
                // their actual transforms avoid localized Front/Top/Right names.
                var bases = new List<Feature>();
                for (Feature f = document.FirstFeature() as Feature; f != null && bases.Count < 3;
                    f = f.GetNextFeature() as Feature)
                    if (f.GetTypeName2() == "RefPlane") bases.Add(f);
                if (bases.Count != 3)
                    throw new InvalidOperationException("The document's three standard reference planes were not found.");

                var normals = bases.Select(f => PlaneNormal((double[])((IRefPlane)f.GetSpecificFeature2()).Transform.ArrayData)).ToArray();
                if (Math.Abs(Vector3d.Dot(normals[0], Vector3d.Cross(normals[1], normals[2]))) < .99)
                    throw new InvalidOperationException("The standard reference planes must be mutually perpendicular.");

                for (int i = 0; i < 3; i++)
                {
                    double[] transform = (double[])((IRefPlane)bases[i].GetSpecificFeature2()).Transform.ArrayData;
                    double offset = ReferencePointGeometry.SignedPlaneOffsetMeters(transform, positionMm);
                    helpers.Add(EnsurePlane(document, bases[i], HelperPrefix + frameName + "_Plane" + i, offset));
                }

                string axisName = HelperPrefix + frameName + "_Axis";
                Feature axis = Find(document, axisName, "RefAxis");
                if (axis == null)
                {
                    var before = NamesOfType(document, "RefAxis");
                    Select(document, helpers[0], helpers[1]);
                    if (!document.InsertAxis2(true))
                        throw new InvalidOperationException("SolidWorks could not create the helper axis from the offset planes.");
                    axis = SwasiFeatureCatalog.Features(document).FirstOrDefault(f => f.GetTypeName2() == "RefAxis" && !before.Contains(f.Name));
                    if (axis == null) throw new InvalidOperationException("No helper reference axis was returned.");
                    axis.Name = axisName;
                }
                helpers.Add(axis);

                Feature point = Find(document, PointName(frameName), "RefPoint");
                if (point == null)
                {
                    var before = NamesOfType(document, "RefPoint");
                    Select(document, axis, helpers[2]);
                    object result = document.FeatureManager.InsertReferencePoint(
                        (int)swRefPointType_e.swRefPointIntersection, 0, 0, 1);
                    point = result as Feature ?? (result as object[])?.OfType<Feature>().FirstOrDefault()
                        ?? SwasiFeatureCatalog.Features(document).FirstOrDefault(f => f.GetTypeName2() == "RefPoint" && !before.Contains(f.Name));
                    if (point == null) throw new InvalidOperationException("SolidWorks could not create the axis/plane intersection point.");
                    point.Name = PointName(frameName);
                }

                // Hide construction geometry only. The point remains selectable.
                Select(document, helpers.ToArray());
                document.BlankRefGeom();
                Select(document, point);
                document.UnBlankRefGeom();
                PutInFolder(document, helpers);
                KeepPointOutsideFolder(document, point);
                document.ClearSelection2(true);
                document.EditRebuild3();

                var actual = (double[])((IRefPoint)point.GetSpecificFeature2()).GetRefPoint().ArrayData;
                if (!ReferencePointGeometry.MatchesPosition(actual, positionMm))
                    throw new InvalidOperationException("The reference point did not rebuild at the constraint-frame origin.");
            }
            finally { document.ClearSelection2(true); }
        }

        private static Feature EnsurePlane(ModelDoc2 document, Feature basis, string name, double offset)
        {
            bool zero = Math.Abs(offset) < 1e-12;
            int constraint = zero ? (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Coincident
                : (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
            if (!zero && offset < 0) constraint |= (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_OptionFlip;
            double distance = zero ? 0 : Math.Abs(offset);
            Feature plane = Find(document, name, "RefPlane");
            if (plane == null)
            {
                var before = NamesOfType(document, "RefPlane");
                Select(document, basis);
                object result = document.FeatureManager.InsertRefPlane(constraint, distance, 0, 0, 0, 0);
                plane = result as Feature ?? SwasiFeatureCatalog.Features(document)
                    .FirstOrDefault(f => f.GetTypeName2() == "RefPlane" && !before.Contains(f.Name));
                if (plane == null) throw new InvalidOperationException("SolidWorks could not create helper plane '" + name + "'.");
                plane.Name = name;
            }
            else
            {
                var data = (IRefPlaneFeatureData)plane.GetDefinition();
                if (!data.AccessSelections(document, null))
                    throw new InvalidOperationException("Could not access helper plane '" + name + "'.");
                bool modified = false;
                try
                {
                    if (data.get_Constraint(0) == constraint && Math.Abs(data.get_AngleOrDistance(0) - distance) < 1e-12)
                        return plane;
                    data.set_Constraint(0, constraint);
                    data.set_AngleOrDistance(0, distance);
                    modified = plane.ModifyDefinition(data, document, null);
                    if (!modified) throw new InvalidOperationException("Could not update helper plane '" + name + "'.");
                }
                finally { if (!modified) data.ReleaseSelectionAccess(); }
            }
            return plane;
        }

        private static void PutInFolder(ModelDoc2 document, IReadOnlyList<Feature> helpers)
        {
            Feature folder = Find(document, FolderName, "FtrFolder");
            if (folder == null)
            {
                // Empty-before does not depend on helpers being consecutive in history.
                Select(document, helpers[0]);
                folder = document.FeatureManager.InsertFeatureTreeFolder2((int)swFeatureTreeFolderType_e.swFeatureTreeFolder_EmptyBefore);
                if (folder == null) throw new InvalidOperationException("Could not create the SWASI helper folder.");
                folder.Name = FolderName;
            }
            var contents = (IFeatureFolder)folder.GetSpecificFeature2();
            var names = new HashSet<string>(((object[])contents.GetFeatures() ?? new object[0]).OfType<Feature>().Select(f => f.Name));
            foreach (Feature helper in helpers)
            {
                if (names.Contains(helper.Name)) continue;
                // Append after existing contents, so the axis is never inserted
                // ahead of its parent planes by swMoveToFolder.
                Feature last = ((object[])contents.GetFeatures() ?? new object[0]).OfType<Feature>().LastOrDefault();
                bool moved = last == null
                    ? document.Extension.ReorderFeature(helper.Name, folder.Name, (int)swMoveLocation_e.swMoveToFolder)
                    : document.Extension.ReorderFeature(helper.Name, last.Name, (int)swMoveLocation_e.swMoveAfter);
                if (!moved || !SwasiFeatureFolders.Contains(folder, helper))
                    throw new InvalidOperationException("Could not move '" + helper.Name + "' into " + FolderName + ".");
            }
        }

        private static void KeepPointOutsideFolder(ModelDoc2 document, Feature point)
        {
            Feature folder = Find(document, FolderName, "FtrFolder");
            if (folder == null) return;
            var contents = (IFeatureFolder)folder.GetSpecificFeature2();
            if (!((object[])contents.GetFeatures() ?? new object[0]).OfType<Feature>().Any(f => f.Name == point.Name)) return;

            // Migrate points grouped by earlier versions without recreating them.
            // Place after the closing folder marker, not after its opening feature.
            int depth = 1;
            for (Feature feature = folder.GetNextFeature() as Feature; feature != null;
                feature = feature.GetNextFeature() as Feature)
            {
                if (feature.GetTypeName2() != "FtrFolder") continue;
                depth += feature.Name.IndexOf("EndTag", StringComparison.Ordinal) >= 0 ? -1 : 1;
                if (depth != 0) continue;
                if (!document.Extension.ReorderFeature(point.Name, feature.Name, (int)swMoveLocation_e.swMoveAfter)
                    || ((object[])contents.GetFeatures() ?? new object[0]).OfType<Feature>().Any(f => f.Name == point.Name))
                    throw new InvalidOperationException("Could not move reference point '" + point.Name + "' outside the helper folder.");
                return;
            }
            throw new InvalidOperationException("Could not locate the end of the SWASI helper folder.");
        }

        private static Feature Find(ModelDoc2 document, string name, string type)
        {
            Feature feature = SwasiFeatureCatalog.Features(document).FirstOrDefault(f => f.Name == name);
            if (feature != null && feature.GetTypeName2() != type)
                throw new InvalidOperationException("Feature name '" + name + "' is already used by another feature type.");
            return feature;
        }
        private static HashSet<string> NamesOfType(ModelDoc2 document, string type) => new HashSet<string>(
            SwasiFeatureCatalog.Features(document).Where(f => f.GetTypeName2() == type).Select(f => f.Name), StringComparer.Ordinal);
        private static Vector3d PlaneNormal(double[] transform) => Vector3d.Normalize(
            new Vector3d(transform[6], transform[7], transform[8]));
        private static void Select(ModelDoc2 document, params Feature[] features)
        {
            document.ClearSelection2(true);
            foreach (Feature feature in features)
                if (!feature.Select2(true, 0)) throw new InvalidOperationException("Could not select '" + feature.Name + "'.");
        }
    }
}
