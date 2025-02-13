using System.Collections.Generic;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameConstraints
    {
        public RefFrameCentroidConstraint centroid;
        public RefFrameOrthogonalConstraint orthogonal;
        public RefFrameInPlaneConstraint inPlane;


        public RefFrameConstraints()
        {
            centroid = new RefFrameCentroidConstraint();
            orthogonal = new RefFrameOrthogonalConstraint();
            inPlane = new RefFrameInPlaneConstraint();
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


public class RefFrameOrthogonalConstraint
{
    public string frame_1;
    public string frame_2;
    public string frame_3;
    public float distance_from_f1;
    public string unit_distance_from_f1;
    public float distance_from_f1_f2_connection;
    public string frame_normal_plane_axis;
    public string frame_orthogonal_connection_axis;

    public RefFrameOrthogonalConstraint()
    {
        frame_1 = "";
        frame_2 = "";
        frame_3 = "";
        distance_from_f1 = 0;
        unit_distance_from_f1 = "%";
        distance_from_f1_f2_connection = 0;
        frame_normal_plane_axis = "z";
        frame_orthogonal_connection_axis = "x";
    }
}

public class RefFrameInPlaneConstraint
{
    public List<string> refFrameNames;
    public float planeOffset;
    public string normalAxis;

    public RefFrameInPlaneConstraint()
    {
        refFrameNames = new List<string>();
        planeOffset = 0;
        normalAxis = "z";
    }
}