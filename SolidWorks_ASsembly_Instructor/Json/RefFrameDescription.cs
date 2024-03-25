namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameDescription
        {
            public string name;
            public string type;
            public CoordinateSystemDescription transformation;
            public RefFrameConstraints constraints;

            public RefFrameDescription()
            {
                name = string.Empty;
                type = string.Empty;
                transformation = new CoordinateSystemDescription();
                constraints = new RefFrameConstraints();
            }
            public RefFrameDescription(string _type)
            {
                name = string.Empty;
                type = _type;
                transformation = new CoordinateSystemDescription();
                constraints = new RefFrameConstraints();
            }
    }
}