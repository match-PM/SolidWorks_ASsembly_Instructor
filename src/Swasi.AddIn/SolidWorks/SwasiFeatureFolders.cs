using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    internal static class SwasiFeatureFolders
    {
        internal const string MainFolderName = "SWASI";
        internal static bool IsOrganizing { get; private set; }

        internal static void Organize(ModelDoc2 document, Action<string, string> log)
        {
            if (IsOrganizing) return;
            IsOrganizing = true;
            try
            {
                // Keep folder membership explicit; ordinary history reordering
                // does not reliably transfer a feature across folder boundaries.
                var features = History(document).Where(f => f.GetTypeName2() != "FtrFolder").ToList();
                Group(document, features.Where(f => FeatureNameRules.IsReference(f.Name)
                    && !f.Name.StartsWith("SWASI_CONSTRAINT_HELPER_", StringComparison.Ordinal)).ToList(), MainFolderName, log);
                Group(document, features.Where(f => f.Name.StartsWith("(auto)_SWASI_helper_", StringComparison.Ordinal)).ToList(),
                    ConstraintOriginPointManager.FolderName, log);
            }
            finally { document.ClearSelection2(true); IsOrganizing = false; }
        }

        private static void Group(ModelDoc2 document, List<Feature> features, string name, Action<string, string> log)
        {
            if (features.Count == 0) return;
            try
            {
                Feature folder = History(document).FirstOrDefault(f => f.Name == name);
                if (folder == null)
                {
                    document.ClearSelection2(true);
                    if (!features.Last().Select2(false, 0))
                        throw new InvalidOperationException("Could not select '" + features.Last().Name + "' for grouping.");
                    folder = document.FeatureManager.InsertFeatureTreeFolder2((int)swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing);
                    if (folder == null) throw new InvalidOperationException("Could not create folder '" + name + "'.");
                    folder.Name = name;
                }
                if (folder.GetTypeName2() != "FtrFolder")
                    throw new InvalidOperationException("The name '" + name + "' is already used by a feature.");
                // Do not require moving the folder to the end first. SolidWorks
                // can reject that operation even when individual members can move,
                // which previously left a newly created SWASI folder empty.
                var pending = features.Where(f => !Contains(folder, f)).ToList();
                for (int pass = 0; pass < 2 && pending.Count > 0; pass++)
                {
                    var order = pass == 0 ? pending.AsEnumerable().Reverse().ToList() : pending.ToList();
                    foreach (Feature feature in order)
                    {
                        try
                        {
                            document.ClearSelection2(true);
                            document.Extension.ReorderFeature(feature.Name, folder.Name, (int)swMoveLocation_e.swMoveToFolder);
                            if (Contains(folder, feature)) pending.Remove(feature);
                        }
                        catch (Exception ex)
                        { if (pass == 1) log("Moving '" + feature.Name + "' to '" + name + "': " + ex.Message, "warning"); }
                    }
                }
                if (pending.Count > 0)
                    log("Folder '" + name + "' could not receive: " + string.Join(", ", pending.Select(f => f.Name))
                        + ". SolidWorks rejected these moves; feature dependencies or fixed default features can prevent grouping.", "warning");
            }
            catch (Exception ex) { log("SWASI folder organization: " + ex.Message, "warning"); }
        }

        internal static bool Contains(Feature folder, Feature feature) =>
            (((IFeatureFolder)folder.GetSpecificFeature2()).GetFeatures() as object[] ?? new object[0])
                .OfType<Feature>().Any(f => f.Name == feature.Name);

        private static IEnumerable<Feature> History(ModelDoc2 document)
        {
            for (Feature f = document.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
                yield return f;
        }
    }
}
