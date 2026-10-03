using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>Fresh CAD geometry wins; authored constraints and persistent identity survive re-export.</summary>
    public sealed class ExportMergePolicy
    {
        public JObject Merge(JObject generated, JObject existing)
        {
            return Merge(generated, existing, null);
        }

        public JObject Merge(JObject generated, JObject existing, ISet<string> authoritativeFrameNames)
        {
            return Merge(generated, existing, authoritativeFrameNames, authoritativeFrameNames, false);
        }

        public JObject Merge(JObject generated, JObject existing,
            ISet<string> authoritativePropertyFrameNames,
            ISet<string> authoritativeConstraintFrameNames,
            bool authoritativeColor)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            var result = (JObject)generated.DeepClone();
            if (existing == null) return RemoveEmptyConstraints(result);
            CopyIfPresent(existing, result, "guid");
            CopyIfPresent(existing, result, "saveDate");

            var frames = result.SelectToken("mountingDescription.mountingReferences.ref_frames") as JArray;
            var oldFrames = existing.SelectToken("mountingDescription.mountingReferences.ref_frames") as JArray;
            if (frames != null && oldFrames != null)
            {
                foreach (var oldFrame in oldFrames.OfType<JObject>())
                {
                    string name = (string)oldFrame["name"];
                    if (string.IsNullOrEmpty(name)) continue;
                    var frame = frames.OfType<JObject>().FirstOrDefault(f => (string)f["name"] == name);
                    if (frame == null)
                        frames.Add(oldFrame.DeepClone()); // Retain manually authored reference frames.
                    else
                    {
                        if (authoritativeConstraintFrameNames == null || !authoritativeConstraintFrameNames.Contains(name))
                            CopyIfPresent(oldFrame, frame, "constraints");
                        if (authoritativePropertyFrameNames == null || !authoritativePropertyFrameNames.Contains(name))
                            CopyIfPresent(oldFrame, frame, "properties");
                    }
                }
            }

            var components = result.SelectToken("mountingDescription.components") as JArray;
            var oldComponents = existing.SelectToken("mountingDescription.components") as JArray;
            if (components != null && oldComponents != null)
                foreach (var component in components.OfType<JObject>())
                {
                    string name = (string)component["name"];
                    var oldComponent = oldComponents.OfType<JObject>().FirstOrDefault(c => (string)c["name"] == name);
                    if (oldComponent != null) CopyIfPresent(oldComponent, component, "guid");
                }
            if (!authoritativeColor) CopyIfPresent(existing, result, "color");
            return RemoveEmptyConstraints(result);
        }

        private static JObject RemoveEmptyConstraints(JObject document)
        {
            var frames = document.SelectToken("mountingDescription.mountingReferences.ref_frames") as JArray;
            if (frames == null) return document;
            foreach (var frame in frames.OfType<JObject>())
            {
                var constraints = frame["constraints"] as JObject;
                if (constraints == null) continue;
                RemoveIf(constraints, "centroid", IsEmptyCentroid);
                RemoveIf(constraints, "orthogonal", IsEmptyOrthogonal);
                RemoveIf(constraints, "inPlane", IsEmptyReferenceListConstraint);
                RemoveIf(constraints, "transform", IsEmptyTransform);
                if (!constraints.Properties().Any()) frame.Remove("constraints");
            }
            return document;
        }

        private static void RemoveIf(JObject constraints, string name, Func<JObject, bool> isEmpty)
        {
            var value = constraints[name] as JObject;
            if (value != null && isEmpty(value)) constraints.Remove(name);
        }

        private static bool IsEmptyCentroid(JObject value)
        {
            return IsEmptyArray(value["refFrameNames"]) &&
                string.IsNullOrWhiteSpace((string)value["dim"]) &&
                IsNullOrZeroArray(value["offsetValues"]);
        }

        private static bool IsEmptyOrthogonal(JObject value)
        {
            return string.IsNullOrWhiteSpace((string)value["frame_1"]) &&
                string.IsNullOrWhiteSpace((string)value["frame_2"]) &&
                string.IsNullOrWhiteSpace((string)value["frame_3"]);
        }

        private static bool IsEmptyReferenceListConstraint(JObject value) =>
            IsEmptyArray(value["refFrameNames"]);

        private static bool IsEmptyTransform(JObject value) =>
            string.IsNullOrWhiteSpace((string)value["refFrame"]);

        private static bool IsEmptyArray(JToken value) =>
            value == null || value.Type == JTokenType.Null || value is JArray array && array.Count == 0;

        private static bool IsNullOrZeroArray(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            var array = value as JArray;
            return array != null && array.All(item => item.Type == JTokenType.Integer ||
                item.Type == JTokenType.Float ? (double)item == 0 : false);
        }

        private static void CopyIfPresent(JObject source, JObject target, string property)
        {
            var value = source[property];
            if (value != null && value.Type != JTokenType.Null) target[property] = value.DeepClone();
        }
    }
}
