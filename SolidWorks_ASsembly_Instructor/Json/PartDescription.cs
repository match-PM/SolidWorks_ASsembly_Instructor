using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Numerics;
using System.Collections.Specialized;
using System.Security.Policy;
using SolidWorks.Interop.sldworks;
using System.ComponentModel;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    public class PartDescription
    {
        public string name { get; set; }
        public Guid guid { get; set; }
        public string type { get; set; }
        public DateTime creationDateTime { get; set; }
        public DateTime lastModifiedDateTime { get; set; }
        //public Vector3 WorldCoordinates { get; set; }
        //public Vector3 relativeCoordinates { get; set; }
        //public Coordinatesystem WorldCoordinatesystem { get; set; }
        //public Coordinatesystem relativeCoordinateSystem { get; set; }

        //public Vector3 Position { get; set; }


        //ToDo: noch auslesen. Bekommen über
        //string test = feature.GetTypeName();
        //logDebug("Feature Name: " + feature.GetTypeName());

        //RefAxis
        //RefPlane
        //RefPoint



        public ParentDescription parent;

        public List<CoordinateSystemDescription> coordinanteSystems { get; set; }


        public PartDescription()
        {
            coordinanteSystems = new List<CoordinateSystemDescription>();
            //WorldCoordinatesystem = new Coordinatesystem();
            //relativeCoordinateSystem = new Coordinatesystem();
            creationDateTime = DateTime.Now;
            guid = Guid.NewGuid();
        }




    }
    public class PartDescriptionShort
    {
        public string name { get; set; }
        public Guid guid { get; set; }
        public CoordinateSystemDescription relativeCoordinates { get; set; }

        public PartDescriptionShort()
        {
            name = "Subpart blubb";
            guid = Guid.NewGuid();
            relativeCoordinates = new CoordinateSystemDescription();

        }

        public void calcRelativeCoordinates(Vector3 parentTranslation, Quaternion parentQuaternion, Vector3 partTranslation, Quaternion partQuaternion)
        {

        }

    }

}
