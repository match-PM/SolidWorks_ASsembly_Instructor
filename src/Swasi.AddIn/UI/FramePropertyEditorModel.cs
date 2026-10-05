using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class FramePropertyEditorModel : ICustomTypeDescriptor
    {
        private readonly SwasiFrameMetadata metadata;
        public FramePropertyEditorModel(string name, SwasiFrameMetadata metadata)
        { Name = name; this.metadata = metadata; }

        [Category("Frame"), ReadOnly(true)] public string Name { get; }
        [Category("Frame"), ReadOnly(true)] public SwasiFrameRole Type => metadata.role;

        [Category("Gripping"), Description("Semicolon-separated gripper names.")]
        public string CompatibleGrippers
        {
            get => string.Join("; ", metadata.properties.grippingProperties.compatibleGrippers);
            set => metadata.properties.grippingProperties.compatibleGrippers = Split(value);
        }

        [Category("Gripping"), Description("Semicolon-separated gripper-tip names.")]
        public string CompatibleGripperTips
        {
            get => string.Join("; ", metadata.properties.grippingProperties.compatibleGripperTips);
            set => metadata.properties.grippingProperties.compatibleGripperTips = Split(value);
        }

        [Category("Assembly / Target"), Description("Name of the associated target or assembly frame.")]
        public string AssociatedFrame
        {
            get => metadata.properties.assemblyProperties.associatedFrame;
            set => metadata.properties.assemblyProperties.associatedFrame = value ?? string.Empty;
        }

        [Category("Glue"), Description("Glue dispensing time in milliseconds.")]
        public float GlueTimeMs
        {
            get => metadata.properties.gluePtProperties.timeMs;
            set => metadata.properties.gluePtProperties.timeMs = value;
        }

        [Category("Glue"), Description("Glue dispensing offset in millimeters.")]
        public float GlueDispenseOffsetMm
        {
            get => metadata.properties.gluePtProperties.dispenseOffsetMm;
            set => metadata.properties.gluePtProperties.dispenseOffsetMm = value;
        }

        private static System.Collections.Generic.List<string> Split(string value) =>
            (value ?? string.Empty).Split(';').Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => GetFilteredProperties();
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) => GetFilteredProperties();
        private PropertyDescriptorCollection GetFilteredProperties()
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { nameof(Name), nameof(Type) };
            if (metadata.role.HasFlag(SwasiFrameRole.Gripping)) allowed.UnionWith(new[] { nameof(CompatibleGrippers), nameof(CompatibleGripperTips) });
            // Associations belong to an assembly instance and are configured in Assembly Matches.
            if (metadata.role.HasFlag(SwasiFrameRole.Glue)) allowed.UnionWith(new[] { nameof(GlueTimeMs), nameof(GlueDispenseOffsetMm) });
            return new PropertyDescriptorCollection(TypeDescriptor.GetProperties(this, true).Cast<PropertyDescriptor>().Where(p => allowed.Contains(p.Name)).ToArray());
        }
        AttributeCollection ICustomTypeDescriptor.GetAttributes() => TypeDescriptor.GetAttributes(this, true);
        string ICustomTypeDescriptor.GetClassName() => TypeDescriptor.GetClassName(this, true);
        string ICustomTypeDescriptor.GetComponentName() => TypeDescriptor.GetComponentName(this, true);
        TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(this, true);
        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
        object ICustomTypeDescriptor.GetEditor(System.Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => TypeDescriptor.GetEvents(this, true);
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);
        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) => this;
    }
}
