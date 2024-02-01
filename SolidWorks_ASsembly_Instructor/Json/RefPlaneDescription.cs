using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefPlaneDescription
    {
        public string name;

        public List<string> refPointNames;

        public List<string> refAxisNames;

        public Vector3 normalVector;

        public RefPlaneDescription()
        {
            refPointNames = new List<string>() {"", "", "" };
            refAxisNames = new List<string>() { "", ""};
            normalVector = new Vector3();
            name = "";
        }
    }
}