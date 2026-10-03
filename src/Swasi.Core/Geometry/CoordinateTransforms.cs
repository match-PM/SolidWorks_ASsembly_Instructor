using System;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>Legacy SWASI matrices store millimeter translation in M14/M24/M34.
    /// Do not pass these matrices directly to Vector3.Transform (which uses row vectors).</summary>
    public static class CoordinateTransforms
    {
        public const float MillimetersPerMeter = 1000f;
        public static Vector3 ToEulerXyz(Quaternion value)
        {
            var angles = ToEulerXyzDouble(value);
            return new Vector3((float)angles[0], (float)angles[1], (float)angles[2]);
        }

        public static double[] ToEulerXyzDouble(Quaternion value)
            => ToEulerXyzDouble((Quaterniond)value);

        public static double[] ToEulerXyzDouble(Quaterniond value)
        {
            double length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y
                + (double)value.Z * value.Z + (double)value.W * value.W);
            if (length < 1e-12 || double.IsNaN(length) || double.IsInfinity(length))
                throw new ArgumentException("Rotation must be a finite, nonzero quaternion.", nameof(value));
            double x = value.X / length, y = value.Y / length, z = value.Z / length, w = value.W / length;
            // SOLIDWORKS numerical coordinate systems use intrinsic XYZ:
            // the active column-vector matrix is Rx * Ry * Rz.
            double r11 = 1 - 2 * (y * y + z * z), r12 = 2 * (x * y - w * z);
            double sinY = 2 * (w * y + z * x);
            double cosY = Math.Sqrt(r11 * r11 + r12 * r12);
            // At +/-90 degrees pitch, roll and yaw are coupled. Choose yaw=0
            // and recover their combined angle instead of using atan2(0, 0).
            if (cosY < 1e-14)
                return new[] { Math.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + z * z)),
                    Math.Atan2(sinY, cosY), 0d };
            return new[] { Math.Atan2(2 * (w * x - y * z), 1 - 2 * (x * x + y * y)),
                Math.Atan2(sinY, cosY), Math.Atan2(-r12, r11) };
        }

        public static bool CadPoseMatches(double[] actual, Vector3 positionMm, Quaternion rotation)
            => CadPoseMatches(actual, (Vector3d)positionMm, (Quaterniond)rotation);

        public static bool CadPoseMatches(double[] actual, Vector3d positionMm, Quaterniond rotation)
        {
            if (actual == null || actual.Length < 12) return false;
            double norm = Math.Sqrt((double)rotation.X * rotation.X + (double)rotation.Y * rotation.Y
                + (double)rotation.Z * rotation.Z + (double)rotation.W * rotation.W);
            if (!(norm > 0) || double.IsInfinity(norm)) return false;
            double x = rotation.X / norm, y = rotation.Y / norm, z = rotation.Z / norm, w = rotation.W / norm;
            // CAD arrays store the three physical basis vectors consecutively.
            var expected = new[] {
                1 - 2 * (y*y + z*z), 2 * (x*y + w*z), 2 * (x*z - w*y),
                2 * (x*y - w*z), 1 - 2 * (x*x + z*z), 2 * (y*z + w*x),
                2 * (x*z + w*y), 2 * (y*z - w*x), 1 - 2 * (x*x + y*y),
                positionMm.X / 1000d, positionMm.Y / 1000d, positionMm.Z / 1000d };
            for (int i = 0; i < expected.Length; i++)
                if (!(Math.Abs(actual[i] - expected[i]) <= 1e-11)) return false;
            return true;
        }
        public static Matrix4x4 ToMatrix(Vector3 translation, Quaternion rotation)
        {
            var matrix = Matrix4x4.CreateFromQuaternion(rotation);
            matrix.M14 = translation.X;
            matrix.M24 = translation.Y;
            matrix.M34 = translation.Z;
            return matrix;
        }
        public static Vector3 GetTranslation(Matrix4x4 matrix) => new Vector3(matrix.M14, matrix.M24, matrix.M34);
        public static Vector3 TransformPlaneNormal(Vector3 normal, Matrix4x4 relativeTransform)
        {
            // Preserve the existing SWASI rotation convention; directions have no translation.
            var transformed = Vector4.Transform(new Vector4(normal, 0f), Invert(relativeTransform));
            return new Vector3(transformed.X, transformed.Y, transformed.Z);
        }
        public static Matrix4x4 Invert(Matrix4x4 matrix)
        {
            if (!Matrix4x4.Invert(matrix, out var inverse))
                throw new ArgumentException("Coordinate transform is singular.", nameof(matrix));
            return inverse;
        }
        public static Matrix4x4 FromCadArray(double[] values)
        {
            if (values == null || values.Length < 12)
                throw new ArgumentException("A CAD transform requires at least 12 values.", nameof(values));
            return new Matrix4x4(
                (float)values[0], (float)values[3], (float)values[6], (float)values[9] * MillimetersPerMeter,
                (float)values[1], (float)values[4], (float)values[7], (float)values[10] * MillimetersPerMeter,
                (float)values[2], (float)values[5], (float)values[8], (float)values[11] * MillimetersPerMeter,
                0, 0, 0, 1);
        }

        public static Matrix4x4 CoordinateSystemPoseFromCadArray(double[] values)
        {
            var pose = PoseFromColumnMatrix(FromCadArray(values));
            return ToMatrix(pose.translation, pose.rotation);
        }

        public static CoordinateSystemDescription PoseFromColumnMatrix(Matrix4x4 matrix)
        {
            // CAD/export composition uses column-vector matrices. Numerics
            // quaternions expect a row-vector rotation matrix. Transpose only
            // the rotation convention; the physical position is unchanged.
            return new CoordinateSystemDescription(GetTranslation(matrix),
                Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix4x4.Transpose(matrix))));
        }
    }
}
