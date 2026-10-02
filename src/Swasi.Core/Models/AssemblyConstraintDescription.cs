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
        public bool CheckAssemblyConstraintComplete()
        {
            if ((component_1 != "") &&
                (component_2 != "") &&
                description.CheckPlaneComplete())
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

        public int GetComponentIndex(string ComponentName)
        {
            if (component_1 == ComponentName)
            {
                return 1;
            }
            else if (component_2 == ComponentName)
            {
                return 2;
            }
            else
            {
                return 0; // Not found
            }
        }

        public bool SetName()
        {
            if(CheckAssemblyConstraintComplete())
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
