using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Numerics;
using SolidWorks.Interop.sldworks;
using SolidWorks_ASsembly_Instructor;

// Run through Add-Type with the installed SolidWorks interop assembly.
// Creates and closes only its own unsaved document; never saves user files.
public static class CoordinateSystemProbe
{
    public static void Run(string template)
    {
        var app = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
        ModelDoc2 document = null;
        try
        {
            document = (ModelDoc2)app.NewDocument(template, 0, 0, 0);
            if (document == null) throw new Exception("Could not create probe document.");
            foreach (var angles in new[] { new[] {0d,0d,0d}, new[] {0d,0d,Math.PI/2}, new[] {.3,.5,.8} })
            {
                var feature = document.FeatureManager.CreateCoordinateSystemUsingNumericalValues(
                    true, .01, .02, .03, true, angles[0], angles[1], angles[2]);
                if (feature == null) throw new Exception("Could not create probe coordinate system.");
                var values = (double[])document.Extension.GetCoordinateSystemTransformByName(feature.Name).ArrayData;
                Console.WriteLine("Angles: " + string.Join(", ", Array.ConvertAll(angles, Format)));
                Console.WriteLine("Array: " + string.Join(", ", Array.ConvertAll(values, Format)));
            }
            var originFeature = document.FeatureManager.CreateCoordinateSystemUsingNumericalValues(
                true, .03, -.02, .05, true, .3, .5, .8);
            var originMatrix = ReadMatrix(document, originFeature);
            var origin = CoordinateTransforms.PoseFromColumnMatrix(originMatrix);
            var engine = new ConstraintEngine();
            var a = engine.Transform(origin, new CoordinateSystemDescription(new Vector3(-192.7f,-10.5f,41.00003f), Quaternion.Identity));
            var b = engine.Transform(origin, new CoordinateSystemDescription(new Vector3(-29.6999969f,-10.5f,41.00003f), Quaternion.Identity));
            var c = engine.Transform(origin, new CoordinateSystemDescription(new Vector3(-205.5f,-3.99999952f,41.00003f), Quaternion.Identity));
            int count = 0;
            foreach (string normal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            foreach (string orthogonal in new[] { "x", "y", "z", "-x", "-y", "-z" })
            {
                if (normal.TrimStart('-') == orthogonal.TrimStart('-')) continue;
                var expected = engine.Orthogonal(a, b, c, orthogonal == "y" ? 63.87f : 99.12f,
                    false, 0, orthogonal, normal, origin);
                var angles = CoordinateTransforms.ToEulerXyzDouble(expected.rotation);
                var feature = document.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,
                    expected.translation.X / 1000d, expected.translation.Y / 1000d, expected.translation.Z / 1000d,
                    true, angles[0], angles[1], angles[2]);
                if (feature == null) throw new Exception("Numerical creation failed.");
                var raw = (double[])document.Extension.GetCoordinateSystemTransformByName(feature.Name).ArrayData;
                if (!CoordinateTransforms.CadPoseMatches(raw, expected.translation, expected.rotation))
                    throw new Exception("Full precision CAD pose mismatch: " + orthogonal + "/" + normal);
                var actualMatrix = ReadMatrix(document, feature);
                var actual = CoordinateTransforms.PoseFromColumnMatrix(actualMatrix);
                if (Vector3.Distance(actual.translation, expected.translation) > .001f)
                    throw new Exception("SolidWorks position differs from calculated position.");
                foreach (var axis in new[] {Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ})
                    if (Vector3.Distance(Vector3.Transform(axis, actual.rotation), Vector3.Transform(axis, expected.rotation)) > .0001f)
                        throw new Exception("SolidWorks axis mismatch: " + orthogonal + "/" + normal);
                var exported = CoordinateTransforms.PoseFromColumnMatrix(CoordinateTransforms.Invert(originMatrix) * actualMatrix);
                if (normal == "z" && (orthogonal == "x" || orthogonal == "y"))
                {
                    var normalAxis = Vector3.Transform(Vector3.UnitZ, exported.rotation);
                    if (Vector3.Distance(normalAxis, Vector3.UnitZ) > .0001f)
                        throw new Exception("HYENA exported Z is not the positive plane normal.");
                    Console.WriteLine("HYENA orthogonal=" + orthogonal + ": exported Z=" + normalAxis);
                }
                count++;
            }
            Console.WriteLine("PASS: " + count + " SolidWorks creation/readback axis combinations and HYENA export normals.");
        }
        finally
        {
            if (document != null) app.CloseDoc(document.GetTitle());
            if (app.GetFirstDocument() == null) app.ExitApp();
        }
    }
    private static Matrix4x4 ReadMatrix(ModelDoc2 document, Feature feature)
    {
        return CoordinateTransforms.FromCadArray((double[])document.Extension.GetCoordinateSystemTransformByName(feature.Name).ArrayData);
    }
    private static string Format(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
}
