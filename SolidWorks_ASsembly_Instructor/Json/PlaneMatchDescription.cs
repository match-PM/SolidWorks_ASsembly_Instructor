namespace SolidWorks_ASsembly_Instructor
{
    public class PlaneMatchDescription
    {
        public string planeNameComponent_1;
        public string planeNameComponent_2;
        public float planeOffset;
        public bool invertNormalVector;

        public PlaneMatchDescription()
        {
            planeNameComponent_1 = "";
            planeNameComponent_2 = "";
            planeOffset = 0;   // This is always in the direction of the normal vector of the plane of component_1
            invertNormalVector = false;
        }
    }
}