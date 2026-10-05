using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SolidWorks_ASsembly_Instructor
{
    public sealed class AssemblyFrameEndpoint
    {
        public string component;
        public string configuration;
        public string frame;
        [JsonIgnore] public string Key => component + "\n" + configuration + "\n" + frame;
        public AssemblyFrameEndpoint Clone() => new AssemblyFrameEndpoint { component = component, configuration = configuration, frame = frame };
        public override string ToString() => component + " / " + frame + " [" + configuration + "]";
    }

    public sealed class AssemblyFrameMatch
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name;
        public string configuration;
        public AssemblyFrameEndpoint assemblyFrame;
        public AssemblyFrameEndpoint targetFrame;
        public string mateName;
        public string mateReference;
        public string assemblyComponentReference;
        public string targetComponentReference;
        public string assemblyFrameReference;
        public string targetFrameReference;
        public AssemblyFrameMatch Clone() => JsonConvert.DeserializeObject<AssemblyFrameMatch>(JsonConvert.SerializeObject(this));
    }

    public sealed class AssemblyFrameOption
    {
        public AssemblyFrameEndpoint endpoint;
        public SwasiFrameRole role;
        public bool isCoordinateSystem;
        public override string ToString() => endpoint.ToString();
    }

    public static class AssemblyMatchRules
    {
        public static bool CanAssignRole(string geometryType, SwasiFrameRole role) =>
            (role & (SwasiFrameRole.Assembly | SwasiFrameRole.Target)) == 0 || geometryType == "Frame";

        public static void Validate(IReadOnlyList<AssemblyFrameMatch> matches, IEnumerable<AssemblyFrameOption> options)
        {
            var available = options.ToDictionary(o => o.endpoint.Key, StringComparer.Ordinal);
            var targets = new HashSet<string>(StringComparer.Ordinal);
            var assemblies = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var match in matches)
            {
                if (match == null || !Guid.TryParseExact(match.id, "N", out var id))
                    throw new InvalidOperationException("A saved match has an invalid identifier. Remove it and add a new match.");
                if (string.IsNullOrWhiteSpace(match.name) || match.name.IndexOfAny(new[] { '@', '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                    throw new InvalidOperationException("Enter a valid name for every match (without path or SolidWorks special characters).");
                if (!names.Add(match.name) || !ids.Add(match.id)) throw new InvalidOperationException("Match names must be unique.");
                ValidateEndpoint(match.assemblyFrame, SwasiFrameRole.Assembly, available);
                ValidateEndpoint(match.targetFrame, SwasiFrameRole.Target, available);
                if (match.assemblyFrame.component == match.targetFrame.component)
                    throw new InvalidOperationException("Match frames on two different component instances.");
                if (!targets.Add(match.targetFrame.Key)) throw new InvalidOperationException("Each target frame can be matched only once.");
                if (!assemblies.Add(match.assemblyFrame.Key)) throw new InvalidOperationException("Each assembly frame can be matched only once.");
            }
        }

        private static void ValidateEndpoint(AssemblyFrameEndpoint endpoint, SwasiFrameRole role, Dictionary<string, AssemblyFrameOption> available)
        {
            if (endpoint == null || !available.TryGetValue(endpoint.Key, out var option) || !option.isCoordinateSystem || !option.role.HasFlag(role))
                throw new InvalidOperationException("Select an available " + role + " coordinate frame. Missing, suppressed, nested components and points cannot be matched.");
        }

        public static void ApplyExport(MountingDescription mounting, IEnumerable<AssemblyFrameMatch> matches)
        {
            mounting.assemblyConstraints.RemoveAll(c => c.type == "FrameMatch");
            foreach (var component in mounting.components) component.frameProperties.Clear();
            foreach (var match in matches)
            {
                var assembly = mounting.components.SingleOrDefault(c => c.name == match.assemblyFrame.component);
                var target = mounting.components.SingleOrDefault(c => c.name == match.targetFrame.component);
                if (assembly == null || target == null)
                    throw new InvalidOperationException("Match '" + match.name + "' references a component excluded from export. Check its SWASI origin.");
                mounting.assemblyConstraints.Add(new AssemblyConstraintDescription { name = match.mateName, type = "FrameMatch",
                    component_1 = assembly.name, component_2 = target.name,
                    assemblyFrame = match.assemblyFrame.Clone(), targetFrame = match.targetFrame.Clone() });
                AddAssociation(assembly, match.assemblyFrame.frame, match.targetFrame, false);
                AddAssociation(target, match.targetFrame.frame, match.assemblyFrame, true);
            }
        }

        public static bool IdentifyComponent1MovingPart(IReadOnlyList<AssemblyComponentDescription> components, string component1, string component2)
        {
            var first = components.FirstOrDefault(c => c.name == component1)
                ?? components.FirstOrDefault(c => c.name.Replace(FeatureNameRules.Prefix, "") == component1);
            var second = components.FirstOrDefault(c => c.name == component2)
                ?? components.FirstOrDefault(c => c.name.Replace(FeatureNameRules.Prefix, "") == component2);
            if (first == null || second == null) throw new InvalidOperationException("Cannot determine the moving component: a component is missing from export.");
            return first.transformation.translation.Z >= second.transformation.translation.Z;
        }

        private static void AddAssociation(AssemblyComponentDescription component, string frame, AssemblyFrameEndpoint counterpart, bool target)
        {
            component.frameProperties[frame] = new RefFrameProperties { assemblyProperties = new AssemblyProperties {
                isAssemblyFrame = !target, isTargetFrame = target,
                associatedFrame = counterpart.frame, associatedComponent = counterpart.component } };
        }
    }
}
