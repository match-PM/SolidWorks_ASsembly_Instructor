namespace SolidWorks_ASsembly_Instructor
{
    public class RefVisionFrameDescription : RefFrameDescription
    {
        // New property as requested (name spelled exactly as provided)
        public object properteis { get; set; }

        public RefVisionFrameDescription()
            : base()
        {
            properteis = null;
        }

        public RefVisionFrameDescription(string _type)
            : base(_type)
        {
            properteis = null;
        }
    }
}
