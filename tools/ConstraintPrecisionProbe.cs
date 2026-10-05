using System;
using System.Linq;
using System.Reflection;
using SolidWorks.Interop.sldworks;
using SolidWorks_ASsembly_Instructor;

// Exercises the actual add-in, helper points and native three-point planes in
// its own unsaved document. No user documents are modified or saved.
public static class ConstraintPrecisionProbe
{
    public static void Run(string template, string addInPath)
    {
        var app = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
        ModelDoc2 doc = null;
        try
        {
            doc = (ModelDoc2)app.NewDocument(template, 0, 0, 0);
            if (doc == null) throw new Exception("Could not create probe document.");
            var originFeature = doc.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true, .312123456789,
                -.207987654321, .519123456789, true, .3, .5, .8);
            originFeature.Name = "SWASI_Origin_probe";
            var origin = PrecisePose.FromCadArray((double[])doc.Extension.GetCoordinateSystemTransformByName(originFeature.Name).ArrayData);
            var engine = new PreciseConstraintEngine();
            var positions = new[] { new Vector3d(-192.7,-10.5,41.00003), new Vector3d(-29.7,-10.5,41.00003), new Vector3d(-205.5,-4,41.00003) };
            for (int i = 0; i < 3; i++)
            {
                var p = engine.Transform(origin, new PrecisePose(positions[i], Quaterniond.Identity));
                var angles = CoordinateTransforms.ToEulerXyzDouble(p.rotation);
                var f = doc.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,
                    p.translation.X/1000, p.translation.Y/1000, p.translation.Z/1000, true, angles[0], angles[1], angles[2]);
                f.Name = "SWASI_" + (char)('A'+i);
            }
            var assembly = Assembly.LoadFrom(addInPath);
            var managerType = assembly.GetType("SolidWorks_ASsembly_Instructor.ConstraintFrameManager", true);
            var manager = Activator.CreateInstance(managerType, new object[] { new Action<string,string>((m,l) => Console.WriteLine(l+": "+m)) });
            var create = managerType.GetMethod("CreateOrUpdate");
            for (int i = 0; i < 3; i++)
            {
                var metadata = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Orthogonal };
                metadata.constraints.orthogonal = new RefFrameOrthogonalConstraint { frame_1="A", frame_2="B", frame_3="C",
                    distance_from_f1 = i == 1 ? 110 : 10, unit_distance_from_f1 = "mm",
                    distance_from_f1_f2_connection = i == 2 ? 6.5f : 0,
                    frame_orthogonal_connection_axis="y", frame_normal_plane_axis="z" };
                create.Invoke(manager, new object[] { doc, "P"+i, metadata, true });
            }
            var transform = new SwasiFrameMetadata { isConstraintFrame = true, constraintKind = ConstraintKind.Transform };
            transform.constraints.transform.refFrame = "P0";
            transform.constraints.transform.transform.translation = new System.Numerics.Vector3(0,0,6.5f);
            create.Invoke(manager, new object[] { doc, "P3", transform, true });
            var points = Enumerable.Range(0,4).Select(i => Find(doc, FeatureNameRules.AutoPrefix + FeatureNameRules.Prefix + "P"+i+"_Point")).ToArray();
            var planes = new[] { Plane(doc,points[0],points[1],points[2]), Plane(doc,points[0],points[1],points[3]), Plane(doc,points[0],points[2],points[3]) };
            CheckPlanes(planes, origin);

            // Simulate helper points saved by the old float path, then verify
            // Update repairs them without deleting the user-created planes.
            var helperType = assembly.GetType("SolidWorks_ASsembly_Instructor.ConstraintOriginPointManager", true);
            var updatePoint = helperType.GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic);
            for (int i=0; i<4; i++)
            {
                var values = (double[])((IRefPoint)points[i].GetSpecificFeature2()).GetRefPoint().ArrayData;
                var rounded = new Vector3d((float)(values[0]*1000), (float)(values[1]*1000), (float)(values[2]*1000));
                updatePoint.Invoke(null, new object[] { doc, "P"+i, rounded, null });
            }
            managerType.GetMethod("UpdateAll").Invoke(manager, new object[] { doc, true });
            doc.ForceRebuild3(false);
            CheckPlanes(planes, origin);
            Console.WriteLine("PASS: native constraint frames, helper-point migration and three mutually perpendicular SolidWorks planes.");
        }
        finally
        {
            if (doc != null) app.CloseDoc(doc.GetTitle());
            if (app.GetFirstDocument() == null) app.ExitApp();
        }
    }
    private static Feature Find(ModelDoc2 doc, string name)
    { return ((object[])doc.FeatureManager.GetFeatures(false)).OfType<Feature>().Single(f => f.Name == name); }
    private static IRefPlane Plane(ModelDoc2 doc, params Feature[] points)
    {
        doc.ClearSelection2(true);
        for (int i=0; i<3; i++) if (!points[i].Select2(true,i)) throw new Exception("Could not select point.");
        // Coincident=4, reference marks 0, 1, 2 (IFeatureManager.InsertRefPlane).
        var plane = doc.FeatureManager.InsertRefPlane(4,0,4,0,4,0) as IRefPlane;
        if (plane == null) throw new Exception("Could not create three-point plane.");
        doc.ClearSelection2(true);
        return plane;
    }
    private static void CheckPlanes(IRefPlane[] planes, PrecisePose origin)
    {
        var normals = planes.Select(p => { var a=(double[])p.Transform.ArrayData; return Vector3d.Normalize(new Vector3d(a[6],a[7],a[8])); }).ToArray();
        double maximum = 0;
        for (int i=0;i<3;i++) for (int j=i+1;j<3;j++) maximum = Math.Max(maximum,Math.Abs(Vector3d.Dot(normals[i],normals[j])));
        var expected = Vector3d.Transform(Vector3d.UnitZ, origin.rotation);
        double parallelError = Math.Atan2(Vector3d.Cross(normals[0],expected).Length(), Math.Abs(Vector3d.Dot(normals[0],expected)));
        Console.WriteLine("Max perpendicular error [degrees]: " + (Math.Asin(maximum)*180/Math.PI).ToString("R"));
        Console.WriteLine("Parallel error to source plane [degrees]: " + (parallelError*180/Math.PI).ToString("R"));
        if (maximum > 1e-10 || parallelError > 1e-10) throw new Exception("Plane angle exceeds precision tolerance.");
    }
}
