using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyDescription
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public Guid GUID { get; set; }

        public string Type { get; set; }

        public DateTime saveDate { get; set; }

        public CoordinateSystemDescription origin { get; set; }

        public List<ChildrenDescription> children { get; set; }

        //public DateTime CreationDateTime { get; set; }

        //public DateTime LastModifiedDateTime { get; set; }


        public AssemblyDescription()
        {
            saveDate = DateTime.Now;
            GUID = Guid.NewGuid();
            children = new List<ChildrenDescription>();
            Description = string.Empty;
        }
    }

    public class ChildrenDescription
    {
        public string Name { get; set; }
        public Guid GUID { get; set; }
        public string Type { get; set; }

        public List<ChildrenDescription> children { get; set; }
        public CoordinateSystemDescription originTransform { get; set; }

        public CoordinateSystemDescription assemblyTransform { get; set; }

        public ChildrenDescription()
        {
            Name = "Subpart blubb";
            GUID = Guid.NewGuid();
            originTransform = new CoordinateSystemDescription();
            assemblyTransform = new CoordinateSystemDescription();
            children = new List<ChildrenDescription>();
        }

        public AssemblyDescription ToMainAssambly()
        {
            return new AssemblyDescription { Name = Name, GUID = GUID, Type = Type, children = children };
        }

        public void calcRelativeCoordinates(Vector3 parentTranslation, Quaternion parentQuaternion, Vector3 partTranslation, Quaternion partQuaternion)
        {

        }

    }
}
