using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace SolidWorks_ASsembly_Instructor
{
    public class PlaneMateDescription
    {
        public string name;

        public PlaneMatchDescription planeMatch_1;
        public PlaneMatchDescription planeMatch_2;
        public PlaneMatchDescription planeMatch_3;

        public PlaneMateDescription()
        {
            planeMatch_1= new PlaneMatchDescription();
            planeMatch_2 = new PlaneMatchDescription();
            planeMatch_3 = new PlaneMatchDescription();
        }

        public (int, bool) SetPlaneMatch(string PlaneName_1, string PlaneName_2)
        {
            // Check if the plane names are not yet set.
            if (planeMatch_1.planeNameComponent_1 == "")
            {
                planeMatch_1.planeNameComponent_1 = PlaneName_1;
                planeMatch_1.planeNameComponent_2 = PlaneName_2;
                return (1, true);
            }
            else if (planeMatch_2.planeNameComponent_1 == "")
            {
                planeMatch_2.planeNameComponent_1 = PlaneName_1;
                planeMatch_2.planeNameComponent_2 = PlaneName_2;
                return (2, true);
            }
            else if (planeMatch_3.planeNameComponent_1 == "")
            {
                planeMatch_3.planeNameComponent_1 = PlaneName_1;
                planeMatch_3.planeNameComponent_2 = PlaneName_2;
                return (3, true);
            }
            return (0, false);
        }

        public bool CheckPlaneComplet()
        {
            if ((!(planeMatch_1.planeNameComponent_1 == "")) &&
                (!(planeMatch_1.planeNameComponent_2 == "")) &&
                (!(planeMatch_2.planeNameComponent_1 == "")) &&
                (!(planeMatch_2.planeNameComponent_2 == "")) &&
                (!(planeMatch_3.planeNameComponent_1 == "")) &&
                (!(planeMatch_3.planeNameComponent_2 == "")) &&
                (!(name == "")))
            {  
                return true; 
            }
            else
            {
                return false;
            }
        }
    }
}