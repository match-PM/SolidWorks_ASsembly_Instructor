using Newtonsoft.Json;

namespace SolidWorks_ASsembly_Instructor
{
    public class AssemblyConstraintDescription
    {
        public string name;
        public string type = "PlaneMatch";
        public string component_1;
        public string component_2;
        [JsonProperty("move_component_1")]
        public bool moveComponent_1;
        // Accept older plane exports while writing the snake_case field.
        [JsonProperty("moveComponent_1")]
        private bool LegacyMoveComponent1 { set { moveComponent_1 = value; } }
        public PlaneMateDescription description;
        public AssemblyFrameEndpoint assemblyFrame;
        public AssemblyFrameEndpoint targetFrame;
        public bool alignAxes = true;
        public bool ShouldSerializemoveComponent_1() => type == "PlaneMatch";
        public bool ShouldSerializedescription() => type == "PlaneMatch";
        public bool ShouldSerializeassemblyFrame() => type == "FrameMatch";
        public bool ShouldSerializetargetFrame() => type == "FrameMatch";
        public bool ShouldSerializealignAxes() => type == "FrameMatch";

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
            if (type == "FrameMatch")
                return !string.IsNullOrEmpty(component_1) && !string.IsNullOrEmpty(component_2)
                    && assemblyFrame != null && targetFrame != null;
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
            if (type == "FrameMatch") return !string.IsNullOrEmpty(name);
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
