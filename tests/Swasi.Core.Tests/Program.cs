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
    private static void Main()
    {
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
            Check(FeatureNameRules.IsOrigin("SWASI_Origin_Main"));
            Check(!FeatureNameRules.IsReference("Other_SWASI_Frame"));
            Check(!FeatureNameRules.IsReference("swasi_Frame"));
            Check(!FeatureNameRules.IsOrigin(null));
        });
        Test("Feature properties preserve combined tags and assembly precedence", () =>
        {
            var frame = new RefFrameDescription();
            FeatureNameRules.ApplyProperties(frame, "SWASI_Vision_Laser_Grip_Glue_Target_Assembly");
            Check(frame.properties.visionProperties.isVisionFrame && frame.properties.laserProperties.isLaserFrame);
            Check(frame.properties.grippingProperties.isGrippingFrame && frame.properties.gluePtProperties.isGlueFrame);
            Check(frame.properties.assemblyProperties.isAssemblyFrame && !frame.properties.assemblyProperties.isTargetFrame);
        });
        Test("Merge preserves identity, all constraint kinds, and new CAD geometry", () =>
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
            Check(JToken.DeepEquals(Frames(old)[0]["constraints"], Frames(output)[0]["constraints"]));
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
            Check(output["Name"] == null);
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
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
