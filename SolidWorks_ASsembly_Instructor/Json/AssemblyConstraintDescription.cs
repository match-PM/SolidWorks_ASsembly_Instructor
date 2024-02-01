using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyConstraintDescription
    {
        public string name;
        public string component_1;
        public string component_2;
        public bool moveComponent_1;
        public PlaneMateDescription description;

        public AssemblyConstraintDescription()
        {
            name = "";
            component_1 = "";
            component_2 = "";
            moveComponent_1 = false;
            description = new PlaneMateDescription();
        }
        public bool CheckAssemblyConstraintComplet()
        {
            if ((component_1 != "") &&
                (component_2 != "") &&
                description.CheckPlaneComplet())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool SetComponent(string ComponentName)
        {
            if (component_1 == "") 
            {
                component_1 = ComponentName;
                return true; 
            }
            else if (component_2 == "")
            {
                component_2 = ComponentName;
                return true;
            }
            else if(ComponentName == component_1)
            {
                return true;
            }
            else if (ComponentName == component_2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        public bool SetName()
        {
            if(CheckAssemblyConstraintComplet())
            {
                name = $"Description_{component_1}_{component_2}";
                return true;
            }
            else
            { 
                return false;
            }
        }
    }
}