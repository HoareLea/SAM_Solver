using SAM.Geometry.Object.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        public static bool Air(this IFace3DObject face3DObject)
        {
            if(face3DObject == null)
            {
                return false;
            }

            if(face3DObject is Panel)
            {
                return ((Panel)face3DObject).PanelType == PanelType.Air;
            }

            if(face3DObject is AirPartition)
            {
                return true;
            }

            return false;
        }
    }
}