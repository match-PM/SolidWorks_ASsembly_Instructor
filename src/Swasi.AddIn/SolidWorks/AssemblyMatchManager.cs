using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class AssemblyFrameCandidate
    {
        public AssemblyFrameOption Option;
        public Component2 Component;
        public ModelDoc2 Document;
        public Feature Feature;
    }

    internal sealed class AssemblyMatchManager
    {
        private readonly SwasiMetadataStore store = new SwasiMetadataStore();
        public static string Configuration(ModelDoc2 document) => document.ConfigurationManager.ActiveConfiguration.Name;

        public List<AssemblyFrameCandidate> ReadCandidates(ModelDoc2 document, List<string> warnings)
        {
            if (!(document is IAssemblyDoc assembly)) throw new InvalidOperationException("Assembly Matches requires an assembly document.");
            var result = new List<AssemblyFrameCandidate>();
            foreach (var component in ((object[])assembly.GetComponents(true) ?? new object[0]).OfType<Component2>())
            {
                var model = component.GetModelDoc2() as ModelDoc2;
                if (component.IsSuppressed() || model == null)
                { warnings.Add(component.Name2 + ": suppressed or unresolved; resolve it to use its frames."); continue; }
                var metadata = store.Load(model);
                foreach (var feature in SwasiFeatureCatalog.Features(model).Where(f => f.GetTypeName2() == "CoordSys" &&
                    FeatureNameRules.IsReference(f.Name) && !FeatureNameRules.IsOrigin(f.Name)))
                {
                    string name = FeatureNameRules.ReferenceName(feature.Name);
                    if (!metadata.frames.TryGetValue(name, out var frame) ||
                        (frame.role & (SwasiFrameRole.Assembly | SwasiFrameRole.Target)) == 0) continue;
                    var contextualFeature = component.FeatureByName(feature.Name);
                    if (contextualFeature == null || contextualFeature.IsSuppressed()) continue;
                    result.Add(new AssemblyFrameCandidate { Component = component, Document = model, Feature = feature,
                        Option = new AssemblyFrameOption { endpoint = new AssemblyFrameEndpoint { component = component.Name2,
                            configuration = component.ReferencedConfiguration, frame = name }, role = frame.role, isCoordinateSystem = true } });
                }
            }
            return result.OrderBy(c => c.Option.endpoint.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
        }

        public List<AssemblyFrameMatch> ReadMatches(ModelDoc2 document)
        {
            return store.Load(document).assemblyMatches.Where(m => m.configuration == Configuration(document))
                .Select(m => ResolveNames(document, m.Clone())).ToList();
        }

        private AssemblyFrameMatch ResolveNames(ModelDoc2 document, AssemblyFrameMatch match)
        {
            ResolveEndpoint(document, match.assemblyFrame, match.assemblyComponentReference, match.assemblyFrameReference);
            ResolveEndpoint(document, match.targetFrame, match.targetComponentReference, match.targetFrameReference);
            var mate = FindMate(document, match);
            if (mate != null) match.mateName = mate.Name;
            return match;
        }

        private static void ResolveEndpoint(ModelDoc2 assembly, AssemblyFrameEndpoint endpoint, string componentReference, string frameReference)
        {
            if (endpoint == null) return;
            var component = Resolve(assembly, componentReference) as Component2;
            if (component == null) return;
            endpoint.component = component.Name2;
            // Preserve the saved configuration: changing it requires explicit rematching.
            var model = component.GetModelDoc2() as ModelDoc2;
            var feature = model == null ? null : Resolve(model, frameReference) as Feature;
            if (feature != null) endpoint.frame = FeatureNameRules.ReferenceName(feature.Name);
        }

        public string Status(ModelDoc2 document, AssemblyFrameMatch match, IEnumerable<AssemblyFrameCandidate> candidates)
        {
            try { AssemblyMatchRules.Validate(new[] { match }, candidates.Select(c => c.Option)); }
            catch (Exception ex) { return ex.Message; }
            var mate = FindMate(document, match);
            if (mate == null) return "Mate missing — Apply to recreate";
            if (mate.IsSuppressed()) return "Mate suppressed — Apply to restore";
            if (!IsCoordinateMate(mate)) return "Mate no longer aligns axes - Apply to repair";
            return mate.GetErrorCode() == 0 ? "Matched" : "Mate has an error";
        }

        public void Apply(ModelDoc2 document, IReadOnlyList<AssemblyFrameMatch> requested)
        {
            var warnings = new List<string>();
            var candidates = ReadCandidates(document, warnings);
            AssemblyMatchRules.Validate(requested, candidates.Select(c => c.Option));
            var byKey = candidates.ToDictionary(c => c.Option.endpoint.Key, StringComparer.Ordinal);
            var before = store.Load(document);
            var previous = ReadMatches(document);
            var desired = requested.Select(m => m.Clone()).ToList();
            string config = Configuration(document);
            var retained = new HashSet<string>();
            foreach (var match in desired)
            {
                match.configuration = config;
                var old = previous.FirstOrDefault(m => m.id == match.id);
                var mate = old == null ? null : FindMate(document, old);
                if (old != null && old.name == match.name && old.assemblyFrame.Key == match.assemblyFrame.Key &&
                    old.targetFrame.Key == match.targetFrame.Key && mate != null && !mate.IsSuppressed() && IsCoordinateMate(mate) && mate.GetErrorCode() == 0)
                { match.mateName = old.mateName; match.mateReference = old.mateReference; retained.Add(match.id); }
            }
            var transforms = ((object[])((IAssemblyDoc)document).GetComponents(true) ?? new object[0]).OfType<Component2>()
                .Where(c => !c.IsSuppressed()).Select(c => Tuple.Create(c, c.Transform2)).Where(t => t.Item2 != null).ToList();
            var oldErrors = new HashSet<string>(MateFeatures(document).Where(f => f.GetErrorCode() != 0).Select(f => f.Name));
            bool changed = false;
            document.Extension.StartRecordingUndoObject();
            try
            {
                foreach (var old in previous.Where(m => !retained.Contains(m.id)))
                {
                    var mate = FindMate(document, old);
                    if (mate == null) continue;
                    document.ClearSelection2(true);
                    if (!mate.Select2(false, 0)) throw new InvalidOperationException("Could not select mate '" + mate.Name + "'.");
                    changed = true;
                    if (!document.Extension.DeleteSelection2(0)) throw new InvalidOperationException("Could not remove mate '" + mate.Name + "'.");
                }
                foreach (var match in desired)
                {
                    var assembly = byKey[match.assemblyFrame.Key]; var target = byKey[match.targetFrame.Key];
                    match.assemblyComponentReference = Persist(document, assembly.Component);
                    match.targetComponentReference = Persist(document, target.Component);
                    match.assemblyFrameReference = Persist(assembly.Document, assembly.Feature);
                    match.targetFrameReference = Persist(target.Document, target.Feature);
                    if (retained.Contains(match.id)) continue;
                    changed = true;
                    var mate = CreateMate(document, assembly, target);
                    mate.Name = FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + match.name + "_" + match.id.Substring(0, 8);
                    match.mateName = mate.Name;
                    match.mateReference = Persist(document, mate);
                    // A match belongs only to the configuration in which it was applied.
                    if (((string[])document.GetConfigurationNames()).Length > 1)
                    {
                        mate.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null);
                        if (!mate.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null))
                            throw new InvalidOperationException("Could not activate the mate in this configuration.");
                    }
                }
                document.ForceRebuild3(false);
                var errors = MateFeatures(document).Where(f => !f.IsSuppressed() && f.GetErrorCode() != 0 && !oldErrors.Contains(f.Name)).ToList();
                if (errors.Count > 0) throw new InvalidOperationException("The matches conflict with existing mates or fixed components: " + string.Join(", ", errors.Select(f => f.Name)));
                foreach (var match in desired)
                {
                    var mate = FindMate(document, match);
                    if (mate == null || mate.IsSuppressed() || !IsCoordinateMate(mate) || mate.GetErrorCode() != 0)
                        throw new InvalidOperationException("SolidWorks could not solve match '" + match.name + "'.");
                    VerifyAlignment(byKey[match.assemblyFrame.Key], byKey[match.targetFrame.Key]);
                }
                var updated = store.Load(document);
                updated.assemblyMatches.RemoveAll(m => m.configuration == config);
                updated.assemblyMatches.AddRange(desired);
                store.Save(document, updated);
                document.Extension.FinishRecordingUndoObject2("SWASI Assembly Matches", false);
            }
            catch (Exception ex)
            {
                bool recorded = document.Extension.FinishRecordingUndoObject2("SWASI Assembly Matches", false);
                if (changed && recorded) document.EditUndo2(1);
                foreach (var transform in transforms) transform.Item1.Transform2 = transform.Item2;
                store.Save(document, before);
                document.ForceRebuild3(false);
                throw new InvalidOperationException("Assembly Matches could not be applied. " + ex.Message, ex);
            }
            finally { document.ClearSelection2(true); }
        }

        private static Feature CreateMate(ModelDoc2 document, AssemblyFrameCandidate assembly, AssemblyFrameCandidate target)
        {
            document.ClearSelection2(true);
            foreach (var candidate in new[] { target, assembly })
            {
                string selection = candidate.Feature.Name + "@" + candidate.Component.GetSelectByIDString();
                if (!document.Extension.SelectByID2(selection, "COORDSYS", 0, 0, 0, true, 1, null, 0))
                    throw new InvalidOperationException("Could not select coordinate system '" + candidate.Option + "'.");
            }
            var doc = (IAssemblyDoc)document;
            // swMateCOORDINATE is the origin-and-axes mate. An ordinary coincident
            // mate between coordinate systems constrains only their origins.
            // CreateMateData does not expose this mate type; use AddMate5.
            var before = new HashSet<string>(MateFeatures(document).Select(f => f.Name), StringComparer.Ordinal);
            int error;
            var mate = doc.AddMate5((int)swMateType_e.swMateCOORDINATE, (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, 0, out error);
            if (mate == null || error != (int)swAddMateError_e.swAddMateError_NoError)
                throw new InvalidOperationException("SolidWorks could not create the coordinate-system mate (error " + error + "). Check fixed components and conflicting mates.");
            var feature = MateFeatures(document).SingleOrDefault(f => !before.Contains(f.Name));
            if (feature == null) throw new InvalidOperationException("The created coordinate mate was not found in the feature tree.");
            return feature;
        }

        private static void VerifyAlignment(AssemblyFrameCandidate assembly, AssemblyFrameCandidate target)
        {
            var a = WorldPose(assembly); var b = WorldPose(target);
            double distance = (a.translation - b.translation).Length();
            double xError = (Vector3d.Transform(Vector3d.UnitX, a.rotation) - Vector3d.Transform(Vector3d.UnitX, b.rotation)).Length();
            double yError = (Vector3d.Transform(Vector3d.UnitY, a.rotation) - Vector3d.Transform(Vector3d.UnitY, b.rotation)).Length();
            if (distance > 1e-5 || xError > 1e-7 || yError > 1e-7)
                throw new InvalidOperationException($"The mate did not align both frame origins and axes (origin error {distance:G6} mm; axis errors {xError:G6}, {yError:G6}). Check fixed components and existing mates.");
        }

        private static PrecisePose WorldPose(AssemblyFrameCandidate candidate) => new PreciseConstraintEngine().Transform(
            PrecisePose.FromCadArray((double[])candidate.Component.Transform2.ArrayData),
            SwasiFeatureCatalog.ReadPreciseCoordinateSystemPose(candidate.Document, candidate.Feature));

        public List<AssemblyFrameMatch> ForExport(ModelDoc2 document)
        {
            var matches = ReadMatches(document);
            if (matches.Count == 0) return matches;
            var candidates = ReadCandidates(document, new List<string>());
            AssemblyMatchRules.Validate(matches, candidates.Select(c => c.Option));
            foreach (var match in matches)
            {
                if (Status(document, match, candidates) != "Matched")
                    throw new InvalidOperationException("Assembly match '" + match.name + "' is not active. Open Assembly Matches to repair or remove it.");
                VerifyAlignment(candidates.Single(c => c.Option.endpoint.Key == match.assemblyFrame.Key),
                    candidates.Single(c => c.Option.endpoint.Key == match.targetFrame.Key));
            }
            return matches;
        }

        internal static IEnumerable<Feature> MateFeatures(ModelDoc2 document) =>
            SwasiFeatureCatalog.Features(document).Where(f => f.GetTypeName2().StartsWith("Mate", StringComparison.Ordinal) && f.GetSpecificFeature2() is Mate2);
        private static bool IsCoordinateMate(Feature feature) =>
            feature.GetSpecificFeature2() is Mate2 mate && mate.Type == (int)swMateType_e.swMateCOORDINATE;
        private static Feature FindMate(ModelDoc2 document, AssemblyFrameMatch match)
        {
            var feature = Resolve(document, match.mateReference) as Feature;
            if (feature == null && string.IsNullOrEmpty(match.mateReference)) feature = MateFeatures(document).FirstOrDefault(f => f.Name == match.mateName);
            return feature != null && feature.GetSpecificFeature2() is Mate2 ? feature : null;
        }
        private static string Persist(ModelDoc2 document, object value) => Convert.ToBase64String((byte[])document.Extension.GetPersistReference3(value));
        private static object Resolve(ModelDoc2 document, string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            try { int error; return document.Extension.GetObjectByPersistReference3(Convert.FromBase64String(reference), out error); }
            catch (FormatException) { return null; }
        }
    }
}
