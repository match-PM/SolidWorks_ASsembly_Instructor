using System;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>Legacy SWASI matrices store millimeter translation in M14/M24/M34.
    /// Do not pass these matrices directly to Vector3.Transform (which uses row vectors).</summary>
    public static class CoordinateTransforms
    {
        public const float MillimetersPerMeter = 1000f;
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
    }
}
