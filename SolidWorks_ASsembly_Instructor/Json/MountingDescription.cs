using System.Collections.Generic;


namespace SolidWorks_ASsembly_Instructor
{
    public class MountingDescription
    {
        

        public List<AssemblyComponentDescription> components;
        public List<AssemblyConstraintDescription> assemblyConstraints;
        public MountingReferences mountingReferences;

        public MountingDescription()
        {
            components = new List<AssemblyComponentDescription>();
            assemblyConstraints = new List<AssemblyConstraintDescription>();
            mountingReferences = new MountingReferences();

        }

        public int AddComponentsAssemblyConstraint(string NameComponent1, string NameComponent2)
        {
            int index = 0;
            foreach (AssemblyConstraintDescription constraint in assemblyConstraints)
            {
                if ((constraint.component_1 == NameComponent1 && constraint.component_2 == NameComponent2) ||
                    (constraint.component_2 == NameComponent1 && constraint.component_1 == NameComponent2))
                {
                    return index;
                }
                else
                {
                    index++;
                }
            }

            AssemblyConstraintDescription new_constraint = new AssemblyConstraintDescription();
            new_constraint.SetComponent(NameComponent1);
            new_constraint.SetComponent(NameComponent2);
            assemblyConstraints.Add(new_constraint);
            return assemblyConstraints.Count - 1;
        }

        public bool CompleteAssemblyConstraintDescription()
        {
            foreach (AssemblyConstraintDescription constraint in assemblyConstraints)
            {
                bool NameSetSuccess = constraint.SetName();

            }
            bool Indicator = false;
            foreach (AssemblyConstraintDescription constraint in assemblyConstraints)
            {
                Indicator = constraint.CheckAssemblyConstraintComplet();
                if (!Indicator)
                {
                    return false;
                }
            }
            return true;
        }
    }

}