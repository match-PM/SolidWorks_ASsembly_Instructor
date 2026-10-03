using System;

namespace SolidWorks_ASsembly_Instructor
{
    public static class FrameMetadataApplicator
    {
        public static void Apply(RefFrameDescription frame, SwasiFrameMetadata metadata)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (metadata == null) return;
            frame.properties = metadata.properties ?? new RefFrameProperties();
            frame.constraints = metadata.constraints ?? new RefFrameConstraints();
            ApplyRole(frame.properties, metadata.role);
        }

        public static void ApplyRole(RefFrameProperties properties, SwasiFrameRole role)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            properties.visionProperties.isVisionFrame = role.HasFlag(SwasiFrameRole.Vision);
            properties.laserProperties.isLaserFrame = role.HasFlag(SwasiFrameRole.Laser);
            properties.grippingProperties.isGrippingFrame = role.HasFlag(SwasiFrameRole.Gripping);
            properties.assemblyProperties.isTargetFrame = role.HasFlag(SwasiFrameRole.Target);
            properties.assemblyProperties.isAssemblyFrame = role.HasFlag(SwasiFrameRole.Assembly);
            properties.gluePtProperties.isGlueFrame = role.HasFlag(SwasiFrameRole.Glue);
        }
    }
}
