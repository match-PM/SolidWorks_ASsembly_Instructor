using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>Fresh CAD geometry wins; authored constraints and persistent identity survive re-export.</summary>
    public sealed class ExportMergePolicy
    {
        public JObject Merge(JObject generated, JObject existing)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            var result = (JObject)generated.DeepClone();
            if (existing == null) return result;
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
                        CopyIfPresent(oldFrame, frame, "constraints");
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
            return result;
        }

        private static void CopyIfPresent(JObject source, JObject target, string property)
        {
            var value = source[property];
            if (value != null && value.Type != JTokenType.Null) target[property] = value.DeepClone();
        }
    }
}
