using System;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    public static class ReferencePointGeometry
    {
        public static double SignedPlaneOffsetMeters(double[] planeTransform, Vector3 pointMm)
            => SignedPlaneOffsetMeters(planeTransform, (Vector3d)pointMm);

        public static double SignedPlaneOffsetMeters(double[] planeTransform, Vector3d pointMm)
        {
            if (planeTransform == null || planeTransform.Length < 12) throw new ArgumentException("Invalid plane transform.");
            double nx = planeTransform[6], ny = planeTransform[7], nz = planeTransform[8];
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-12) throw new ArgumentException("Invalid plane normal.");
            return ((pointMm.X / 1000d - planeTransform[9]) * nx
                + (pointMm.Y / 1000d - planeTransform[10]) * ny
                + (pointMm.Z / 1000d - planeTransform[11]) * nz) / length;
        }

        public static bool MatchesPosition(double[] pointMeters, Vector3 expectedMm)
            => MatchesPosition(pointMeters, (Vector3d)expectedMm);

        public static bool MatchesPosition(double[] pointMeters, Vector3d expectedMm)
        {
            if (pointMeters == null || pointMeters.Length < 3) return false;
            double dx = pointMeters[0] * 1000 - expectedMm.X;
            double dy = pointMeters[1] * 1000 - expectedMm.Y;
            double dz = pointMeters[2] * 1000 - expectedMm.Z;
            return dx * dx + dy * dy + dz * dz < 1e-16; // 1e-8 mm: plane angles depend on these points.
        }
    }
}
