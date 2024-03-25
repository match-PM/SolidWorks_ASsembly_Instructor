using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    public class CoordinateSystemDescription
    {
        public string name { get; set; }
        //public int Id { get; set; }
        public Vector3 translation;
        public Quaternion rotation;

        public CoordinateSystemDescription(Vector3 translation, Quaternion _rotation, int iD = -99)
        {
            //Id = iD;
            this.translation = translation;
            this.rotation = _rotation;
        }

        public CoordinateSystemDescription()
        {
            //Id = -99;
            translation = Vector3.Zero;
            rotation = Quaternion.Identity;
        }

        public CoordinateSystemDescription(Vector3 _translation, Vector3 _rotation, int iD = -99)
        {
            //Id = iD;
            translation = _translation;
            rotation = Quaternion.CreateFromAxisAngle(_rotation, 0f);
        }
        public CoordinateSystemDescription(Matrix4x4 matrix)
        {
            fromMatrix4x4(matrix);
            //Id = -99;
        }

        public Matrix4x4 AsMatrix4x4()
        {
            Matrix4x4 TransformationMatrix = new Matrix4x4();

            TransformationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
            TransformationMatrix.M14 = translation.X;
            TransformationMatrix.M24 = translation.Y;
            TransformationMatrix.M34 = translation.Z;
            TransformationMatrix.M44 = 1.0f;

            return TransformationMatrix;
        }

        public void fromMatrix4x4(Matrix4x4 TransformationMatrix)
        {
            //translation = TransformationMatrix.translation * 1000f; // Not working
            translation.X = TransformationMatrix.M14;
            translation.Y = TransformationMatrix.M24;
            translation.Z = TransformationMatrix.M34;

            rotation = Quaternion.CreateFromRotationMatrix(TransformationMatrix);
        }

        public void fromArrayData(dynamic ArrayData)
        {
            //!!!!!!!!! This function is not working, but i dont know why!!!!!!!!!!!
            //Matrix4x4 TransformationMatrix = new Matrix4x4(
            //           (float)ArrayData[0], (float)ArrayData[1], (float)ArrayData[2], (float)ArrayData[9] * 1000f,
            //           (float)ArrayData[3], (float)ArrayData[4], (float)ArrayData[5], (float)ArrayData[10] * 1000f,
            //           (float)ArrayData[6], (float)ArrayData[7], (float)ArrayData[8], (float)ArrayData[11] * 1000f,
            //           0.0f, 0.0f, 0.0f, 1.0f);
            Matrix4x4 TransformationMatrix = new Matrix4x4(
                       (float)ArrayData[0], (float)ArrayData[3], (float)ArrayData[6], (float)ArrayData[9] * 1000f,
                       (float)ArrayData[1], (float)ArrayData[4], (float)ArrayData[7], (float)ArrayData[10] * 1000f,
                       (float)ArrayData[2], (float)ArrayData[5], (float)ArrayData[8], (float)ArrayData[11] * 1000f,
                       0.0f, 0.0f, 0.0f, 1.0f);
            fromMatrix4x4(TransformationMatrix);
        }

        public Matrix4x4 GetInverted4x4Matrix() 
        {
            Matrix4x4 InitialMatirx = this.AsMatrix4x4();
            Matrix4x4 Inverse;
            bool ConvSuccess = Matrix4x4.Invert(InitialMatirx, out Inverse);
            return Inverse;
        }

        public override string ToString()
        {
            string StringDescription = $"CS: {name}, X: {translation.X}, Y: {translation.Y}, Z: {translation.Z}, Q_w: {rotation.W}, Q_x: {rotation.X}, Q_y: {rotation.Y}, Q_z: {rotation.Z}";

            return StringDescription;
        }



    }


}
