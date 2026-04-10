namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameDescription
        {
            public string name;
            public string type;
            public CoordinateSystemDescription transformation;
            public RefFrameConstraints constraints;
            public RefFrameproperties properties;

        public RefFrameDescription()
            {
                name = string.Empty;
                type = string.Empty;
                transformation = new CoordinateSystemDescription();
                constraints = new RefFrameConstraints();
                properties = new RefFrameproperties();
            }
            public RefFrameDescription(string _type)
            {
                name = string.Empty;
                type = _type;
                transformation = new CoordinateSystemDescription();
                constraints = new RefFrameConstraints();
                properties = new RefFrameproperties();
            }
    }
}