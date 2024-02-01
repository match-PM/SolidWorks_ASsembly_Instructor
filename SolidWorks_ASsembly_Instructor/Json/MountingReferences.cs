using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace SolidWorks_ASsembly_Instructor
{
    public class MountingReferences
    {
        public string spawningOrigin;
        public CoordinateSystemDescription spawningTransformation;
        public List<RefPlaneDescription> ref_planes;
        public List<RefAxisDescription> ref_axes;
        public List<RefFrameDescription> ref_frames;


        public MountingReferences()
        {
            spawningOrigin = string.Empty;
            spawningTransformation = new CoordinateSystemDescription();
            ref_planes = new List<RefPlaneDescription>();
            ref_axes = new List<RefAxisDescription>();
            ref_frames = new List<RefFrameDescription>();
        }

    }
}
