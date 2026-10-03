using System;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    // CAD calculations must not pass through System.Numerics' single-precision
    // types. Legacy JSON/export models are converted only at their boundary.
    public struct Vector3d
    {
        public double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vector3d Zero => new Vector3d();
        public static Vector3d UnitX => new Vector3d(1, 0, 0);
        public static Vector3d UnitY => new Vector3d(0, 1, 0);
        public static Vector3d UnitZ => new Vector3d(0, 0, 1);
        public static implicit operator Vector3d(Vector3 v) => new Vector3d(v.X, v.Y, v.Z);
        public Vector3 ToLegacy() => new Vector3((float)X, (float)Y, (float)Z);
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
        public static Vector3d operator -(Vector3d a) => a * -1;
        public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.X*s, a.Y*s, a.Z*s);
        public static Vector3d operator *(double s, Vector3d a) => a * s;
        public static Vector3d operator /(Vector3d a, double s) => a * (1 / s);
        public double LengthSquared() => Dot(this, this);
        public double Length() => Math.Sqrt(LengthSquared());
        public static double Dot(Vector3d a, Vector3d b) => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
        public static Vector3d Cross(Vector3d a, Vector3d b) =>
            new Vector3d(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);
        public static Vector3d Normalize(Vector3d v)
        {
            double length = v.Length();
            if (!(length > 0) || double.IsInfinity(length)) throw new ArgumentException("Invalid direction vector.");
            return v / length;
        }
        public static Vector3d Transform(Vector3d v, Quaterniond rotation)
        {
            var q = Quaterniond.Normalize(rotation);
            var xyz = new Vector3d(q.X, q.Y, q.Z);
            var t = 2 * Cross(xyz, v);
            return v + q.W * t + Cross(xyz, t);
        }
    }

    public struct Quaterniond
    {
        public double X, Y, Z, W;
        public Quaterniond(double x, double y, double z, double w) { X=x; Y=y; Z=z; W=w; }
        public static Quaterniond Identity => new Quaterniond(0, 0, 0, 1);
        public static implicit operator Quaterniond(Quaternion q) => new Quaterniond(q.X, q.Y, q.Z, q.W);
        public Quaternion ToLegacy() => new Quaternion((float)X, (float)Y, (float)Z, (float)W);
        public double LengthSquared() => X*X + Y*Y + Z*Z + W*W;
        public static Quaterniond Normalize(Quaterniond q)
        {
            double length = Math.Sqrt(q.LengthSquared());
            if (!(length > 0) || double.IsInfinity(length)) throw new ArgumentException("Invalid rotation quaternion.");
            return new Quaterniond(q.X/length, q.Y/length, q.Z/length, q.W/length);
        }
        public static Quaterniond Inverse(Quaterniond q)
        {
            q = Normalize(q);
            return new Quaterniond(-q.X, -q.Y, -q.Z, q.W);
        }
        public static Quaterniond operator *(Quaterniond a, Quaterniond b) => new Quaterniond(
            a.W*b.X + a.X*b.W + a.Y*b.Z - a.Z*b.Y,
            a.W*b.Y - a.X*b.Z + a.Y*b.W + a.Z*b.X,
            a.W*b.Z + a.X*b.Y - a.Y*b.X + a.Z*b.W,
            a.W*b.W - a.X*b.X - a.Y*b.Y - a.Z*b.Z);

        public static Quaterniond FromAxes(Vector3d x, Vector3d y, Vector3d z)
        {
            double trace = x.X + y.Y + z.Z;
            Quaterniond q;
            if (trace > 0)
            {
                double s = Math.Sqrt(trace + 1) * 2;
                q = new Quaterniond((y.Z-z.Y)/s, (z.X-x.Z)/s, (x.Y-y.X)/s, s/4);
            }
            else if (x.X > y.Y && x.X > z.Z)
            {
                double s = Math.Sqrt(1 + x.X - y.Y - z.Z) * 2;
                q = new Quaterniond(s/4, (y.X+x.Y)/s, (z.X+x.Z)/s, (y.Z-z.Y)/s);
            }
            else if (y.Y > z.Z)
            {
                double s = Math.Sqrt(1 + y.Y - x.X - z.Z) * 2;
                q = new Quaterniond((y.X+x.Y)/s, s/4, (z.Y+y.Z)/s, (z.X-x.Z)/s);
            }
            else
            {
                double s = Math.Sqrt(1 + z.Z - x.X - y.Y) * 2;
                q = new Quaterniond((z.X+x.Z)/s, (z.Y+y.Z)/s, s/4, (x.Y-y.X)/s);
            }
            return Normalize(q);
        }
    }

    public sealed class PrecisePose
    {
        public string name;
        public Vector3d translation;
        public Quaterniond rotation;
        public PrecisePose() : this(Vector3d.Zero, Quaterniond.Identity) { }
        public PrecisePose(Vector3d translation, Quaterniond rotation) { this.translation = translation; this.rotation = rotation; }
        public static PrecisePose FromLegacy(CoordinateSystemDescription value) =>
            value == null ? null : new PrecisePose(value.translation, value.rotation) { name = value.name };
        public CoordinateSystemDescription ToLegacy() =>
            new CoordinateSystemDescription(translation.ToLegacy(), rotation.ToLegacy()) { name = name };
        public static PrecisePose FromCadArray(double[] a)
        {
            if (a == null || a.Length < 12) throw new ArgumentException("A CAD transform requires at least 12 values.");
            return new PrecisePose(new Vector3d(a[9]*1000, a[10]*1000, a[11]*1000),
                Quaterniond.FromAxes(new Vector3d(a[0],a[1],a[2]), new Vector3d(a[3],a[4],a[5]), new Vector3d(a[6],a[7],a[8])));
        }
    }
}
