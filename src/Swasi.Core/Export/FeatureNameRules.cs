using System;
namespace SolidWorks_ASsembly_Instructor
{
    public static class FeatureNameRules
    {
        public const string Prefix = "SWASI_";
        public const string OriginPrefix = "SWASI_Origin_";
        public static bool IsReference(string name) => name != null && name.StartsWith(Prefix, StringComparison.Ordinal);
        public static bool IsOrigin(string name) => name != null && name.StartsWith(OriginPrefix, StringComparison.Ordinal);
        public static void ApplyProperties(RefFrameDescription refFrameDesc, string featureName)
        {
            if (refFrameDesc == null) throw new ArgumentNullException(nameof(refFrameDesc));
            if (!IsReference(featureName)) return;
            if (featureName.Contains("Vision"))
            {
                refFrameDesc.properties.SetVision(true);
            }

            if (featureName.Contains("Laser"))
            {
                refFrameDesc.properties.SetLaser(true);
            }

            if (featureName.Contains("Grip"))
            {
                refFrameDesc.properties.SetGripping(true);
            }

            if (featureName.Contains("Target"))
            {
                refFrameDesc.properties.SetTarget(true);
            }

            if (featureName.Contains("Assembly"))
            {
                refFrameDesc.properties.SetAssembly(true);
            }

            if (featureName.Contains("Glue"))
            {
                refFrameDesc.properties.SetGluePt(true);
            }
        }
    }
}
