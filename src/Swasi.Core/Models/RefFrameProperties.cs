using System.Collections.Generic;

namespace SolidWorks_ASsembly_Instructor
{
    public class RefFrameProperties
    {
        public VisionProperties visionProperties;
        public LaserProperties laserProperties;
        public GrippingProperties grippingProperties;
        public AssemblyProperties assemblyProperties;
        public GluePtProperties gluePtProperties;
        public DispensePathProperties dispensePathProperties;

        public RefFrameProperties()
        {
            visionProperties = new VisionProperties();
            laserProperties = new LaserProperties();
            grippingProperties = new GrippingProperties();
            assemblyProperties = new AssemblyProperties();
            gluePtProperties = new GluePtProperties();
            dispensePathProperties = new DispensePathProperties();

        }

        public void SetVision(bool isVisionFrame)
        {
            visionProperties.isVisionFrame = isVisionFrame;
        }

        public void SetLaser(bool isLaserFrame)
        {
            laserProperties.isLaserFrame = isLaserFrame;
        }

        public void SetTarget(bool isTargetFrame)
        {
            assemblyProperties.isTargetFrame = isTargetFrame;
            assemblyProperties.isAssemblyFrame = !isTargetFrame;
        }

        public void SetAssembly(bool isAssemblyFrame)
        {
            assemblyProperties.isTargetFrame = !isAssemblyFrame;
            assemblyProperties.isAssemblyFrame = isAssemblyFrame;
        }

        public void SetGripping(bool isGrippingFrame)
        {
            grippingProperties.isGrippingFrame = isGrippingFrame;
        }

        public void SetGluePt(bool isGluePt)
        {
            gluePtProperties.isGlueFrame = isGluePt;
        }

        // Newtonsoft.Json will call methods named ShouldSerialize{PropertyName} (matching the property name)
        // to determine whether to include a property during serialization. These methods return true
        // only when the corresponding feature flag is enabled.

        public bool ShouldSerializevisionProperties()
        {
            return visionProperties != null && visionProperties.isVisionFrame;
        }

        public bool ShouldSerializelaserProperties()
        {
            return laserProperties != null && laserProperties.isLaserFrame;
        }

        public bool ShouldSerializegrippingProperties()
        {
            return grippingProperties != null && grippingProperties.isGrippingFrame;
        }

        public bool ShouldSerializeassemblyProperties()
        {
            return assemblyProperties != null && (assemblyProperties.isAssemblyFrame || assemblyProperties.isTargetFrame);
        }

        public bool ShouldSerializegluePtProperties()
        {
            return gluePtProperties != null && gluePtProperties.isGlueFrame;
        }

        public bool ShouldSerializedispensePathProperties()
        {
            return dispensePathProperties != null && dispensePathProperties.isDispensePathFrame;
        }
    }

    public class VisionProperties
    {

        public bool isVisionFrame;

        public VisionProperties()
        {
            isVisionFrame = false;
        }
    }

    public class LaserProperties
    {

        public bool isLaserFrame;

        public LaserProperties()
        {
            isLaserFrame = false;
        }
    }

    public class GrippingProperties
    {

        public bool isGrippingFrame;
        public List<string> compatibleGrippers;
        public List<string> compatibleGripperTips;

        public GrippingProperties()
        {
            isGrippingFrame = false;
            compatibleGrippers = new List<string>();
            compatibleGripperTips = new List<string>();
        }
    }

    public class AssemblyProperties
    {

        public bool isAssemblyFrame;
        public bool isTargetFrame;
        public string associatedFrame;
        public string associatedComponent;
        public bool ShouldSerializeassociatedComponent() => !string.IsNullOrEmpty(associatedComponent);

        public AssemblyProperties()
        {
            isAssemblyFrame = false;
            isTargetFrame = false;
            associatedFrame = "";
        }
    }

    public class GluePtProperties
    {

        public bool isGlueFrame;
        public float timeMs;
        public float dispenseOffsetMm;

        public GluePtProperties()
        {
            isGlueFrame = false;
            timeMs = 0;
            dispenseOffsetMm = 0;
        }
    }

    public class DispensePathProperties
    {

        public bool isDispensePathFrame;
        public string dispenseFilePath;

        public DispensePathProperties()
        {
            isDispensePathFrame = false;
            dispenseFilePath = "";
        }
    }

}
