namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyComponentDescription
    {
        public string name;

        public string type;

        public CoordinateSystemDescription transformation;

        public string guid;
        public System.Collections.Generic.Dictionary<string, RefFrameProperties> frameProperties =
            new System.Collections.Generic.Dictionary<string, RefFrameProperties>(System.StringComparer.Ordinal);
        public bool ShouldSerializeframeProperties() => frameProperties.Count > 0;

        public AssemblyComponentDescription()
        {
            name = string.Empty;
            type = string.Empty;
            transformation = new CoordinateSystemDescription();
            guid = string.Empty;
        }

    }
}
