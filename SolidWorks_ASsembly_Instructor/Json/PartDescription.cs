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

namespace JsonExporter_2
{
    public class PartDescription
    {
        public string Name { get; set; }
        public Guid GUID { get; set; }
        public string Type { get; set; }
        public DateTime CreationDateTime { get; set; }
        public DateTime LastModifiedDateTime { get; set; }
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

        public List<CoordinateSystemDescription>CoordinanteSystems { get; set; }


        public PartDescription()
        {
            CoordinanteSystems = new List<CoordinateSystemDescription>();
            //WorldCoordinatesystem = new Coordinatesystem();
            //relativeCoordinateSystem = new Coordinatesystem();
            CreationDateTime = DateTime.Now;
            GUID = Guid.NewGuid();
        }




    }
    public class PartDescriptionShort
    {
        public string Name { get; set; }
        public Guid GUID { get; set; }
        public CoordinateSystemDescription relativeCoordinates { get; set; }

        public PartDescriptionShort()
        {
            Name = "Subpart blubb";
            GUID = Guid.NewGuid();
            relativeCoordinates = new CoordinateSystemDescription();

        }

        public void calcRelativeCoordinates(Vector3 parentTranslation, Quaternion parentQuaternion, Vector3 partTranslation, Quaternion partQuaternion)
        {

        }

    }

}
