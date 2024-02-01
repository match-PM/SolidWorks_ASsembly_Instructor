using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    public class ComponentDescription
    {
        public string name { get; set; }

        public string description { get; set; }

        public Guid guid { get; set; }

        public string type { get; set; }

        public DateTime saveDate { get; set; }

        public MountingDescription mountingDescription;

        public string documentUnits;

        public string cadPath;
        //public DateTime CreationDateTime { get; set; }

        //public DateTime LastModifiedDateTime { get; set; }


        public ComponentDescription()
        {
            saveDate = DateTime.Now;
            guid = Guid.NewGuid();
            description = string.Empty;
            mountingDescription = new MountingDescription();
            type = "Component";
            documentUnits = "mm";
            cadPath = string.Empty;   
        }

    }

}
