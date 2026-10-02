using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class ExportCoordinator
    {
        private readonly SldWorks app;
        private readonly string componentsPath;
        private readonly string assembliesPath;
        private readonly JsonExportStore store = new JsonExportStore(new ExportMergePolicy());
        private readonly Action<string, string> log;
        public ExportCoordinator(SldWorks app, string componentsPath, string assembliesPath, Action<string, string> log)
        { this.app = app; this.componentsPath = componentsPath; this.assembliesPath = assembliesPath; this.log = log; }

        public ExportResult Run()
        {
            var result = new ExportResult();
            try
            {
                var active = app.IActiveDoc2 as ModelDoc2;
                if (active == null) throw new InvalidOperationException("No active document found.");
                int type = active.GetType();
                if (type != (int)swDocumentTypes_e.swDocPART && type != (int)swDocumentTypes_e.swDocASSEMBLY)
                    throw new InvalidOperationException("Open a part or assembly before exporting.");
                Directory.CreateDirectory(componentsPath);
                Directory.CreateDirectory(assembliesPath);
                using (var session = new DocumentSession(app, active, (message, level) =>
                {
                    log(message, level);
                    if (level.Equals("error", StringComparison.OrdinalIgnoreCase)) result.Add("Document cleanup", ExportStatus.Failed, message);
                }))
                {
                    var documents = CollectDocuments(active, result);
                    var identities = BuildIdentities(documents, active);
                    foreach (var document in documents)
                        ExportDocument(document, active, session, identities, result);
                }
            }
            catch (Exception ex)
            {
                result.Add("Export", ExportStatus.Failed, ex.Message);
                log(ex.ToString(), "error");
            }
            return result;
        }

        private List<ModelDoc2> CollectDocuments(ModelDoc2 active, ExportResult result)
        {
            var documents = new List<ModelDoc2> { active };
            if (active is IAssemblyDoc assembly)
                foreach (var component in ((object[])assembly.GetComponents(true) ?? new object[0]).OfType<Component2>())
                {
                    var document = component.GetModelDoc2() as ModelDoc2;
                    if (document == null)
                        result.Add(component.Name2, ExportStatus.Skipped, "Component is suppressed or unavailable.");
                    else if (!documents.Contains(document)) documents.Add(document);
                }
            return documents;
        }

        private Dictionary<string, Guid> BuildIdentities(IEnumerable<ModelDoc2> documents, ModelDoc2 active)
        {
            var identities = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents)
            {
                string name = GetName(document);
                if (identities.ContainsKey(name)) throw new InvalidOperationException($"Multiple documents have export name '{name}'. Rename them to avoid overwriting files.");
                string folder = document == active && document.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY ? assembliesPath : componentsPath;
                var existing = store.Read(Path.Combine(folder, name + ".json"));
                Guid id;
                identities.Add(name, Guid.TryParse((string)existing?["guid"], out id) ? id : Guid.NewGuid());
            }
            return identities;
        }

        private void ExportDocument(ModelDoc2 document, ModelDoc2 active, DocumentSession session,
            Dictionary<string, Guid> identities, ExportResult result)
        {
            string name = GetName(document);
            var extractionErrors = new List<string>();
            Action<string, string> report = (message, level) =>
            {
                log(message, level);
                if (level.Equals("error", StringComparison.OrdinalIgnoreCase)) extractionErrors.Add(message);
            };
            try
            {
                var references = new ReferenceFeatureExtractor(report);
                var assemblies = new AssemblyExtractor(references, report);
                var features = (object[])document.FeatureManager.GetFeatures(false) ?? new object[0];
                var origin = references.ExtractOrigin(document, features);
                if (!origin.Item1)
                {
                    result.Add(name, ExportStatus.Skipped, "No unique valid SWASI origin.");
                    return;
                }
                var mounting = new MountingDescription();
                var relative = origin.Item2.GetInverted4x4Matrix();
                mounting.mountingReferences.spawningOrigin = origin.Item2.name;
                mounting.mountingReferences.ref_planes.AddRange(references.ExtractRefPlanes(document, features, relative));
                mounting.mountingReferences.ref_frames.AddRange(references.ExtractRefPoints(features, relative));
                mounting.mountingReferences.ref_frames.AddRange(references.ExtractRefFrames(document, features, relative));
                mounting.mountingReferences.ref_axes.AddRange(references.ExtractRefAxes(document, features));

                bool isAssembly = document.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
                bool isMainAssembly = isAssembly && document == active;
                object model;
                string folder = isMainAssembly ? assembliesPath : componentsPath;
                if (isMainAssembly)
                {
                    assemblies.ExtractAssemblyComponents(mounting, document, relative);
                    assemblies.ExtractAssemblyMates(mounting, features);
                    foreach (var component in mounting.components)
                    {
                        string componentName = component.name;
                        int dash = componentName.LastIndexOf('-');
                        if (dash >= 0) componentName = componentName.Substring(0, dash);
                        if (identities.TryGetValue(componentName, out var id)) component.guid = id.ToString();
                    }
                    model = new AssemblyDescription { name = name, guid = identities[name], mountingDescription = mounting, cadPath = name + ".STL" };
                }
                else
                    model = new ComponentDescription { name = name, guid = identities[name], mountingDescription = mounting, cadPath = name + ".STL", type = isAssembly ? "Assembly" : "Component" };

                if (extractionErrors.Count > 0) throw new InvalidOperationException(string.Join(System.Environment.NewLine, extractionErrors));
                session.Activate(document);
                new StlExporter(app).Export(document, Path.Combine(folder, name + ".STL"), origin.Item2.name);
                store.Save(Path.Combine(folder, name + ".json"), model);
                result.Add(name, ExportStatus.Succeeded, "JSON and STL exported.");
                log($"Exported {name}.", "log");
            }
            catch (Exception ex)
            {
                result.Add(name, ExportStatus.Failed, ex.Message);
                log($"{name}: {ex}", "error");
            }
        }

        private static string GetName(ModelDoc2 document)
        {
            string title = document.GetTitle();
            string extension = Path.GetExtension(title);
            return extension.Equals(".sldprt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".sldasm", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(title) : title;
        }
    }
}
