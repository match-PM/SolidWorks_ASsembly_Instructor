using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    public sealed class ConstraintFrameImportResult
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public override string ToString() => "Created " + Created.Count + " constrained frame(s); skipped " + Skipped.Count + "." +
            Environment.NewLine + string.Join(Environment.NewLine, Created.Select(n => "Created: " + n).Concat(Skipped.Select(n => "Skipped: " + n)));
    }

    public static class ConstraintFrameImporter
    {
        private sealed class Candidate
        {
            public string Name;
            public SwasiFrameMetadata Metadata;
            public List<string> Dependencies;
        }

        public static IReadOnlyList<string> ReadFrameNames(string json, string currentSpawn)
        {
            return ReadRows(json, currentSpawn).OfType<JObject>()
                .Where(row => row["constraints"] is JObject constraints && constraints.Properties().Any())
                .Select(row => (string)row["name"] ?? "<unnamed>").Distinct(StringComparer.Ordinal).ToList();
        }

        private static JArray ReadRows(string json, string currentSpawn)
        {
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var references = root.SelectToken("mountingDescription.mountingReferences") as JObject;
            var spawn = references?["spawningOrigin"];
            if (currentSpawn == null || spawn?.Type != JTokenType.String || (string)spawn != currentSpawn)
                throw new InvalidOperationException("Spawn name mismatch. JSON: '" + (spawn?.ToString() ?? "<missing>") +
                    "'; current document: '" + (currentSpawn ?? "<missing>") + "'. No frames were imported.");
            string units = (string)root["documentUnits"];
            if (units != null && units != "mm")
                throw new InvalidOperationException("Constraint import requires documentUnits 'mm'. No frames were imported.");
            var rows = references["ref_frames"] as JArray;
            if (rows == null) throw new InvalidOperationException("JSON must contain mountingDescription.mountingReferences.ref_frames.");
            return rows;
        }

        // The callback must either create the frame successfully or throw and roll it back.
        public static ConstraintFrameImportResult Import(string json, string currentSpawn,
            IEnumerable<string> existingFrames, Action<string, SwasiFrameMetadata> create,
            IEnumerable<string> selectedFrames = null)
        {
            var rows = ReadRows(json, currentSpawn);
            var selected = selectedFrames == null ? null : new HashSet<string>(selectedFrames, StringComparer.Ordinal);
            var result = new ConstraintFrameImportResult();
            var available = new HashSet<string>(existingFrames, StringComparer.Ordinal);
            var occupied = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
            var duplicateNames = new HashSet<string>(rows.OfType<JObject>().Select(r => (string)r["name"])
                .Where(n => n != null).GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            var pending = new List<Candidate>();
            foreach (var row in rows)
            {
                string name = "<unnamed>";
                try
                {
                    var obj = row as JObject ?? throw new InvalidOperationException("Frame entry must be an object.");
                    name = (string)obj["name"] ?? name;
                    if (selected != null && !selected.Contains(name)) continue;
                    var raw = obj["constraints"] as JObject;
                    if (raw == null || !raw.Properties().Any()) continue;
                    if (duplicateNames.Contains(name)) throw new InvalidOperationException("Duplicate frame name in JSON.");
                    if (occupied.Contains(name)) throw new InvalidOperationException("Already exists; kept unchanged.");
                    if (string.IsNullOrWhiteSpace(name) || name == "<unnamed>") throw new InvalidOperationException("Missing frame name.");
                    string geometry = (string)obj["type"];
                    if (geometry != null && !geometry.Equals("frame", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Only coordinate frames can be imported.");
                    var frame = obj.ToObject<RefFrameDescription>(JsonSerializer.Create(new JsonSerializerSettings {
                        ObjectCreationHandling = ObjectCreationHandling.Replace }));
                    var c = frame.constraints;
                    var kinds = new List<ConstraintKind>();
                    if (c.ShouldSerializecentroid()) kinds.Add(ConstraintKind.Centroid);
                    if (c.ShouldSerializeorthogonal()) kinds.Add(ConstraintKind.Orthogonal);
                    if (c.ShouldSerializetransform()) kinds.Add(ConstraintKind.Transform);
                    if (kinds.Count != 1) throw new InvalidOperationException("Expected exactly one complete centroid, orthogonal, or transform constraint.");
                    var deps = new List<string>();
                    switch (kinds[0])
                    {
                        case ConstraintKind.Centroid:
                            deps.AddRange(c.centroid.refFrameNames);
                            if (c.centroid.offsetValues == null || c.centroid.offsetValues.Count != 3 ||
                                c.centroid.offsetValues.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                                throw new InvalidOperationException("Centroid requires three finite offsetValues.");
                            break;
                        case ConstraintKind.Orthogonal:
                            deps.AddRange(new[] { c.orthogonal.frame_1, c.orthogonal.frame_2, c.orthogonal.frame_3 });
                            if (c.orthogonal.unit_distance_from_f1 != "%" && c.orthogonal.unit_distance_from_f1 != "mm")
                                throw new InvalidOperationException("Orthogonal distance unit must be '%' or 'mm'.");
                            break;
                        case ConstraintKind.Transform:
                            deps.Add(c.transform.refFrame);
                            if (c.transform.transform == null) throw new InvalidOperationException("Missing transform.");
                            break;
                    }
                    if (c.ShouldSerializeinPlane()) deps.AddRange(c.inPlane.refFrameNames.Where(n => n != name));
                    if (deps.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Empty dependency name.");
                    var properties = frame.properties ?? new RefFrameProperties();
                    var role = SwasiFrameRole.None;
                    if (properties.visionProperties?.isVisionFrame == true) role |= SwasiFrameRole.Vision;
                    if (properties.laserProperties?.isLaserFrame == true) role |= SwasiFrameRole.Laser;
                    if (properties.grippingProperties?.isGrippingFrame == true) role |= SwasiFrameRole.Gripping;
                    if (properties.gluePtProperties?.isGlueFrame == true) role |= SwasiFrameRole.Glue;
                    if (properties.assemblyProperties?.isAssemblyFrame == true) role |= SwasiFrameRole.Assembly;
                    if (properties.assemblyProperties?.isTargetFrame == true) role |= SwasiFrameRole.Target;
                    // Associations belong to the parent assembly, never to a component frame.
                    if (properties.assemblyProperties != null)
                    { properties.assemblyProperties.associatedFrame = ""; properties.assemblyProperties.associatedComponent = null; }
                    pending.Add(new Candidate { Name = name, Dependencies = deps, Metadata = new SwasiFrameMetadata {
                        isConstraintFrame = true, constraintKind = kinds[0], constraints = c, properties = properties, role = role } });
                }
                catch (Exception ex) { result.Skipped.Add(name + ": " + ex.Message); }
            }
            while (pending.Count > 0)
            {
                var ready = pending.Where(f => f.Dependencies.All(available.Contains)).ToList();
                if (ready.Count == 0) break;
                foreach (var frame in ready)
                {
                    pending.Remove(frame);
                    try
                    {
                        create(frame.Name, frame.Metadata);
                        available.Add(frame.Name);
                        result.Created.Add(frame.Name);
                    }
                    catch (Exception ex) { result.Skipped.Add(frame.Name + ": " + ex.Message); }
                }
            }
            foreach (var frame in pending)
                result.Skipped.Add(frame.Name + ": Missing, failed, or cyclic dependencies: " +
                    string.Join(", ", frame.Dependencies.Where(n => !available.Contains(n)).Distinct()));
            return result;
        }
    }
}
