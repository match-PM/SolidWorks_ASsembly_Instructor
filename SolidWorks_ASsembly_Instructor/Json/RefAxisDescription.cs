using System.Collections.Generic;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefAxisDescription
    {
        public string name;

        public List<string> refPointNames;

        public RefAxisDescription()
        {
            refPointNames = new List<string>() {"", ""};
            name = "";
        }
    }
}