using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
        public class RefFrameDescription
        {
            public string name;
            public string type;
            public CoordinateSystemDescription transformation;

            public RefFrameDescription()
            {
                name = string.Empty;
                type = string.Empty;
                transformation = new CoordinateSystemDescription();
            }
            public RefFrameDescription(string _type)
            {
                name = string.Empty;
                type = _type;
                transformation = new CoordinateSystemDescription();
            }
    }
}