using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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