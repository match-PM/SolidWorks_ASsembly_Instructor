using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SolidWorks_ASsembly_Instructor
{
    [Flags]
    public enum SwasiFrameRole
    {
        None = 0,
        Vision = 1,
        Laser = 2,
        Gripping = 4,
        Target = 8,
        Assembly = 16,
        Glue = 32
    }

    public enum ConstraintKind
    {
        None,
        Centroid,
        Orthogonal,
        Transform
    }

    public sealed class SwasiDocumentMetadata
    {
        public Dictionary<string, SwasiFrameMetadata> frames =
            new Dictionary<string, SwasiFrameMetadata>(StringComparer.Ordinal);
        public ColorComp componentColor = new ColorComp(255, 255, 255);
        public bool hasComponentColor;

        public void SetConstraintFrame(string name, SwasiFrameMetadata frame, IEnumerable<string> existingFeatureNames, bool requireNew)
        {
            var existing = new HashSet<string>(existingFeatureNames, StringComparer.OrdinalIgnoreCase);
            bool isUpdate = frames.TryGetValue(name, out var previous) && previous.isConstraintFrame;
            if (existing.Contains(name) && (requireNew || !isUpdate))
                throw new InvalidOperationException($"A SWASI point or frame named '{name}' already exists. Choose a different name.");
            // Native deletion leaves metadata behind. Replace only the requested
            // name, including old casing, without redirecting other references.
            var stale = new List<string>();
            foreach (string key in frames.Keys)
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) stale.Add(key);
            foreach (string key in stale) frames.Remove(key);
            frames[name] = frame;
        }

        public void RenameConstraintFrame(string oldName, string newName, IEnumerable<string> existingFeatureNames = null)
        {
            if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Enter a frame name.");
            if (!frames.TryGetValue(oldName, out var frame) || !frame.isConstraintFrame)
                throw new InvalidOperationException("The selected frame is not a SWASI constraint frame.");
            if (oldName == newName) return;
            var existing = existingFeatureNames == null ? null
                : new HashSet<string>(existingFeatureNames, StringComparer.OrdinalIgnoreCase);
            if (existing != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)
                && existing.Contains(newName))
                throw new InvalidOperationException($"A SWASI point or frame named '{newName}' already exists.");
            var staleNames = new List<string>();
            foreach (string name in frames.Keys)
                if (string.Equals(name, newName, StringComparison.OrdinalIgnoreCase) && name != oldName)
                {
                    // Native deletion can leave ordinary settings or a creation
                    // constraint behind. Neither reserves a name without geometry.
                    // Replace only the requested destination entry on a successful rename.
                    if (existing == null || existing.Contains(name))
                        throw new InvalidOperationException($"A frame named '{newName}' already exists.");
                    staleNames.Add(name);
                }
            foreach (string name in staleNames) frames.Remove(name);
            frames.Remove(oldName);
            frames.Add(newName, frame);
            foreach (var item in frames.Values)
            {
                var c = item.constraints;
                if (c == null) continue;
                RenameReferences(c.centroid?.refFrameNames, oldName, newName);
                RenameReferences(c.inPlane?.refFrameNames, oldName, newName);
                if (c.transform != null && c.transform.refFrame == oldName) c.transform.refFrame = newName;
                if (c.orthogonal != null)
                {
                    if (c.orthogonal.frame_1 == oldName) c.orthogonal.frame_1 = newName;
                    if (c.orthogonal.frame_2 == oldName) c.orthogonal.frame_2 = newName;
                    if (c.orthogonal.frame_3 == oldName) c.orthogonal.frame_3 = newName;
                }
            }
        }

        private static void RenameReferences(List<string> names, string oldName, string newName)
        {
            if (names == null) return;
            for (int i = 0; i < names.Count; i++)
                if (names[i] == oldName) names[i] = newName;
        }
    }

    public sealed class SwasiFrameMetadata
    {
        public SwasiFrameMetadata Clone()
        {
            // Replace initialized collections so default offsets are never
            // prepended to the user's values when an editor makes a working copy.
            return JsonConvert.DeserializeObject<SwasiFrameMetadata>(JsonConvert.SerializeObject(this),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        }

        public SwasiFrameRole role;
        public RefFrameProperties properties = new RefFrameProperties();
        public RefFrameConstraints constraints = new RefFrameConstraints();
        public ConstraintKind constraintKind;
        public bool isConstraintFrame;
        public float inPlaneToleranceMm = 0.01f;
    }
}
