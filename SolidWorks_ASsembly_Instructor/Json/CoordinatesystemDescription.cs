using System.Numerics;

namespace JsonExporter_2
{
    public class CoordinateSystemDescription
    {
        public CoordinateSystemDescription(Vector3 translation, Quaternion rotation, int iD = -99)
        {
            Id = iD;
            this.Translation = translation;
            this.Rotation = rotation;
        }

        public CoordinateSystemDescription()
        {
            Id = -99;
            Translation = Vector3.Zero;
            Rotation = Quaternion.Identity;
        }

        public CoordinateSystemDescription(Vector3 translation, Vector3 rotation, int iD = -99)
        {
            Id = iD;
            Translation = translation;
            Rotation = Quaternion.CreateFromAxisAngle(rotation, 0f);
        }
        public CoordinateSystemDescription(Matrix4x4 matrix)
        {
            fromMatrix4x4(matrix);
            Id = -99;
        }

        public Matrix4x4 AsMatrix4x4()
        {
            Matrix4x4 TransformationMatrix = new Matrix4x4();

            TransformationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            TransformationMatrix.Translation = Translation / 1000;


            return TransformationMatrix;
        }

        public void fromMatrix4x4(Matrix4x4 TransformationMatrix)
        {
            Translation = TransformationMatrix.Translation * 1000f;
            Rotation = Quaternion.CreateFromRotationMatrix(TransformationMatrix);
        }

        public string Name { get; set; }
        public int Id { get; set; }
        public Vector3 Translation { get; set; }
        public Quaternion Rotation { get; set; }



    }


}
