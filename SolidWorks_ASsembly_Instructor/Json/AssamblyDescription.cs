using System;
using System.Collections.Generic;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyDescription
    {
        public string name { get; set; }

        public string description { get; set; }

        public Guid guid { get; set; }

        public string type { get; set; }

        public DateTime saveDate { get; set; }

        public CoordinateSystemDescription origin { get; set; }

        public List<ChildrenDescription> children { get; set; }

        public MountingDescription mountingDescription;

        public string documentUnits;

        public string cadPath;

        //public DateTime CreationDateTime { get; set; }

        //public DateTime LastModifiedDateTime { get; set; }


        public AssemblyDescription()
        {
            saveDate = DateTime.Now;
            guid = Guid.NewGuid();
            description = string.Empty;
            mountingDescription = new MountingDescription();
            type = "Assembly";
            documentUnits = "mm";
            cadPath = string.Empty;
        }

    }

    public class ChildrenDescription
    {
        public string name { get; set; }
        public Guid guid { get; set; }
        public string type { get; set; }

        public List<ChildrenDescription> children { get; set; }
        public CoordinateSystemDescription originTransform { get; set; }

        public CoordinateSystemDescription assemblyTransform { get; set; }

        public ChildrenDescription()
        {
            name = "Subpart blubb";
            guid = Guid.NewGuid();
            originTransform = new CoordinateSystemDescription();
            assemblyTransform = new CoordinateSystemDescription();
            children = new List<ChildrenDescription>();
        }

        public void calcRelativeCoordinates(Vector3 parentTranslation, Quaternion parentQuaternion, Vector3 partTranslation, Quaternion partQuaternion)
        {

        }

    }
}
