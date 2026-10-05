using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks_ASsembly_Instructor;

// Dependency-free regression runner: exits nonzero on failure, usable without SolidWorks.
internal static class Program
{
    private static int failures;
    private static int total;
    private static readonly ExportMergePolicy Policy = new ExportMergePolicy();
    [STAThread]
    private static void Main()
    {
        Test("Assembly match dropdowns reserve pending selections and release changed or removed matches", () =>
        {
            using (var grid = new System.Windows.Forms.DataGridView { AllowUserToAddRows = false })
            {
                grid.Columns.Add("Name", "Name");
                foreach (string column in new[] { "Assembly", "Target" })
                {
                    var combo = new System.Windows.Forms.DataGridViewComboBoxColumn { Name = column };
                    combo.Items.AddRange("", "A1", "A2", "A3", "T1", "T2", "Missing");
                    grid.Columns.Add(combo);
                }
                grid.Rows.Add("Saved", "A1", "T1");
                grid.Rows.Add("New", "", "");
                new AssemblyMatchChoices(grid, new[] { "A1", "A2", "A3" }, new[] { "T1", "T2" });
                Func<int, int, string, bool> offers = (row, column, value) =>
                    ((System.Windows.Forms.DataGridViewComboBoxCell)grid.Rows[row].Cells[column]).Items.Contains(value);
                Check(offers(0, 1, "A1")); Check(offers(0, 2, "T1"));
                Check(!offers(1, 1, "A1")); Check(!offers(1, 2, "T1"));
                grid.Rows[1].Cells[1].Value = "A2"; grid.Rows[1].Cells[2].Value = "T2";
                Check(!offers(0, 1, "A2")); Check(!offers(0, 2, "T2"));
                grid.Rows[0].Cells[1].Value = "A3";
                Check(offers(1, 1, "A1")); Check(!offers(1, 1, "A3"));
                grid.Rows[0].Cells[2].Value = "";
                Check(offers(1, 2, "T1"));
                grid.Rows.Add("Third", "", "");
                Check(!offers(2, 1, "A2")); Check(!offers(2, 1, "A3")); Check(!offers(2, 2, "T2"));
                grid.Rows.RemoveAt(1);
                Check(offers(1, 1, "A2")); Check(offers(1, 2, "T2"));
                grid.Rows[0].Cells[1].Value = "Missing";
                Check(offers(0, 1, "Missing")); Check(!offers(1, 1, "Missing"));
                Equal("Missing", (string)grid.Rows[0].Cells[1].Value);
            }
        });
        Test("Assembly matches enforce oriented frames, instance identity and one-to-one endpoints", () =>
        {
            Check(!AssemblyMatchRules.CanAssignRole("Point", SwasiFrameRole.Target));
            Check(!AssemblyMatchRules.CanAssignRole("Point", SwasiFrameRole.Assembly));
            Check(AssemblyMatchRules.CanAssignRole("Point", SwasiFrameRole.Vision));
            Check(AssemblyMatchRules.CanAssignRole("Frame", SwasiFrameRole.Target));
            var a = new AssemblyFrameEndpoint { component = "Part-1", configuration = "Default", frame = "Mount" };
            var b = new AssemblyFrameEndpoint { component = "Part-2", configuration = "Default", frame = "Target" };
            var c = new AssemblyFrameEndpoint { component = "Part-3", configuration = "Default", frame = "Mount" };
            var options = new[] { new AssemblyFrameOption { endpoint=a, role=SwasiFrameRole.Assembly, isCoordinateSystem=true },
                new AssemblyFrameOption { endpoint=b, role=SwasiFrameRole.Target, isCoordinateSystem=true },
                new AssemblyFrameOption { endpoint=c, role=SwasiFrameRole.Assembly, isCoordinateSystem=true } };
            var match = new AssemblyFrameMatch { name="Match", assemblyFrame=a, targetFrame=b };
            AssemblyMatchRules.Validate(new[] {match}, options);
            var duplicate = new AssemblyFrameMatch { name="Other", assemblyFrame=c, targetFrame=b };
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {match,duplicate},options));
            options[0].isCoordinateSystem=false;
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {match},options));
            options[0].isCoordinateSystem=true;
            options[1].role=SwasiFrameRole.Assembly;
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {match},options));
            options[1].role=SwasiFrameRole.Target;
            var missing=match.Clone(); missing.targetFrame.component="Deleted-1";
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {missing},options));
            var wrongConfig=match.Clone(); wrongConfig.targetFrame.configuration="Other";
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {wrongConfig},options));
            var same=match.Clone(); same.targetFrame=a;
            options[0].role |= SwasiFrameRole.Target;
            Throws<InvalidOperationException>(() => AssemblyMatchRules.Validate(new[] {same},options));
        });
        Test("Frame matches export beside plane constraints with instance-specific properties", () =>
        {
            var mounting = new MountingDescription();
            mounting.components.Add(new AssemblyComponentDescription { name="Part-1" });
            mounting.components.Add(new AssemblyComponentDescription { name="Part-2" });
            mounting.components.Add(new AssemblyComponentDescription { name="Part-3" });
            mounting.AddComponentsAssemblyConstraint("Part-1","Part-3");
            var match = new AssemblyFrameMatch { name="Mount", mateName="(auto)SWASI_Mount", configuration="Default",
                assemblyFrame=new AssemblyFrameEndpoint {component="Part-2",configuration="Default",frame="Mount"},
                targetFrame=new AssemblyFrameEndpoint {component="Part-1",configuration="Default",frame="Target"},
                mateReference="internal-persistent-reference" };
            string before=JsonConvert.SerializeObject(match);
            AssemblyMatchRules.ApplyExport(mounting,new[] {match});
            var generated=JObject.FromObject(new {mountingDescription=mounting});
            Equal(2, ((JArray)generated.SelectToken("mountingDescription.assemblyConstraints")).Count);
            Equal("PlaneMatch", (string)generated.SelectToken("mountingDescription.assemblyConstraints[0].type"));
            Equal("FrameMatch", (string)generated.SelectToken("mountingDescription.assemblyConstraints[1].type"));
            Equal("Part-2", (string)generated.SelectToken("mountingDescription.assemblyConstraints[1].component_1"));
            Equal("Part-1", (string)generated.SelectToken("mountingDescription.assemblyConstraints[1].component_2"));
            Check(generated.SelectToken("mountingDescription.assemblyConstraints[1].move_component_1") == null);
            Check(generated.SelectToken("mountingDescription.assemblyConstraints[1].description") == null);
            Check(generated.SelectToken("mountingDescription.assemblyConstraints[0].assemblyFrame") == null);
            Check(generated.SelectToken("mountingDescription.frameMatches") == null);
            Equal("Part-2", (string)generated.SelectToken("mountingDescription.components[0].frameProperties.Target.assemblyProperties.associatedComponent"));
            Equal("Mount", (string)generated.SelectToken("mountingDescription.components[0].frameProperties.Target.assemblyProperties.associatedFrame"));
            Check(generated.SelectToken("mountingDescription.components[2].frameProperties") == null);
            Check(!generated.ToString().Contains("internal-persistent-reference"));
            Equal(before,JsonConvert.SerializeObject(match));
            var metadata = new SwasiDocumentMetadata(); metadata.assemblyMatches.Add(match);
            var loaded=JsonConvert.DeserializeObject<SwasiDocumentMetadata>(JsonConvert.SerializeObject(metadata));
            Equal(match.id,loaded.assemblyMatches.Single().id);
            Equal("internal-persistent-reference",loaded.assemblyMatches.Single().mateReference);
            AssemblyMatchRules.ApplyExport(mounting,new AssemblyFrameMatch[0]);
            // Old exports used a separate list; it must not survive the next export.
            generated["mountingDescription"]["frameMatches"] = new JArray(new JObject { ["type"]="coincidentFrames" });
            var merged=Policy.Merge(JObject.FromObject(new {mountingDescription=mounting}),generated);
            Check(merged.SelectToken("mountingDescription.frameMatches") == null);
            Check(merged.SelectToken("mountingDescription.components[0].frameProperties") == null);
            Equal(1,((JArray)merged.SelectToken("mountingDescription.assemblyConstraints")).Count);
        });
        Test("Frame match export uses frame roles and omits movement while preserving plane movement", () =>
        {
            var mounting = new MountingDescription();
            var assembly = new AssemblyComponentDescription { name="Assembly-1" };
            var target = new AssemblyComponentDescription { name="Target-1" };
            mounting.components.Add(assembly); mounting.components.Add(target);
            var match = new AssemblyFrameMatch { name="Match", mateName="(auto)SWASI_Match",
                assemblyFrame=new AssemblyFrameEndpoint { component=assembly.name, frame="Mount" },
                targetFrame=new AssemblyFrameEndpoint { component=target.name, frame="Target" } };
            foreach (float z in new[] {-100f, 0f, 100f})
            {
                assembly.transformation.translation.Z=z;
                AssemblyMatchRules.ApplyExport(mounting,new[] {match});
                var frameJson=JObject.FromObject(mounting.assemblyConstraints.Single());
                Equal("Assembly-1",(string)frameJson["component_1"]);
                Equal("Target-1",(string)frameJson["component_2"]);
                Check(frameJson["move_component_1"] == null);
                Check(frameJson["moveComponent_1"] == null);
            }
            // Ignore obsolete movement overrides in previously saved match metadata.
            var oldMetadata=JObject.FromObject(match);
            oldMetadata["move_component_1"]=false;
            var restored=oldMetadata.ToObject<AssemblyFrameMatch>().Clone();
            Check(JObject.FromObject(restored)["move_component_1"] == null);
            AssemblyMatchRules.ApplyExport(mounting,new[] {restored});
            Check(JObject.FromObject(mounting.assemblyConstraints.Single())["move_component_1"] == null);
            assembly.transformation.translation.Z=10; target.transformation.translation.Z=20;
            Check(!AssemblyMatchRules.IdentifyComponent1MovingPart(mounting.components,assembly.name,target.name));
            assembly.transformation.translation.Z=30;
            Check(AssemblyMatchRules.IdentifyComponent1MovingPart(mounting.components,assembly.name,target.name));
            target.transformation.translation.Z=30;
            Check(AssemblyMatchRules.IdentifyComponent1MovingPart(mounting.components,assembly.name,target.name));
            var legacy=JsonConvert.DeserializeObject<AssemblyConstraintDescription>("{\"moveComponent_1\":true}");
            Check(legacy.moveComponent_1);
            var json=JObject.FromObject(legacy);
            Check((bool)json["move_component_1"]); Check(json["moveComponent_1"] == null);
            Equal("PlaneMatch",(string)json["type"]);
            var roundTrip=JsonConvert.DeserializeObject<MountingDescription>(JsonConvert.SerializeObject(mounting));
            Equal("FrameMatch",roundTrip.assemblyConstraints.Single().type);
            Equal("Mount",roundTrip.assemblyConstraints.Single().assemblyFrame.frame);
            int planeIndex=roundTrip.AddComponentsAssemblyConstraint(assembly.name,target.name);
            Equal("PlaneMatch",roundTrip.assemblyConstraints[planeIndex].type);
            Equal(2,roundTrip.assemblyConstraints.Count);
        });
        Test("Double constraint geometry preserves plane normals and chained orthogonality", () =>
        {
            var engine = new PreciseConstraintEngine();
            var origin = new PrecisePose(new Vector3d(312.123456789, -207.987654321, 519.123456789),
                Quaterniond.Normalize(new Quaterniond(.123456789, -.234567891, .345678912, .876543219)));
            var a = engine.Transform(origin, new PrecisePose(new Vector3d(-192.7, -10.5, 41.00003), Quaterniond.Identity));
            var b = engine.Transform(origin, new PrecisePose(new Vector3d(-29.7, -10.5, 41.00003), Quaterniond.Identity));
            var c = engine.Transform(origin, new PrecisePose(new Vector3d(-205.5, -4, 41.00003), Quaterniond.Identity));
            foreach (string normal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            foreach (string orthogonal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            {
                if (normal.TrimStart('-') == orthogonal.TrimStart('-')) continue;
                var frame = engine.Orthogonal(a, b, c, 63.87, false, 3.25, orthogonal, normal, origin);
                var unit = normal.EndsWith("x") ? Vector3d.UnitX : normal.EndsWith("y") ? Vector3d.UnitY : Vector3d.UnitZ;
                var n = Vector3d.Transform(unit, frame.rotation);
                var expected = Vector3d.Transform(Vector3d.UnitZ, origin.rotation);
                Check(Vector3d.Cross(n, expected).Length() < 1e-12);
                var px = engine.Transform(frame, new PrecisePose(new Vector3d(6.5, 0, 0), Quaterniond.Identity)).translation;
                var py = engine.Transform(frame, new PrecisePose(new Vector3d(0, 6.5, 0), Quaterniond.Identity)).translation;
                var pz = engine.Transform(frame, new PrecisePose(new Vector3d(0, 0, 6.5), Quaterniond.Identity)).translation;
                var dx = Vector3d.Normalize(px-frame.translation);
                var dy = Vector3d.Normalize(py-frame.translation);
                var dz = Vector3d.Normalize(pz-frame.translation);
                Check(Math.Abs(Vector3d.Dot(dx, dy)) < 1e-12);
                Check(Math.Abs(Vector3d.Dot(dx, dz)) < 1e-12);
                Check(Math.Abs(Vector3d.Dot(dy, dz)) < 1e-12);
            }
            var centroid = engine.Centroid(new[] { a, b, c }, Vector3d.Zero);
            Check(Vector3d.Cross(Vector3d.Transform(Vector3d.UnitZ, centroid.rotation),
                Vector3d.Transform(Vector3d.UnitZ, origin.rotation)).Length() < 1e-12);
        });
        Test("CAD readback and helper point checks preserve sub-float position differences", () =>
        {
            var raw = new[] { 1d,0,0,0,1,0,0,0,1,.312123456789,-.207987654321,.519123456789 };
            var pose = PrecisePose.FromCadArray(raw);
            Check(Math.Abs(pose.translation.X - 312.123456789) < 1e-12);
            Check(Math.Abs((double)(float)pose.translation.X - pose.translation.X) > 1e-6);
            Check(CoordinateTransforms.CadPoseMatches(raw, pose.translation, pose.rotation));
            var oldPoint = new[] { (double)(float)pose.translation.X / 1000, raw[10], raw[11] };
            Check(!ReferencePointGeometry.MatchesPosition(oldPoint, pose.translation));
            Check(ReferencePointGeometry.MatchesPosition(new[] { raw[9], raw[10], raw[11] }, pose.translation));
        });
        Test("Unedited constraint fields retain hidden precision and typed precision survives repeated Apply", () =>
        {
            using (var box = new ConstraintNumberBox { DecimalPlaces = 3, Minimum = -1000000, Maximum = 1000000 })
            {
                box.Value = 12.123456789m;
                Check(box.TryCommit()); Equal(12.123456789m, box.Value);
                box.Text = "10.987654321";
                Check(box.TryCommit()); Check(box.TryCommit()); Equal(10.987654321m, box.Value);
            }
            var source = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Centroid };
            source.constraints.centroid.refFrameNames.Add("A");
            source.constraints.centroid.offsetValues = new[] { 12.123456f, -.000012345678f, 123456.78f }.ToList();
            for (int i = 0; i < 5; i++)
            using (var dialog = ConstraintEditorDialog.ForCopy(new[] { "A" }, "Copy", source))
            {
                typeof(ConstraintEditorDialog).GetMethod("Apply_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(dialog, new object[] { null, EventArgs.Empty });
                Check(source.constraints.centroid.offsetValues.SequenceEqual(dialog.FrameMetadata.constraints.centroid.offsetValues));
                source = dialog.FrameMetadata;
            }
            source.constraintKind = ConstraintKind.Transform;
            source.constraints.transform.refFrame = "A";
            source.constraints.transform.transform.rotation = Quaternion.Normalize(new Quaternion(.123456789f, -.234567891f, .345678912f, .876543219f));
            for (int i = 0; i < 5; i++)
            using (var dialog = ConstraintEditorDialog.ForCopy(new[] { "A" }, "Copy", source))
            {
                typeof(ConstraintEditorDialog).GetMethod("Apply_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(dialog, new object[] { null, EventArgs.Empty });
                Equal(source.constraints.transform.transform.rotation, dialog.FrameMetadata.constraints.transform.transform.rotation);
                source = dialog.FrameMetadata;
            }
        });
        Test("Numerical CAD creation preserves right angles and detects old float-angle errors", () =>
        {
            // Equal quaternion components describe an exact quarter turn.
            var q = new Quaternion(0, 0, 1, 1);
            double angle = CoordinateTransforms.ToEulerXyzDouble(q)[2];
            Check(Math.Abs(angle - Math.PI / 2) < 1e-15);
            Func<double, double[]> cad = a => new[] { Math.Cos(a), Math.Sin(a), 0d,
                -Math.Sin(a), Math.Cos(a), 0d, 0d, 0d, 1d, 0d, 0d, 0d };
            Check(CoordinateTransforms.CadPoseMatches(cad(angle), Vector3.Zero, q));
            Check(!CoordinateTransforms.CadPoseMatches(cad((float)angle), Vector3.Zero, q));
            Check(Math.Abs(Math.Cos((float)angle)) > 4e-8);
        });
        Test("Creation and paste reuse deleted names while protecting live frames", () =>
        {
            foreach (bool paste in new[] { false, true })
            foreach (bool wasConstraint in new[] { false, true })
            {
                var document = new SwasiDocumentMetadata();
                document.frames["Deleted"] = new SwasiFrameMetadata { isConstraintFrame = wasConstraint };
                var unrelated = new SwasiFrameMetadata();
                document.frames["Unrelated"] = unrelated;
                var replacement = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Centroid };
                document.SetConstraintFrame("deleted", replacement, new[] { "Unrelated" }, paste);
                Check(!document.frames.ContainsKey("Deleted"));
                Check(ReferenceEquals(replacement, document.frames["deleted"]));
                Check(ReferenceEquals(unrelated, document.frames["Unrelated"]));
                var loaded = JsonConvert.DeserializeObject<SwasiDocumentMetadata>(JsonConvert.SerializeObject(document));
                Check(loaded.frames["deleted"].isConstraintFrame);
                string before = JsonConvert.SerializeObject(document);
                Throws<InvalidOperationException>(() => document.SetConstraintFrame("UNRELATED", replacement, new[] { "Unrelated" }, paste));
                Throws<InvalidOperationException>(() => document.SetConstraintFrame("Native", replacement, new[] { "Native" }, paste));
                Throws<InvalidOperationException>(() => document.SetConstraintFrame("deleted", new SwasiFrameMetadata(), new[] { "deleted" }, true));
                Equal(before, JsonConvert.SerializeObject(document));
            }
        });
        Test("Paste editor preserves each constraint kind and creates independent editable copies", () =>
        {
            foreach (var kind in new[] { ConstraintKind.Centroid, ConstraintKind.Orthogonal, ConstraintKind.Transform })
            {
                var source = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = kind,
                    role = SwasiFrameRole.Vision | SwasiFrameRole.Assembly, inPlaneToleranceMm = .25f };
                source.constraints.centroid.refFrameNames.AddRange(new[] { "A", "B" });
                source.constraints.centroid.dim = "xyz";
                source.constraints.centroid.offsetValues = new[] { 10f, -2.5f, 3f }.ToList();
                source.constraints.orthogonal = new RefFrameOrthogonalConstraint { frame_1 = "A", frame_2 = "B", frame_3 = "C",
                    distance_from_f1 = 25, unit_distance_from_f1 = "%", distance_from_f1_f2_connection = -10,
                    frame_orthogonal_connection_axis = "y", frame_normal_plane_axis = "-z" };
                source.constraints.transform = new RefFrameTransformConstraint { refFrame = "A",
                    transform = new CoordinateSystemDescription(new Vector3(10, -20, 30), Quaternion.Identity) };
                source.constraints.inPlane.refFrameNames.AddRange(new[] { "A", "B", "C" });
                source.constraints.inPlane.planeOffset = 2;
                string before = JsonConvert.SerializeObject(source);
                for (int copy = 1; copy <= 2; copy++)
                {
                    using (var dialog = ConstraintEditorDialog.ForCopy(new[] { "A", "B", "C" }, "Original_Copy", source))
                    {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        var name = (System.Windows.Forms.TextBox)typeof(ConstraintEditorDialog).GetField("nameBox", flags).GetValue(dialog);
                        Check(!name.ReadOnly);
                        Equal("Original_Copy", dialog.FrameName);
                        name.Text = "Changed_" + copy;
                        typeof(ConstraintEditorDialog).GetMethod("Apply_Click", flags).Invoke(dialog, new object[] { null, EventArgs.Empty });
                        Equal(System.Windows.Forms.DialogResult.OK, dialog.DialogResult);
                        Equal("Changed_" + copy, dialog.FrameName);
                        Equal(before, JsonConvert.SerializeObject(dialog.FrameMetadata));
                        dialog.FrameMetadata.constraints.centroid.offsetValues[0] = 999;
                        dialog.FrameMetadata.constraints.inPlane.refFrameNames.Clear();
                        Equal(before, JsonConvert.SerializeObject(source));
                    }
                }
                using (var cancelled = ConstraintEditorDialog.ForCopy(new[] { "A", "B", "C" }, "Cancelled", source))
                    Check(cancelled.FrameMetadata == null);
                Equal(before, JsonConvert.SerializeObject(source));
            }
        });
        Test("Constraint number controls accept dot and comma decimals in German and English", () =>
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "de-DE", "en-US", "fr-FR" })
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(culture);
                    using (var box = new ConstraintNumberBox { Minimum = -1000000, Maximum = 1000000, DecimalPlaces = 4, Increment = .1m })
                    {
                        foreach (string input in new[] { "10.00", "10,00", "10.000", "10,000" })
                        {
                            box.Value = 0;
                            box.Text = input;
                            Equal(10m, box.Value); // Value getter validates pending typed/pasted text.
                            box.UpButton(); Equal(10.1m, box.Value);
                            box.DownButton(); Equal(10m, box.Value);
                            Check(box.TryCommit()); Equal(10m, box.Value); // Repeated focus/apply.
                        }
                        foreach (string input in new[] { "-0.0125", "-0,0125" })
                        {
                            box.Text = input;
                            Check(box.TryCommit()); Equal(-.0125m, box.Value);
                        }
                        foreach (string input in new[] { "1,000.00", "1.000,00", "10..00", "", "-", "1000001" })
                        {
                            box.Value = 7;
                            box.Text = input;
                            Check(!box.TryCommit());
                            box.UpButton();
                            Equal(7m, box.Value);
                            Equal(input, box.Text);
                            box.Text = "10,00";
                            Check(box.TryCommit()); Equal(10m, box.Value);
                        }
                        foreach (char separator in new[] { '.', ',' })
                        {
                            var key = new System.Windows.Forms.KeyPressEventArgs(separator);
                            typeof(ConstraintNumberBox).GetMethod("OnKeyPress",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                                .Invoke(box, new object[] { key });
                            Check(!key.Handled);
                            Equal(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0], key.KeyChar);
                        }
                    }
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
        });
        Test("Centroid offsets survive repeated editor metadata clones", () =>
        {
            var frame = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Centroid };
            frame.constraints.centroid.refFrameNames.AddRange(new[] { "ReferenceA", "ReferenceB" });
            var offsets = new[] { 12.345f, -6.789f, 0f };
            frame.constraints.centroid.offsetValues = offsets.ToList();
            for (int i = 0; i < 3; i++)
            {
                var clone = JsonConvert.DeserializeObject<SwasiFrameMetadata>(JsonConvert.SerializeObject(frame));
                Check(clone.constraints.centroid.refFrameNames.SequenceEqual(new[] { "ReferenceA", "ReferenceB" }));
                Check(clone.constraints.centroid.offsetValues.SequenceEqual(offsets));
                Check(!ReferenceEquals(frame.constraints.centroid.offsetValues, clone.constraints.centroid.offsetValues));
                frame = clone;
            }
            for (int i = 0; i < 3; i++)
            {
                var workingCopy = frame.Clone();
                Check(workingCopy.constraints.centroid.offsetValues.SequenceEqual(offsets));
                Check(workingCopy.constraints.centroid.refFrameNames.SequenceEqual(new[] { "ReferenceA", "ReferenceB" }));
                workingCopy.constraints.centroid.offsetValues[0] = 42f;
                Equal(offsets[0], frame.constraints.centroid.offsetValues[0]);
                workingCopy.constraints.centroid.offsetValues[0] = offsets[0];
                frame = workingCopy;
            }
            var defaults = JsonConvert.DeserializeObject<RefFrameCentroidConstraint>("{}");
            Check(defaults.offsetValues.SequenceEqual(new[] { 0f, 0f, 0f }));
        });
        Test("Previously saved centroid zero padding is repaired on load", () =>
        {
            foreach (int copies in new[] { 1, 2, 4 })
            {
                var document = new SwasiDocumentMetadata();
                var frame = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Centroid };
                frame.constraints.centroid.refFrameNames.Add("Reference");
                frame.constraints.centroid.offsetValues = Enumerable.Repeat(0f, copies * 3)
                    .Concat(new[] { 12.345f, -6.789f, 2f }).ToList();
                document.frames.Add("Centroid", frame);
                var restored = JsonConvert.DeserializeObject<SwasiDocumentMetadata>(JsonConvert.SerializeObject(document),
                    new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
                var recovered = restored.frames["Centroid"].Clone();
                Check(recovered.constraints.centroid.offsetValues.SequenceEqual(new[] { 12.345f, -6.789f, 2f }));
                Equal("Reference", recovered.constraints.centroid.refFrameNames.Single());
                var roundTrip = JsonConvert.DeserializeObject<SwasiFrameMetadata>(JsonConvert.SerializeObject(recovered));
                Check(roundTrip.constraints.centroid.offsetValues.SequenceEqual(recovered.constraints.centroid.offsetValues));
            }
            foreach (string json in new[] { "[1,2,3]", "[0,0,0]", "[1,0,0,4,5,6]", "[0,0,0,4]" })
            {
                var restored = JsonConvert.DeserializeObject<RefFrameCentroidConstraint>("{\"offsetValues\":" + json + "}");
                Check(restored.offsetValues.SequenceEqual(JsonConvert.DeserializeObject<float[]>(json)));
            }
        });
        Test("Constraint frame rename preserves metadata and updates all references", () =>
        {
            var document = new SwasiDocumentMetadata();
            var frame = new SwasiFrameMetadata { isConstraintFrame = true, role = SwasiFrameRole.Gripping };
            var dependent = new SwasiFrameMetadata();
            document.frames.Add("Old", frame);
            document.frames.Add("Dependent", dependent);
            dependent.constraints.centroid.refFrameNames.AddRange(new[] { "Old", "OldSuffix" });
            dependent.constraints.inPlane.refFrameNames.Add("Old");
            dependent.constraints.transform.refFrame = "Old";
            dependent.constraints.orthogonal.frame_1 = "Old";
            dependent.constraints.orthogonal.frame_2 = "Old";
            dependent.constraints.orthogonal.frame_3 = "Old";
            document.RenameConstraintFrame("Old", "New");
            Check(!document.frames.ContainsKey("Old"));
            Check(ReferenceEquals(frame, document.frames["New"]));
            Equal("New", dependent.constraints.centroid.refFrameNames[0]);
            Equal("OldSuffix", dependent.constraints.centroid.refFrameNames[1]);
            Equal("New", dependent.constraints.inPlane.refFrameNames[0]);
            Equal("New", dependent.constraints.transform.refFrame);
            Equal("New", dependent.constraints.orthogonal.frame_1);
            Equal("New", dependent.constraints.orthogonal.frame_2);
            Equal("New", dependent.constraints.orthogonal.frame_3);
            document.RenameConstraintFrame("New", "New");
            document.RenameConstraintFrame("New", "new");
            Equal("new", dependent.constraints.transform.refFrame);
            Throws<InvalidOperationException>(() => document.RenameConstraintFrame("new", "dependent"));
            Throws<InvalidOperationException>(() => document.RenameConstraintFrame("Dependent", "Other"));
            Throws<ArgumentException>(() => document.RenameConstraintFrame("new", " "));
            Check(ReferenceEquals(frame, document.frames["new"]));
            Equal("new", dependent.constraints.transform.refFrame);
        });
        Test("Rename reuses deleted point names but protects live features", () =>
        {
            var document = new SwasiDocumentMetadata();
            var frame = new SwasiFrameMetadata { isConstraintFrame = true, role = SwasiFrameRole.Gripping };
            var stale = new SwasiFrameMetadata { role = SwasiFrameRole.Vision };
            document.frames.Add("Source", frame);
            document.frames.Add("Gripping_point", stale);
            Throws<InvalidOperationException>(() => document.RenameConstraintFrame("Source", "gripping_point",
                new[] { "Source", "Gripping_point" }));
            Check(ReferenceEquals(stale, document.frames["Gripping_point"]));
            Throws<InvalidOperationException>(() => document.RenameConstraintFrame("Source", "LiveWithoutMetadata",
                new[] { "Source", "LiveWithoutMetadata" }));
            document.frames.Add("MissingConstraint", new SwasiFrameMetadata { isConstraintFrame = true });
            document.RenameConstraintFrame("Source", "gripping_point", new[] { "Source" });
            Check(!document.frames.ContainsKey("Gripping_point"));
            Check(!document.frames.ContainsKey("Source"));
            Check(ReferenceEquals(frame, document.frames["gripping_point"]));
            Check(document.frames.ContainsKey("MissingConstraint"));
        });
        Test("Rename replaces a deleted constraint frame definition and survives save/reload", () =>
        {
            var document = new SwasiDocumentMetadata();
            var source = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Transform,
                role = SwasiFrameRole.Gripping };
            source.constraints.transform.refFrame = "Reference";
            document.frames.Add("Source", source);
            document.frames.Add("Gripping_point", new SwasiFrameMetadata { isConstraintFrame = true,
                constraintKind = ConstraintKind.Centroid });
            var dependent = new SwasiFrameMetadata();
            dependent.constraints.transform.refFrame = "Source";
            document.frames.Add("Dependent", dependent);
            Throws<InvalidOperationException>(() => document.RenameConstraintFrame("Source", "Gripping_point",
                new[] { "Source", "Gripping_point" }));
            document.RenameConstraintFrame("Source", "Gripping_point", new[] { "Source", "Dependent", "Reference" });
            Check(ReferenceEquals(source, document.frames["Gripping_point"]));
            Check(!document.frames.ContainsKey("Source"));
            Equal("Gripping_point", dependent.constraints.transform.refFrame);
            var reloaded = JsonConvert.DeserializeObject<SwasiDocumentMetadata>(JsonConvert.SerializeObject(document));
            Check(reloaded.frames["Gripping_point"].constraintKind == ConstraintKind.Transform);
            Equal("Reference", reloaded.frames["Gripping_point"].constraints.transform.refFrame);
            Equal("Gripping_point", reloaded.frames["Dependent"].constraints.transform.refFrame);
            Check(!reloaded.frames.ContainsKey("Source"));
        });
        Test("CAD translation is converted from meters to millimeters", () =>
        {
            var matrix = CoordinateTransforms.FromCadArray(new double[] { 1,0,0,0,1,0,0,0,1,0.01,-0.02,0.003 });
            Equal(new Vector3(10,-20,3), CoordinateTransforms.GetTranslation(matrix));
            Equal(1f, matrix.M44);
        });
        Test("CAD rotation preserves established SWASI matrix convention", () =>
        {
            var matrix = CoordinateTransforms.FromCadArray(new double[] { 0,1,0,-1,0,0,0,0,1,0,0,0 });
            Near(-1, matrix.M12); Near(1, matrix.M21);
            var pose = new CoordinateSystemDescription(matrix);
            Near(1, Math.Abs(Quaternion.Dot(pose.rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -(float)Math.PI / 2))));
        });
        Test("Coordinate-system pose matches measured SolidWorks numerical transforms", () =>
        {
            // Captured from a SolidWorks 2025 coordinate system at (10,20,30)
            // mm with numerical Z rotation +90 degrees.
            var matrix = CoordinateTransforms.CoordinateSystemPoseFromCadArray(
                new double[] { 0,1,0,-1,0,0,0,0,1,.01,.02,.03 });
            var pose = new CoordinateSystemDescription(matrix);
            Near(10, pose.translation.X); Near(20, pose.translation.Y); Near(30, pose.translation.Z);
            Near(1, Math.Abs(Quaternion.Dot(pose.rotation,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2))));
        });
        Test("Pose round-trip and inverse include rotation and translation", () =>
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(.2f, -.6f, 1.2f);
            var pose = new CoordinateSystemDescription(new Vector3(10,-20,30), rotation);
            var roundTrip = new CoordinateSystemDescription(pose.AsMatrix4x4());
            Equal(pose.translation, roundTrip.translation);
            Near(1, Math.Abs(Quaternion.Dot(rotation, roundTrip.rotation)));
            var identity = pose.AsMatrix4x4() * pose.GetInverted4x4Matrix();
            Near(1,identity.M11); Near(1,identity.M22); Near(1,identity.M33); Near(1,identity.M44);
            Near(0, identity.M14); Near(0, identity.M24); Near(0, identity.M34);
        });
        Test("Relative translation uses parent inverse", () =>
        {
            var parent = CoordinateTransforms.ToMatrix(new Vector3(10,20,30), Quaternion.Identity);
            var child = CoordinateTransforms.ToMatrix(new Vector3(15,18,35), Quaternion.Identity);
            Equal(new Vector3(5,-2,5), CoordinateTransforms.GetTranslation(CoordinateTransforms.Invert(parent) * child));
        });
        Test("Invalid transforms fail explicitly", () =>
        {
            Throws<ArgumentException>(() => CoordinateTransforms.Invert(new Matrix4x4()));
            Throws<ArgumentException>(() => CoordinateTransforms.FromCadArray(new double[3]));
            Throws<ArgumentException>(() => CoordinateTransforms.FromCadArray(null));
        });
        Test("Plane normals rotate without translation", () =>
        {
            var matrix = CoordinateTransforms.ToMatrix(new Vector3(50,100,200), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2));
            var normal = CoordinateTransforms.TransformPlaneNormal(Vector3.UnitX, matrix);
            Near(0,normal.X); Near(-1,normal.Y); Near(0,normal.Z); Near(1,normal.Length());
        });
        Test("Core assembly does not reference SolidWorks or WinForms", () =>
        {
            var names = typeof(ExportMergePolicy).Assembly.GetReferencedAssemblies().Select(a => a.Name);
            Check(!names.Any(n => n.StartsWith("SolidWorks.Interop",StringComparison.Ordinal) || n == "System.Windows.Forms"));
        });
        Test("Feature tags require an ordinal prefix", () =>
        {
            Check(FeatureNameRules.IsReference("SWASI_Grip_Frame"));
            Check(FeatureNameRules.IsOrigin("SWASI_Origin"));
            Check(FeatureNameRules.IsOrigin("SWASI_Origin_Main"));
            Check(!FeatureNameRules.IsReference("Other_SWASI_Frame"));
            Check(!FeatureNameRules.IsReference("swasi_Frame"));
            Check(!FeatureNameRules.IsOrigin(null));
            Check(FeatureNameRules.IsAutoGenerated("(auto)SWASI_Frame"));
            Equal("Frame_Point", FeatureNameRules.ReferenceName("(auto)SWASI_Frame_Point"));
        });
        Test("Generated origin points resolve to their frame in plane and axis references", () =>
        {
            Equal("Frame", FeatureNameRules.PointReferenceName("(auto)SWASI_Frame_Point"));
            Equal("Frame_Point", FeatureNameRules.PointReferenceName("SWASI_Frame_Point"));
            Equal("Frame_Point", FeatureNameRules.ReferenceName("(auto)SWASI_Frame_Point"));
            Equal("Frame_Point", FeatureNameRules.PointReferenceName("(auto)SWASI_Frame_Point_Point"));
            Equal("Frame", FeatureNameRules.ReferenceName("(auto)SWASI_Frame"));
            Equal("", FeatureNameRules.PointReferenceName(null));
        });
        Test("Origin point offsets handle units, reversed normals, and zero crossings", () =>
        {
            var plane = new double[] { 1,0,0,0,1,0,0,0,1,0,0,0 };
            Near(.03, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,30)));
            Near(-.03, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,-30)));
            Near(0, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,0)));
            plane[8] = -1;
            Near(-.03, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,30)));
            plane[11] = .01;
            Near(-.02, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,30)));
            plane[6] = 1; plane[8] = 0; plane[9] = .004;
            Near(.006, ReferencePointGeometry.SignedPlaneOffsetMeters(plane, new Vector3(10,-20,30)));
            Check(ReferencePointGeometry.MatchesPosition(new double[] { .01,-.02,.03 }, new Vector3(10,-20,30)));
            Check(!ReferencePointGeometry.MatchesPosition(new double[] { .01,-.02,.031 }, new Vector3(10,-20,30)));
            Check(!ReferencePointGeometry.MatchesPosition(new double[] { double.NaN,0,0 }, Vector3.Zero));
            Throws<ArgumentException>(() => ReferencePointGeometry.SignedPlaneOffsetMeters(new double[12], Vector3.Zero));
            Check(!FeatureNameRules.IsReference("(auto)_SWASI_helper"));
            Check(!FeatureNameRules.IsReference("(auto)_SWASI_helper_Frame_Plane0"));
            Check(!FeatureNameRules.IsReference("(auto)_SWASI_helper_Frame_Axis"));
        });
        Test("Feature properties preserve combined tags and assembly precedence", () =>
        {
            var frame = new RefFrameDescription();
            FeatureNameRules.ApplyProperties(frame, "SWASI_Vision_Laser_Grip_Glue_Target_Assembly");
            Check(frame.properties.visionProperties.isVisionFrame && frame.properties.laserProperties.isLaserFrame);
            Check(frame.properties.grippingProperties.isGrippingFrame && frame.properties.gluePtProperties.isGlueFrame);
            Check(frame.properties.assemblyProperties.isAssemblyFrame && !frame.properties.assemblyProperties.isTargetFrame);
        });
        Test("Merge preserves identity, configured constraints, and new CAD geometry", () =>
        {
            var old = Fixture();
            var generated = (JObject)old.DeepClone();
            generated["guid"] = Guid.NewGuid().ToString();
            generated["saveDate"] = "2030-01-01";
            Frames(generated)[0]["transformation"]["translation"]["X"] = 123;
            Frames(generated)[0]["constraints"] = new JObject();
            var output = Policy.Merge(generated, old);
            Equal((string)old["guid"], (string)output["guid"]);
            Check(JToken.DeepEquals(old["saveDate"], output["saveDate"]));
            Equal(123, (int)Frames(output)[0]["transformation"]["translation"]["X"]);
            Check(JToken.DeepEquals(Frames(old)[0]["constraints"]["inPlane"],
                Frames(output)[0]["constraints"]["inPlane"]));
            Check(Frames(output)[0]["constraints"]["centroid"] == null);
            Check(Frames(output)[0]["constraints"]["orthogonal"] == null);
        });
        Test("Merge precedence is independent of frame count and input order", () =>
        {
            foreach (int extra in new[] { -1, 0, 1 })
            {
                var old = Fixture(); var generated = (JObject)old.DeepClone();
                Frames(old)[0]["constraints"]["centroid"]["dim"] = "authored";
                if (extra < 0) Frames(generated).Last.Remove();
                if (extra > 0) Frames(generated).Add(new JObject { ["name"] = "new" });
                var firstName = (string)Frames(old)[0]["name"];
                var result = Policy.Merge(generated, old);
                Equal("authored", (string)Frames(result).First(f => (string)f["name"] == firstName)["constraints"]["centroid"]["dim"]);
                generated.SelectToken("mountingDescription.mountingReferences.ref_frames").Replace(new JArray(Frames(generated).Reverse()));
                result = Policy.Merge(generated, old);
                Equal("authored", (string)Frames(result).First(f => (string)f["name"] == firstName)["constraints"]["centroid"]["dim"]);
            }
        });
        Test("Merge retains authored-only frames and newly extracted frames", () =>
        {
            var old = Fixture(); var generated = (JObject)old.DeepClone();
            Frames(old).Add(new JObject { ["name"] = "manual" });
            Frames(generated).Add(new JObject { ["name"] = "new" });
            var names = Frames(Policy.Merge(generated,old)).Select(f => (string)f["name"]).ToArray();
            Check(names.Contains("manual") && names.Contains("new"));
        });
        Test("Fresh component membership wins while matching GUIDs survive", () =>
        {
            var old = JObject.Parse("{mountingDescription:{components:[{name:'a',guid:'old',x:1},{name:'removed',guid:'removed'}]}}");
            var generated = JObject.Parse("{mountingDescription:{components:[{name:'a',guid:'new',x:2},{name:'added',guid:'added'}]}}");
            var components = (JArray)Policy.Merge(generated,old).SelectToken("mountingDescription.components");
            Equal(2,components.Count); Equal("old",(string)components[0]["guid"]); Equal(2,(int)components[0]["x"]);
            Equal("added",(string)components[1]["name"]);
        });
        Test("Merge handles absent optional arrays without losing metadata", () =>
        {
            var output = Policy.Merge(new JObject { ["guid"] = "new" }, new JObject { ["guid"] = "old" });
            Equal("old",(string)output["guid"]);
            Check(JToken.DeepEquals(new JObject(),Policy.Merge(new JObject(),null)));
        });
        Test("Merge is idempotent and does not mutate either input", () =>
        {
            var old = Fixture(); var generated = Fixture();
            string oldText = old.ToString(), newText = generated.ToString();
            var output = Policy.Merge(generated,old);
            Check(JToken.DeepEquals(output,Policy.Merge(output,old)));
            Frames(output)[0]["name"] = "changed";
            Equal(oldText,old.ToString()); Equal(newText,generated.ToString());
        });
        Test("Document-backed frame metadata overrides an older JSON edit", () =>
        {
            var old = Fixture(); var generated = (JObject)old.DeepClone();
            string name = (string)Frames(old)[0]["name"];
            Frames(old)[0]["properties"] = new JObject { ["laserProperties"] = new JObject { ["isLaserFrame"] = true } };
            Frames(generated)[0]["properties"] = new JObject { ["visionProperties"] = new JObject { ["isVisionFrame"] = true } };
            Frames(old)[0]["constraints"]["centroid"]["dim"] = "old";
            Frames(generated)[0]["constraints"]["centroid"]["dim"] = "new";
            var result = Policy.Merge(generated, old, new System.Collections.Generic.HashSet<string> { name });
            Check(result.SelectToken("mountingDescription.mountingReferences.ref_frames[0].properties.visionProperties") != null);
            Equal("new",(string)Frames(result)[0]["constraints"]["centroid"]["dim"]);
        });
        Test("Role-only metadata keeps legacy constraints and color during migration", () =>
        {
            var old = Fixture(); var generated = (JObject)old.DeepClone();
            string name = (string)Frames(old)[0]["name"];
            Frames(old)[0]["constraints"]["centroid"]["dim"] = "manual";
            Frames(generated)[0]["constraints"]["centroid"]["dim"] = "empty";
            Frames(old)[0]["properties"] = new JObject { ["laserProperties"] = new JObject { ["isLaserFrame"] = true } };
            Frames(generated)[0]["properties"] = new JObject { ["visionProperties"] = new JObject { ["isVisionFrame"] = true } };
            old["color"] = new JObject { ["R"] = 1, ["G"] = 2, ["B"] = 3 };
            generated["color"] = new JObject { ["R"] = 255, ["G"] = 255, ["B"] = 255 };
            var properties = new System.Collections.Generic.HashSet<string> { name };
            var result = Policy.Merge(generated, old, properties,
                new System.Collections.Generic.HashSet<string>(), false);
            Check(Frames(result)[0]["properties"]["visionProperties"] != null);
            Equal("manual", (string)Frames(result)[0]["constraints"]["centroid"]["dim"]);
            Equal(1, (int)result["color"]["R"]);
            result = Policy.Merge(generated, old, properties, properties, true);
            Equal("empty", (string)Frames(result)[0]["constraints"]["centroid"]["dim"]);
            Equal(255, (int)result["color"]["R"]);
        });
        Test("Existing example coordinate JSON survives typed round-trip", () =>
        {
            foreach (var frame in Frames(Fixture()))
            {
                var token = frame["transformation"];
                var pose = token.ToObject<CoordinateSystemDescription>();
                var output = JObject.FromObject(pose, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                Check(JToken.DeepEquals(token,output));
            }
        });
        Test("Model serialization keeps legacy field names and conditional properties", () =>
        {
            var frame = new RefFrameDescription();
            FeatureNameRules.ApplyProperties(frame,"SWASI_Vision_Point");
            var output = JObject.FromObject(frame);
            Check(output["transformation"]["translation"]["X"] != null);
            Check(output["properties"]["visionProperties"] != null);
            Check(output["properties"]["laserProperties"] == null);
            Check(output["constraints"] == null);
            Check(output["Name"] == null);
            frame.constraints.centroid.refFrameNames.Add("Reference");
            output = JObject.FromObject(frame);
            Check(output["constraints"]["centroid"] != null);
            Check(output["constraints"]["orthogonal"] == null);
            Check(output["constraints"]["inPlane"] == null);
            Check(output["constraints"]["transform"] == null);
            var generated = Fixture();
            var firstFrame = Frames(generated)[0];
            firstFrame["constraints"] = JObject.FromObject(new RefFrameConstraints());
            var merged = Policy.Merge(generated, (JObject)generated.DeepClone());
            Check(Frames(merged)[0]["constraints"] == null);
            var component = JObject.FromObject(new ComponentDescription());
            Equal("mm",(string)component["documentUnits"]);
            Check(component["mountingDescription"] != null && component["cadPath"] != null);
        });
        Test("Partial and empty exports cannot report complete success", () =>
        {
            var result = new ExportResult(); Check(!result.IsCompleteSuccess);
            result.Add("a",ExportStatus.Succeeded,""); Check(result.IsCompleteSuccess);
            result.Add("b",ExportStatus.Skipped,""); Check(!result.IsCompleteSuccess);
            result.Add("c",ExportStatus.Failed,"");
            Equal(1,result.SucceededCount); Equal(1,result.SkippedCount); Equal(1,result.FailedCount);
        });
        Test("Centroid uses local offset and preserves a single reference orientation", () =>
        {
            var engine = new ConstraintEngine();
            var reference = new CoordinateSystemDescription(new Vector3(10,20,30),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2));
            var result = engine.Centroid(new[] { reference }, new Vector3(2,0,0));
            Near(10,result.translation.X); Near(22,result.translation.Y); Near(30,result.translation.Z);
            Near(1,Math.Abs(Quaternion.Dot(reference.rotation,result.rotation)));
        });
        Test("Orthogonal constraint follows assembly-manager axis and distance rules", () =>
        {
            var engine = new ConstraintEngine();
            var identity = Quaternion.Identity;
            var result = engine.Orthogonal(
                new CoordinateSystemDescription(new Vector3(0,0,0),identity),
                new CoordinateSystemDescription(new Vector3(100,0,0),identity),
                new CoordinateSystemDescription(new Vector3(0,100,0),identity),
                50,true,10,"x","z");
            Equal(new Vector3(50,-10,0),result.translation);
            Near(1,result.rotation.Length());
            var xAxis = Vector3.Transform(Vector3.UnitX,result.rotation);
            var yAxis = Vector3.Transform(Vector3.UnitY,result.rotation);
            Near(0,xAxis.X); Near(-1,xAxis.Y); Near(0,xAxis.Z);
            Near(1,yAxis.X); Near(0,yAxis.Y); Near(0,yAxis.Z);
        });
        Test("Orthogonal axis orientation follows a rotated SWASI origin", () =>
        {
            var engine = new ConstraintEngine();
            var origin = new CoordinateSystemDescription(new Vector3(25, -40, 10),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI));
            var local1 = new CoordinateSystemDescription(Vector3.Zero, Quaternion.Identity);
            var local2 = new CoordinateSystemDescription(new Vector3(0, 100, 0), Quaternion.Identity);
            var local3 = new CoordinateSystemDescription(new Vector3(0, 0, 100), Quaternion.Identity);
            var frame1 = engine.Transform(origin, local1);
            var frame2 = engine.Transform(origin, local2);
            var frame3 = engine.Transform(origin, local3);
            var localExpected = engine.Orthogonal(local1, local2, local3, 50, true, 10, "x", "z");
            var expected = engine.Transform(origin, localExpected);
            var result = engine.Orthogonal(frame1, frame2, frame3, 50, true, 10, "x", "z", origin);
            Equal(expected.translation, result.translation);
            Near(1, Math.Abs(Quaternion.Dot(expected.rotation, result.rotation)));
            var normal = Vector3.Transform(Vector3.UnitZ, result.rotation);
            Near(-1, normal.X); Near(0, normal.Y); Near(0, normal.Z);
        });
        Test("Orthogonal normal follows selected positive SWASI axis on tilted planes", () =>
        {
            var engine = new ConstraintEngine();
            var origin = new CoordinateSystemDescription(new Vector3(25, -40, 10),
                Quaternion.CreateFromYawPitchRoll(.7f, -.4f, 1.1f));
            var p1 = new CoordinateSystemDescription(Vector3.Zero, Quaternion.Identity);
            var p2 = new CoordinateSystemDescription(new Vector3(0, 10, 0), Quaternion.Identity);
            var p3 = new CoordinateSystemDescription(new Vector3(2, 0, 10), Quaternion.Identity);
            // Cross product is (100, 0, -20): dominant-positive would point Z down.
            foreach (bool reversed in new[] { false, true })
            foreach (string slot in new[] { "x", "z", "-x", "-z" })
            {
                var a = engine.Transform(origin, p1);
                var b = engine.Transform(origin, reversed ? p3 : p2);
                var c = engine.Transform(origin, reversed ? p2 : p3);
                var result = engine.Orthogonal(a, b, c, 50, true, 0, "y", slot, origin);
                Vector3 axis = slot.EndsWith("x") ? Vector3.UnitX : Vector3.UnitZ;
                Vector3 normal = Vector3.Transform(axis, result.rotation);
                Near(0, Vector3.Dot(normal, Vector3.Normalize(b.translation - a.translation)));
                Near(0, Vector3.Dot(normal, Vector3.Normalize(c.translation - a.translation)));
                float projection = Vector3.Dot(normal, Vector3.Transform(axis, origin.rotation));
                Check(slot.StartsWith("-") ? projection < 0 : projection > 0);
            }
        });
        Test("Euler conversion preserves every orthogonal axis assignment including gimbal lock", () =>
        {
            var engine = new ConstraintEngine();
            var a = new CoordinateSystemDescription();
            var b = new CoordinateSystemDescription(new Vector3(100, 0, 0), Quaternion.Identity);
            var c = new CoordinateSystemDescription(new Vector3(0, 100, 0), Quaternion.Identity);
            foreach (string normal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            foreach (string orthogonal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            {
                if (normal.TrimStart('-') == orthogonal.TrimStart('-')) continue;
                var pose = engine.Orthogonal(a, b, c, 50, true, 0, orthogonal, normal);
                var angles = CoordinateTransforms.ToEulerXyz(pose.rotation);
                var written = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angles.X)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitY, angles.Y)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angles.Z);
                foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
                    Near(0, Vector3.Distance(Vector3.Transform(axis, pose.rotation), Vector3.Transform(axis, written)));
            }
            foreach (float pitch in new[] { -.5f, .5f, -(float)Math.PI / 2, (float)Math.PI / 2 })
            {
                var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .8f)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -.3f);
                var angles = CoordinateTransforms.ToEulerXyz(q);
                var written = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angles.X)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitY, angles.Y)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angles.Z);
                Near(1, Math.Abs(Quaternion.Dot(q, written)));
            }
        });
        Test("Numerical Euler conversion reproduces a measured mixed SolidWorks rotation", () =>
        {
            var pose = new CoordinateSystemDescription(CoordinateTransforms.CoordinateSystemPoseFromCadArray(
                new double[] { .61141765887509669, .78402581010259587, -.10710730838119392,
                    -.62953919603926634, .56395437786761837, .534449118564323,
                    .479425538604203, -.25934338005223079, .83838664359420356, .01, .02, .03 }));
            var angles = CoordinateTransforms.ToEulerXyz(pose.rotation);
            Near(.3f, angles.X); Near(.5f, angles.Y); Near(.8f, angles.Z);
        });
        Test("HYENA orthogonal frames export Z normal and agree with local helper offsets", () =>
        {
            var engine = new ConstraintEngine();
            var a = new CoordinateSystemDescription(new Vector3(-192.7f, -10.5f, 41.00003f), Quaternion.Identity);
            var b = new CoordinateSystemDescription(new Vector3(-29.6999969f, -10.5f, 41.00003f), Quaternion.Identity);
            var c = new CoordinateSystemDescription(new Vector3(-205.5f, -3.99999952f, 41.00003f), Quaternion.Identity);
            var origin = new CoordinateSystemDescription(new Vector3(30, -20, 50),
                Quaternion.CreateFromYawPitchRoll(.4f, -.8f, 1.2f));
            // Column matrices are the CAD/export convention.
            Func<CoordinateSystemDescription, Matrix4x4> column = p =>
                CoordinateTransforms.ToMatrix(p.translation, Quaternion.Conjugate(p.rotation));
            foreach (string orthogonal in new[] { "x", "y" })
            {
                var pose = engine.Orthogonal(engine.Transform(origin, a), engine.Transform(origin, b),
                    engine.Transform(origin, c), orthogonal == "y" ? 63.87f : 99.12f, false, 0, orthogonal, "z", origin);
                var exported = CoordinateTransforms.PoseFromColumnMatrix(
                    CoordinateTransforms.Invert(column(origin)) * column(pose));
                Near(0, Vector3.Distance(Vector3.UnitZ, Vector3.Transform(Vector3.UnitZ, exported.rotation)));
                var helper = engine.Transform(pose, new CoordinateSystemDescription(new Vector3(0,0,10), Quaternion.Identity));
                var helperExport = CoordinateTransforms.PoseFromColumnMatrix(
                    CoordinateTransforms.Invert(column(origin)) * column(helper));
                Near(0, Vector3.Distance(new Vector3(0,0,10), helperExport.translation - exported.translation));
            }
        });
        Test("Transform constraint composes translation in reference orientation", () =>
        {
            var engine = new ConstraintEngine();
            var reference = new CoordinateSystemDescription(Vector3.Zero,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ,(float)Math.PI/2));
            var result = engine.Transform(reference,
                new CoordinateSystemDescription(new Vector3(5,0,0),Quaternion.Identity));
            Near(0,result.translation.X); Near(5,result.translation.Y);
        });
        Test("In-plane validation reports distance without changing the candidate", () =>
        {
            var engine = new ConstraintEngine();
            var q = Quaternion.Identity;
            var references = new[] {
                new CoordinateSystemDescription(new Vector3(0,0,0),q),
                new CoordinateSystemDescription(new Vector3(10,0,0),q),
                new CoordinateSystemDescription(new Vector3(0,10,0),q),
                new CoordinateSystemDescription(new Vector3(10,10,0),q) };
            var candidate = new CoordinateSystemDescription(new Vector3(2,2,.3f),q);
            var warning = engine.ValidateInPlane(candidate,references,0,.1f);
            Check(!warning.IsInPlane); Near(.3f,Math.Abs(warning.SignedDistance));
            Near(.3f,candidate.translation.Z);
            Check(engine.ValidateInPlane(candidate,references,.3f,.001f).IsInPlane);
        });
        Test("Frame role assignment is exclusive while keeping role properties", () =>
        {
            var properties = new RefFrameProperties();
            properties.grippingProperties.compatibleGrippers.Add("g1");
            FrameMetadataApplicator.ApplyRole(properties,SwasiFrameRole.Gripping);
            Check(properties.grippingProperties.isGrippingFrame);
            FrameMetadataApplicator.ApplyRole(properties,SwasiFrameRole.Target);
            Check(!properties.grippingProperties.isGrippingFrame);
            Check(properties.assemblyProperties.isTargetFrame);
            Equal("g1",properties.grippingProperties.compatibleGrippers[0]);
        });
        Test("Store merges re-exports and preserves a malformed existing file", () =>
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SwasiTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory,"part.json");
                var store = new JsonExportStore(Policy);
                store.Save(path,Fixture());
                string guid = (string)store.Read(path)["guid"];
                var generated = Fixture(); generated["guid"] = Guid.NewGuid().ToString();
                store.Save(path,generated); Equal(guid,(string)store.Read(path)["guid"]);
                File.WriteAllText(path,"invalid json");
                Throws<JsonReaderException>(() => store.Save(path,generated));
                Equal("invalid json",File.ReadAllText(path));
                Equal(1,Directory.GetFiles(directory).Length);
            }
            finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        });
        Console.WriteLine($"{total - failures}/{total} tests passed.");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static JObject Fixture() => JObject.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fixtures","Glas_6D_ideal.json")));
    private static JArray Frames(JObject document) => (JArray)document.SelectToken("mountingDescription.mountingReferences.ref_frames");
    private static void Test(string name, Action action)
    {
        total++;
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
    private static void Equal<T>(T expected, T actual) { if (!Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}."); }
    private static void Near(float expected, float actual) { if (Math.Abs(expected-actual) > .0001f) throw new Exception($"Expected approximately {expected}, got {actual}."); }
    private static void Near(double expected, double actual) { if (double.IsNaN(actual) || Math.Abs(expected-actual) > 1e-10) throw new Exception($"Expected approximately {expected}, got {actual}."); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
