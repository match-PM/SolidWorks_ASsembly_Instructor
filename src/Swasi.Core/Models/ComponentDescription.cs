using System;

namespace SolidWorks_ASsembly_Instructor
{

    public class ColorComp
            {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public ColorComp()
        {
            R = 0;
            G = 0;
            B = 0;
        }
        public ColorComp(int red, int green, int blue)
        {
            R = red;
            G = green;
            B = blue;
        }
    }

    public class ComponentDescription
    {
        public string name { get; set; }

        public string description { get; set; }

        public Guid guid { get; set; }

        public string type { get; set; }

        public DateTime saveDate { get; set; }

        public MountingDescription mountingDescription;

        public string documentUnits;

        public ColorComp color;

        public string cadPath;

        public string cadPathCollision;

        public ComponentDescription()
        {
            saveDate = DateTime.Now;
            guid = Guid.NewGuid();
            description = string.Empty;
            cadPathCollision = string.Empty;
            mountingDescription = new MountingDescription();
            type = "Component";
            documentUnits = "mm";
            cadPath = string.Empty;
            color = new ColorComp(255, 255, 255); // Default color: white
        }

    }

}
