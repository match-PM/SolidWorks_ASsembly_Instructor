using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    public class CoordinateSystemDescription
    {
        public string name { get; set; }
        public Vector3 translation;
        public Quaternion rotation;

        public CoordinateSystemDescription() : this(Vector3.Zero, Quaternion.Identity) { }
        public CoordinateSystemDescription(Vector3 translation, Quaternion rotation)
        { this.translation = translation; this.rotation = rotation; }
        public CoordinateSystemDescription(Matrix4x4 matrix) { FromMatrix4x4(matrix); }
        public Matrix4x4 AsMatrix4x4() => CoordinateTransforms.ToMatrix(translation, rotation);
        public void FromMatrix4x4(Matrix4x4 matrix)
        {
            translation = CoordinateTransforms.GetTranslation(matrix);
            rotation = Quaternion.CreateFromRotationMatrix(matrix);
        }
        public Matrix4x4 GetInverted4x4Matrix() => CoordinateTransforms.Invert(AsMatrix4x4());
        public override string ToString() => $"CS: {name}, X: {translation.X}, Y: {translation.Y}, Z: {translation.Z}, Q_w: {rotation.W}, Q_x: {rotation.X}, Q_y: {rotation.Y}, Q_z: {rotation.Z}";
    }
}
