using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace SolidWorks_ASsembly_Instructor
{
    public sealed class JsonExportStore
    {
        private readonly ExportMergePolicy mergePolicy;
        public JsonExportStore(ExportMergePolicy mergePolicy) { this.mergePolicy = mergePolicy; }
        public JObject Read(string path) => File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;

        public void Save(string path, object model, ISet<string> authoritativePropertyFrameNames = null,
            ISet<string> authoritativeConstraintFrameNames = null, bool authoritativeColor = false)
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var output = mergePolicy.Merge(JObject.FromObject(model, serializer), Read(path),
                authoritativePropertyFrameNames, authoritativeConstraintFrameNames, authoritativeColor);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, output.ToString(Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
