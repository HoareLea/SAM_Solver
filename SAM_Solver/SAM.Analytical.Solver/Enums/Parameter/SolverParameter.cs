using System.ComponentModel;
using SAM.Core.Attributes;

namespace SAM.Analytical.Solver
{
    [AssociatedTypes(typeof(Geometry.Spatial.IFace3DObject)), Description("Solver Parameter")]
    public enum SolverParameter
    {
        [ParameterProperties("Bucket Size", "Bucket Size [m]"), DoubleParameterValue(0)] BucketSize,
        [ParameterProperties("Max Extend", "Max Extend [m]"), DoubleParameterValue(0)] MaxExtend,
        [ParameterProperties("Weight", "Weight"), DoubleParameterValue(0)] Weight
    }
}