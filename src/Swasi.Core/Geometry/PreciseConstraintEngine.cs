using System;
using System.Collections.Generic;
using System.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    public sealed class PreciseConstraintEngine
    {
        private const double Epsilon = 1e-5;

        public PrecisePose Centroid(
            IReadOnlyList<PrecisePose> references,
            Vector3d localOffset)
        {
            if (references == null || references.Count == 0)
                throw new ArgumentException("A centroid needs at least one reference frame.", nameof(references));

            PrecisePose basis;
            if (references.Count == 1)
            {
                basis = Clone(references[0]);
            }
            else
            {
                var centroid = Average(references.Select(r => r.translation));
                basis = new PrecisePose(centroid, PcaOrientation(references.Select(r => r.translation).ToArray()));
            }

            basis.translation += Vector3d.Transform(localOffset, basis.rotation);
            return basis;
        }

        public PrecisePose Orthogonal(
            PrecisePose frame1,
            PrecisePose frame2,
            PrecisePose frame3,
            double distanceFromFrame1,
            bool distanceIsPercent,
            double orthogonalDistance,
            string orthogonalAxis,
            string normalAxis)
        {
            if (frame1 == null || frame2 == null || frame3 == null)
                throw new ArgumentNullException("Orthogonal constraint references cannot be null.");

            Vector3d connection = frame2.translation - frame1.translation;
            if (connection.LengthSquared() < Epsilon * Epsilon)
                throw new ArgumentException("The first and second reference frames have the same position.");
            Vector3d connectionUnit = Vector3d.Normalize(connection);
            Vector3d normal = Vector3d.Cross(connection, frame3.translation - frame1.translation);
            if (normal.LengthSquared() < Epsilon * Epsilon)
                throw new ArgumentException("The three orthogonal references are collinear.");
            normal = Vector3d.Normalize(normal);

            string normalSlot = NormalizeAxis(normalAxis, out bool flipNormal);
            // Inputs are expressed in SWASI-origin coordinates by the overload
            // below. Select the normal's hemisphere using the requested axis.
            int normalIndex = normalSlot == "x" ? 0 : normalSlot == "y" ? 1 : 2;
            double direction = GetComponent(normal, normalIndex);
            // A perpendicular normal has no positive projection on that axis;
            // retain the ROS handler's deterministic dominant-component fallback.
            if (Math.Abs(direction) < Epsilon) direction = GetComponent(normal, DominantIndex(normal));
            if (direction < 0) normal = -normal;
            if (flipNormal) normal = -normal;
            string orthogonalSlot = NormalizeAxis(orthogonalAxis, out bool flipOrthogonal);
            if (orthogonalSlot == normalSlot)
                throw new ArgumentException("Normal and orthogonal axes must be different.");

            Vector3d orthogonal = Vector3d.Normalize(Vector3d.Cross(connectionUnit, normal));
            if (flipOrthogonal) orthogonal = -orthogonal;
            Vector3d position = frame1.translation
                + (distanceIsPercent ? connection * (distanceFromFrame1 / 100d) : connectionUnit * distanceFromFrame1)
                + orthogonal * orthogonalDistance;

            var axes = new Dictionary<string, Vector3d>
            {
                [normalSlot] = normal,
                [orthogonalSlot] = orthogonal
            };
            string remaining = new[] { "x", "y", "z" }.Single(a => !axes.ContainsKey(a));
            axes[remaining] = connectionUnit;
            if (Determinant(axes["x"], axes["y"], axes["z"]) < 0)
                axes[orthogonalSlot] = -axes[orthogonalSlot];

            return new PrecisePose(position, QuaternionFromAxes(axes["x"], axes["y"], axes["z"]));
        }

        public PrecisePose Orthogonal(
            PrecisePose frame1,
            PrecisePose frame2,
            PrecisePose frame3,
            double distanceFromFrame1,
            bool distanceIsPercent,
            double orthogonalDistance,
            string orthogonalAxis,
            string normalAxis,
            PrecisePose axisReference)
        {
            if (axisReference == null) throw new ArgumentNullException(nameof(axisReference));
            if (axisReference.rotation.LengthSquared() < Epsilon * Epsilon)
                throw new ArgumentException("The axis reference rotation cannot be zero.", nameof(axisReference));
            Quaterniond referenceRotation = Quaterniond.Normalize(axisReference.rotation);
            Quaterniond inverseRotation = Quaterniond.Inverse(referenceRotation);
            var localFrame1 = InReference(frame1, axisReference.translation, inverseRotation);
            var localFrame2 = InReference(frame2, axisReference.translation, inverseRotation);
            var localFrame3 = InReference(frame3, axisReference.translation, inverseRotation);
            var localResult = Orthogonal(localFrame1, localFrame2, localFrame3,
                distanceFromFrame1, distanceIsPercent, orthogonalDistance,
                orthogonalAxis, normalAxis);
            return Transform(new PrecisePose(axisReference.translation, referenceRotation), localResult);
        }

        public PrecisePose Transform(
            PrecisePose reference,
            PrecisePose localTransform)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (localTransform == null) throw new ArgumentNullException(nameof(localTransform));
            return new PrecisePose(
                reference.translation + Vector3d.Transform(localTransform.translation, reference.rotation),
                Quaterniond.Normalize(reference.rotation * localTransform.rotation));
        }

        public PreciseInPlaneValidationResult ValidateInPlane(
            PrecisePose candidate,
            IReadOnlyList<PrecisePose> references,
            double planeOffset,
            double tolerance)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (references == null || references.Count < 3)
                throw new ArgumentException("An in-plane check needs at least three references.", nameof(references));
            if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            Vector3d centroid = Average(references.Select(r => r.translation));
            Vector3d normal = SmallestEigenvector(Covariance(references.Select(r => r.translation).ToArray(), centroid));
            if (normal.LengthSquared() < Epsilon * Epsilon)
                throw new ArgumentException("The in-plane references do not define a plane.");
            normal = Vector3d.Normalize(normal);
            if (normal.Z < 0) normal = -normal;
            double signedDistance = Vector3d.Dot(candidate.translation - centroid, normal) - planeOffset;
            return new PreciseInPlaneValidationResult(signedDistance, Math.Abs(signedDistance) <= tolerance);
        }

        private static Quaterniond PcaOrientation(Vector3d[] points)
        {
            Vector3d centroid = Average(points);
            var matrix = Covariance(points, centroid);
            var eigen = Eigenvectors(matrix);
            Vector3d z = eigen[0];
            Vector3d xCandidate = eigen[2];
            if (z.Z < 0) z = -z;
            Vector3d x = xCandidate - Vector3d.Dot(xCandidate, z) * z;
            if (x.LengthSquared() < Epsilon * Epsilon)
                x = Perpendicular(z);
            x = Vector3d.Normalize(x);
            if (x.X < 0) x = -x;
            Vector3d y = Vector3d.Normalize(Vector3d.Cross(z, x));
            return QuaternionFromAxes(x, y, z);
        }

        private static Matrix3 Covariance(Vector3d[] points, Vector3d centroid)
        {
            if (points.Length < 2) throw new ArgumentException("At least two points are required.");
            var result = new Matrix3();
            foreach (Vector3d p in points)
            {
                Vector3d d = p - centroid;
                result.M00 += d.X * d.X; result.M01 += d.X * d.Y; result.M02 += d.X * d.Z;
                result.M11 += d.Y * d.Y; result.M12 += d.Y * d.Z; result.M22 += d.Z * d.Z;
            }
            double divisor = points.Length - 1;
            result.M00 /= divisor; result.M01 /= divisor; result.M02 /= divisor;
            result.M11 /= divisor; result.M12 /= divisor; result.M22 /= divisor;
            result.M10 = result.M01; result.M20 = result.M02; result.M21 = result.M12;
            return result;
        }

        private static Vector3d[] Eigenvectors(Matrix3 source)
        {
            double[,] a = source.ToArray();
            double[,] v = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int iteration = 0; iteration < 32; iteration++)
            {
                int p = 0, q = 1;
                if (Math.Abs(a[0, 2]) > Math.Abs(a[p, q])) { p = 0; q = 2; }
                if (Math.Abs(a[1, 2]) > Math.Abs(a[p, q])) { p = 1; q = 2; }
                if (Math.Abs(a[p, q]) <= 1e-15 * Math.Max(Math.Abs(a[0, 0]), Math.Max(Math.Abs(a[1, 1]), Math.Abs(a[2, 2])))) break;
                double angle = .5 * Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]);
                double c = Math.Cos(angle), s = Math.Sin(angle);
                for (int k = 0; k < 3; k++)
                {
                    double akp = a[k, p], akq = a[k, q];
                    a[k, p] = c * akp - s * akq; a[k, q] = s * akp + c * akq;
                }
                for (int k = 0; k < 3; k++)
                {
                    double apk = a[p, k], aqk = a[q, k];
                    a[p, k] = c * apk - s * aqk; a[q, k] = s * apk + c * aqk;
                    double vkp = v[k, p], vkq = v[k, q];
                    v[k, p] = c * vkp - s * vkq; v[k, q] = s * vkp + c * vkq;
                }
            }
            var order = Enumerable.Range(0, 3).OrderBy(i => a[i, i]).ToArray();
            return order.Select(i => Vector3d.Normalize(new Vector3d(v[0, i], v[1, i], v[2, i]))).ToArray();
        }

        private static Vector3d SmallestEigenvector(Matrix3 matrix) => Eigenvectors(matrix)[0];
        private static Vector3d Average(IEnumerable<Vector3d> points)
        {
            var values = points.ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one point is required.");
            Vector3d sum = Vector3d.Zero;
            foreach (var point in values) sum += point - values[0];
            return values[0] + sum / values.Length;
        }
        private static string NormalizeAxis(string axis, out bool flipped)
        {
            string value = (axis ?? string.Empty).Trim().ToLowerInvariant();
            flipped = value.StartsWith("-");
            if (flipped) value = value.Substring(1);
            if (value != "x" && value != "y" && value != "z")
                throw new ArgumentException("Axis must be x, y, z, -x, -y, or -z.");
            return value;
        }
        private static int DominantIndex(Vector3d value)
        {
            double x = Math.Abs(value.X), y = Math.Abs(value.Y), z = Math.Abs(value.Z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }
        private static double GetComponent(Vector3d value, int index) => index == 0 ? value.X : index == 1 ? value.Y : value.Z;
        private static double Determinant(Vector3d x, Vector3d y, Vector3d z) => Vector3d.Dot(x, Vector3d.Cross(y, z));
        private static Vector3d Perpendicular(Vector3d value) =>
            Vector3d.Normalize(Vector3d.Cross(value, Math.Abs(value.Z) < .9 ? Vector3d.UnitZ : Vector3d.UnitY));
        private static Quaterniond QuaternionFromAxes(Vector3d x, Vector3d y, Vector3d z)
        {
            return Quaterniond.FromAxes(x, y, z);
        }
        private static PrecisePose Clone(PrecisePose value) =>
            new PrecisePose(value.translation, value.rotation) { name = value.name };
        private static PrecisePose InReference(PrecisePose value,
            Vector3d referenceTranslation, Quaterniond inverseReferenceRotation)
        {
            if (value == null) throw new ArgumentNullException("Orthogonal constraint references cannot be null.");
            return new PrecisePose(
                Vector3d.Transform(value.translation - referenceTranslation, inverseReferenceRotation),
                Quaterniond.Identity);
        }

        private sealed class Matrix3
        {
            public double M00, M01, M02, M10, M11, M12, M20, M21, M22;
            public double[,] ToArray() => new[,] { { M00, M01, M02 }, { M10, M11, M12 }, { M20, M21, M22 } };
        }
    }

    public sealed class PreciseInPlaneValidationResult
    {
        public double SignedDistance { get; }
        public bool IsInPlane { get; }
        public PreciseInPlaneValidationResult(double signedDistance, bool isInPlane)
        { SignedDistance = signedDistance; IsInPlane = isInPlane; }
    }
}
