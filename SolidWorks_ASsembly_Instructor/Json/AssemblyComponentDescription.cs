using System.Collections.Generic;
using System;

namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyComponentDescription
    {
        public string name;

        public string type;

        public CoordinateSystemDescription transformation;

        public string guid;


        public AssemblyComponentDescription()
        {
            name = string.Empty;
            type = string.Empty;
            transformation = new CoordinateSystemDescription();
            guid = string.Empty;
        }


    }
}