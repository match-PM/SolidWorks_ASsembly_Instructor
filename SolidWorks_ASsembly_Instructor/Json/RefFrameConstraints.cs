using System.Collections.Generic;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameConstraints
    {
        public RefFrameCentroidConstraint centroid;


        public RefFrameConstraints()
        {
            centroid = new RefFrameCentroidConstraint();
        }
    }
}


public class RefFrameCentroidConstraint
{

    public List<string> refFrameNames;
    public string dim;
    public List<float> offsetValues;

    public RefFrameCentroidConstraint()
    {
        refFrameNames = new List<string>();
        dim = "";
        offsetValues = new List<float>() {0, 0, 0};
    }
}