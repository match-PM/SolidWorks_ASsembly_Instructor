using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameConstraints
    {
        public RefFrameCentroidConstraint centroid;
        public RefFrameOrthogonalConstraint orthogonal;
        public RefFrameInPlaneConstraint inPlane;
        public RefFrameTransformConstraint transform;

        public RefFrameConstraints()
        {
            centroid = new RefFrameCentroidConstraint();
            orthogonal = new RefFrameOrthogonalConstraint();
            inPlane = new RefFrameInPlaneConstraint();
            transform = new RefFrameTransformConstraint();
        }

        public bool ShouldSerializecentroid() =>
            centroid?.refFrameNames != null && centroid.refFrameNames.Count > 0;

        public bool ShouldSerializeorthogonal() =>
            orthogonal != null &&
            !string.IsNullOrWhiteSpace(orthogonal.frame_1) &&
            !string.IsNullOrWhiteSpace(orthogonal.frame_2) &&
            !string.IsNullOrWhiteSpace(orthogonal.frame_3);

        public bool ShouldSerializeinPlane() =>
            inPlane?.refFrameNames != null && inPlane.refFrameNames.Count > 0;

        public bool ShouldSerializetransform() =>
            transform != null && !string.IsNullOrWhiteSpace(transform.refFrame);

        public bool HasAny() => ShouldSerializecentroid() || ShouldSerializeorthogonal() ||
            ShouldSerializeinPlane() || ShouldSerializetransform();
    }

    public class RefFrameCentroidConstraint
    {

        public List<string> refFrameNames;
        public string dim;
        // Replace the constructor's zero vector when loading or cloning saved offsets.
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<float> offsetValues;

        public RefFrameCentroidConstraint()
        {
            refFrameNames = new List<string>();
            dim = "";
            offsetValues = new List<float>() {0, 0, 0};
        }

        [OnDeserialized]
        private void RestoreLegacyOffsets(StreamingContext context)
        {
            // Older editor clones prepended one default zero triplet per copy.
            // Recover only this known pattern; leave other malformed input intact.
            if (offsetValues == null || offsetValues.Count <= 3 || offsetValues.Count % 3 != 0) return;
            int padding = offsetValues.Count - 3;
            for (int i = 0; i < padding; i++)
                if (offsetValues[i] != 0f) return;
            offsetValues = offsetValues.GetRange(padding, 3);
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

    public class RefFrameTransformConstraint
    {
        public string refFrame;
        public CoordinateSystemDescription transform;

        public RefFrameTransformConstraint()
        {
            refFrame = "";
            transform = new CoordinateSystemDescription();
        }
    }
}
