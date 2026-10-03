using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class SwasiMetadataStore
    {
        internal const string PropertyName = "SWASI_Metadata_v1";
        private const int ChunkSize = 900;
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public SwasiDocumentMetadata Load(ModelDoc2 document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var manager = document.Extension.CustomPropertyManager[string.Empty];
            string json = Read(manager, PropertyName);
            if (string.IsNullOrWhiteSpace(json)) return new SwasiDocumentMetadata();
            if (json.StartsWith("gzip:", StringComparison.Ordinal))
            {
                if (!int.TryParse(json.Substring(5), out int count) || count < 1 || count > 999)
                    throw new InvalidOperationException("The SWASI metadata chunk manifest is invalid.");
                var encoded = new StringBuilder();
                for (int i = 0; i < count; i++) encoded.Append(Read(manager, ChunkName(i)));
                json = Decompress(encoded.ToString());
            }
            try
            {
                return JsonConvert.DeserializeObject<SwasiDocumentMetadata>(json, Settings)
                    ?? new SwasiDocumentMetadata();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The SWASI metadata stored in this document is invalid.", ex);
            }
        }

        public void Save(ModelDoc2 document, SwasiDocumentMetadata metadata)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            string json = JsonConvert.SerializeObject(metadata, Formatting.None, Settings);
            var manager = document.Extension.CustomPropertyManager[string.Empty];
            int previousCount = 0;
            string previous = Read(manager, PropertyName);
            if (previous.StartsWith("gzip:", StringComparison.Ordinal)) int.TryParse(previous.Substring(5), out previousCount);
            string encoded = Compress(json);
            int count = (encoded.Length + ChunkSize - 1) / ChunkSize;
            for (int i = 0; i < count; i++)
                Write(manager, ChunkName(i), encoded.Substring(i * ChunkSize, Math.Min(ChunkSize, encoded.Length - i * ChunkSize)));
            Write(manager, PropertyName, "gzip:" + count);
            for (int i = count; i < previousCount; i++) manager.Delete2(ChunkName(i));
        }

        private static string Read(CustomPropertyManager manager, string name)
        {
            string raw, resolved; bool wasResolved;
            manager.Get5(name, false, out raw, out resolved, out wasResolved);
            return string.IsNullOrWhiteSpace(raw) ? resolved ?? string.Empty : raw;
        }
        private static void Write(CustomPropertyManager manager, string name, string value)
        {
            int result = manager.Add3(name, (int)swCustomInfoType_e.swCustomInfoText, value,
                (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            if (result != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
                throw new InvalidOperationException("SolidWorks did not save SWASI metadata property '" + name + "'.");
        }
        private static string ChunkName(int index) => PropertyName + "_" + index.ToString("000");
        private static string Compress(string value)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(value); gzip.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(output.ToArray());
            }
        }
        private static string Decompress(string value)
        {
            byte[] bytes = Convert.FromBase64String(value);
            using (var input = new MemoryStream(bytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8)) return reader.ReadToEnd();
        }
    }
}
