using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class ConstraintFrameManager
    {
        // Kept only so frames created by the earlier sketch-based implementation can be cleaned up.
        private const string LegacyHelperPrefix = "SWASI_CONSTRAINT_HELPER_";
        private readonly SwasiMetadataStore store = new SwasiMetadataStore();
        private readonly SwasiFeatureCatalog catalog = new SwasiFeatureCatalog();
        private readonly PreciseConstraintEngine engine = new PreciseConstraintEngine();
        private readonly Action<string, string> log;

        public ConstraintFrameManager(Action<string, string> log) { this.log = log; }

        public void CreateOrUpdate(ModelDoc2 document, string frameName, SwasiFrameMetadata frameMetadata, bool requireNew = false)
        {
            ValidateFrameName(frameName);
            if (frameMetadata == null || frameMetadata.constraintKind == ConstraintKind.None)
                throw new ArgumentException("Choose a creation constraint.");
            var metadata = store.Load(document);
            var previousFrames = new Dictionary<string, SwasiFrameMetadata>(metadata.frames, StringComparer.Ordinal);
            var existingNames = catalog.Read(document).Select(f => f.Name).ToArray();
            bool hadGeometry = existingNames.Contains(frameName, StringComparer.OrdinalIgnoreCase);
            frameMetadata.isConstraintFrame = true;
            metadata.SetConstraintFrame(frameName, frameMetadata, existingNames, requireNew);
            store.Save(document, metadata);
            try { UpdateAll(document, true); }
            catch
            {
                metadata.frames = previousFrames;
                store.Save(document, metadata);
                if (!hadGeometry) DeleteManagedFeatures(document, frameName);
                throw;
            }
        }

        internal static bool IsUpdating { get; private set; }

        public IReadOnlyList<string> UpdateAll(ModelDoc2 document, bool createMissing)
        {
            if (IsUpdating) return new List<string>();
            IsUpdating = true;
            try
            {
                var warnings = UpdateAllCore(document, createMissing).ToList();
                SwasiFeatureFolders.Organize(document, (message, level) =>
                {
                    warnings.Add(message);
                    log(message, level);
                });
                return warnings;
            }
            finally { IsUpdating = false; }
        }

        private IReadOnlyList<string> UpdateAllCore(ModelDoc2 document, bool createMissing)
        {
            var warnings = new List<string>();
            var metadata = store.Load(document);
            var items = catalog.Read(document).ToDictionary(f => f.Name, StringComparer.Ordinal);
            Feature originFeature = catalog.FindOrigin(document);
            PrecisePose axisReference = originFeature == null
                ? new PrecisePose()
                : SwasiFeatureCatalog.ReadPreciseCoordinateSystemPose(document, originFeature);
            var resolved = new Dictionary<string, PrecisePose>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            Func<string, PrecisePose> resolve = null;
            resolve = name =>
            {
                if (resolved.TryGetValue(name, out var cached)) return cached;
                if (!visiting.Add(name)) throw new InvalidOperationException($"Constraint cycle detected at '{name}'.");
                try
                {
                    PrecisePose pose;
                    if (metadata.frames.TryGetValue(name, out var frame) && frame.isConstraintFrame)
                        pose = Calculate(frame, resolve, axisReference);
                    else if (items.TryGetValue(name, out var item))
                        pose = item.Pose;
                    else
                        throw new InvalidOperationException($"Referenced SWASI frame '{name}' does not exist.");
                    resolved[name] = pose;
                    return pose;
                }
                finally { visiting.Remove(name); }
            };

            foreach (var entry in metadata.frames.Where(e => e.Value.isConstraintFrame).ToList())
            {
                try
                {
                    PrecisePose pose = resolve(entry.Key);
                    UpdateSolidWorksFrame(document, entry.Key, pose, createMissing, warnings);
                }
                catch (Exception ex)
                {
                    string message = $"Constraint frame '{entry.Key}': {ex.Message}";
                    warnings.Add(message); log(message + System.Environment.NewLine + ex, "error");
                    if (createMissing) throw new InvalidOperationException(message, ex);
                }
            }

            foreach (var entry in metadata.frames)
            {
                var inPlane = entry.Value.constraints?.inPlane;
                if (inPlane == null || inPlane.refFrameNames == null || inPlane.refFrameNames.Count == 0) continue;
                try
                {
                    var result = engine.ValidateInPlane(resolve(entry.Key),
                        inPlane.refFrameNames.Select(resolve).ToList(), inPlane.planeOffset,
                        Math.Max(0, entry.Value.inPlaneToleranceMm));
                    if (!result.IsInPlane)
                    {
                        string message = $"Frame '{entry.Key}' is {Math.Abs(result.SignedDistance):0.###} mm outside its assigned plane.";
                        warnings.Add(message); log(message, "warning");
                    }
                }
                catch (Exception ex)
                {
                    string message = $"In-plane check for '{entry.Key}': {ex.Message}";
                    warnings.Add(message); log(message, "error");
                }
            }
            return warnings;
        }

        public void Rename(ModelDoc2 document, string oldName, string newName)
        {
            ValidateFrameName(newName);
            if (newName.StartsWith(FeatureNameRules.AutoPrefix, StringComparison.Ordinal)
                || FeatureNameRules.IsOrigin(FeatureNameRules.Prefix + newName))
                throw new ArgumentException("Choose a frame name without a generated prefix or reserved origin name.");
            var original = store.Load(document);
            var updated = store.Load(document);
            updated.RenameConstraintFrame(oldName, newName, catalog.Read(document).Select(f => f.Name));
            if (oldName == newName) return;
            if (catalog.Read(document).Any(f => f.Name != oldName && string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A SWASI point or frame named '{newName}' already exists.");

            var features = SwasiFeatureCatalog.Features(document).ToList();
            var oldNames = ManagedFeatureNames(oldName).ToArray();
            var newNames = ManagedFeatureNames(newName).ToArray();
            var changes = new List<Tuple<Feature, string, string>>();
            for (int i = 0; i < oldNames.Length; i++)
            {
                string source = oldNames[i], target = newNames[i];
                if (features.Any(f => string.Equals(f.Name, target, StringComparison.OrdinalIgnoreCase)
                    && f.Name != source))
                    throw new InvalidOperationException($"A feature named '{target}' already exists.");
                var feature = features.FirstOrDefault(f => f.Name == source);
                if (feature != null) changes.Add(Tuple.Create(feature, source, target));
            }
            var applied = new List<Tuple<Feature, string, string>>();
            bool wasUpdating = IsUpdating;
            IsUpdating = true;
            try
            {
                foreach (var change in changes)
                {
                    applied.Add(change);
                    change.Item1.Name = change.Item3;
                    if (change.Item1.Name != change.Item3)
                        throw new InvalidOperationException($"SolidWorks could not rename '{change.Item2}' to '{change.Item3}'.");
                }
                store.Save(document, updated);
            }
            catch
            {
                foreach (var change in applied.AsEnumerable().Reverse())
                {
                    try { change.Item1.Name = change.Item2; }
                    catch (Exception ex) { log("Could not restore feature name: " + ex.Message, "error"); }
                }
                store.Save(document, original);
                throw;
            }
            finally { IsUpdating = wasUpdating; }
        }

        public void Delete(ModelDoc2 document, string frameName)
        {
            var metadata = store.Load(document);
            if (!metadata.frames.TryGetValue(frameName, out var frame) || !frame.isConstraintFrame)
                throw new InvalidOperationException("The selected frame is not a SWASI constraint frame.");
            document.ClearSelection2(true);
            foreach (string featureName in ManagedFeatureNames(frameName))
            {
                Feature feature = FindFeature(document, featureName);
                if (feature != null) feature.Select2(true, 0);
            }
            document.EditDelete();
            metadata.frames.Remove(frameName);
            store.Save(document, metadata);
        }

        private PrecisePose Calculate(SwasiFrameMetadata frame,
            Func<string, PrecisePose> resolve,
            PrecisePose axisReference)
        {
            var constraints = frame.constraints ?? new RefFrameConstraints();
            switch (frame.constraintKind)
            {
                case ConstraintKind.Centroid:
                    if (constraints.centroid.refFrameNames == null)
                        throw new InvalidOperationException("Centroid references are missing.");
                    return engine.Centroid(constraints.centroid.refFrameNames.Select(resolve).ToList(),
                        ToVector(constraints.centroid.offsetValues));
                case ConstraintKind.Orthogonal:
                    var o = constraints.orthogonal;
                    return engine.Orthogonal(resolve(o.frame_1), resolve(o.frame_2), resolve(o.frame_3),
                        o.distance_from_f1, o.unit_distance_from_f1 == "%",
                        o.distance_from_f1_f2_connection, o.frame_orthogonal_connection_axis,
                        o.frame_normal_plane_axis, axisReference);
                case ConstraintKind.Transform:
                    var t = constraints.transform;
                    return engine.Transform(resolve(t.refFrame), PrecisePose.FromLegacy(t.transform));
                default:
                    throw new InvalidOperationException("The constraint frame has no supported creation constraint.");
            }
        }

        private void UpdateSolidWorksFrame(ModelDoc2 document, string frameName,
            PrecisePose pose, bool createMissing, List<string> warnings)
        {
            string coordinateName = FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + frameName;
            Feature coordinate = FindFeature(document, coordinateName) ?? FindFeature(document, FeatureNameRules.Prefix + frameName);
            if (coordinate == null && !createMissing)
                throw new InvalidOperationException("The SolidWorks coordinate-system feature is missing.");

            if (coordinate == null || !PoseMatches(document, coordinate, pose))
            {
                // The origin point has its own plane/axis parents. Never include it
                // in coordinate replacement: user planes can depend on that point.
                if (coordinate != null)
                {
                    document.ClearSelection2(true);
                    if (!coordinate.Select2(false, 0))
                        throw new InvalidOperationException("Could not select the previous coordinate system.");
                    document.EditDelete();
                }
                CreateFrame(document, frameName, pose);
            }
            try { ConstraintOriginPointManager.Update(document, frameName, pose.translation); }
            catch (Exception ex)
            {
                string message = $"Constraint frame '{frameName}' was retained, but its origin point/helpers could not be synchronized: {ex.Message}";
                warnings.Add(message);
                log(message + System.Environment.NewLine + ex, "warning");
            }
        }

        private static void CreateFrame(ModelDoc2 document, string name, PrecisePose pose)
        {
            try
            {
                double[] angles = CoordinateTransforms.ToEulerXyzDouble(pose.rotation);
                Feature feature = ((dynamic)document.FeatureManager).CreateCoordinateSystemUsingNumericalValues(
                    true, pose.translation.X / 1000d, pose.translation.Y / 1000d, pose.translation.Z / 1000d,
                    true, angles[0], angles[1], angles[2]) as Feature;
                if (feature == null)
                    throw new InvalidOperationException("SolidWorks could not create the numerical coordinate system.");
                feature.Name = FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + name;
                if (!PoseMatches(document, feature, pose))
                    throw new InvalidOperationException("The created coordinate system does not match the calculated position/orientation.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Numerical coordinate-system creation (CreateCoordinateSystemUsingNumericalValues) failed: " +
                    ex.Message + " No helper sketch was created.", ex);
            }
        }

        private static bool PoseMatches(ModelDoc2 document, Feature feature, PrecisePose expected)
        {
            var actual = document.Extension.GetCoordinateSystemTransformByName(feature.Name);
            return actual != null && CoordinateTransforms.CadPoseMatches((double[])actual.ArrayData,
                expected.translation, expected.rotation);
        }

        private static void DeleteManagedFeatures(ModelDoc2 document, string frameName)
        {
            document.ClearSelection2(true);
            foreach (string featureName in ManagedFeatureNames(frameName))
            {
                Feature feature = FindFeature(document, featureName);
                if (feature != null) feature.Select2(true, 0);
            }
            document.EditDelete();
        }

        private static IEnumerable<string> ManagedFeatureNames(string name)
        {
            yield return FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + name;
            yield return FeatureNameRules.Prefix + name;
            foreach (string helper in ConstraintOriginPointManager.FeatureNames(name)) yield return helper;
            foreach (string helper in LegacyHelperNames(name)) yield return helper;
        }
        private static IEnumerable<string> LegacyHelperNames(string name)
        {
            yield return LegacyHelperPrefix + name + "_X";
            yield return LegacyHelperPrefix + name + "_Y";
            yield return LegacyHelperPrefix + name + "_Z";
        }

        private static Feature FindFeature(ModelDoc2 document, string name) =>
            SwasiFeatureCatalog.Features(document).FirstOrDefault(f => f.Name == name);
        private static Vector3d ToVector(IReadOnlyList<float> values) => values != null && values.Count >= 3
            ? new Vector3d(values[0], values[1], values[2]) : Vector3d.Zero;
        private static void ValidateFrameName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a frame name.");
            if (name.StartsWith(FeatureNameRules.Prefix, StringComparison.Ordinal))
                throw new ArgumentException("Enter the name without the SWASI_ prefix.");
            if (name.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                throw new ArgumentException("The frame name contains invalid characters.");
        }
    }
}
