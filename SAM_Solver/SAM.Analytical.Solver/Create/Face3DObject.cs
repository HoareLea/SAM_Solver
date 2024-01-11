using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Create
    {
        public static T Face3DObject<T>(this T face3DObject, Face3D face3D, double tolerance = Core.Tolerance.Distance) where T: IFace3DObject
        {
            if (face3DObject == null || face3D == null)
            {
                return default;
            }

            if(face3DObject is Panel)
            {
                Panel panel = (Panel)(object)face3DObject;

                return (T)(object)Analytical.Create.Panel(panel.Guid, panel, face3D);
            }

            if (face3DObject is IPartition)
            {
                IPartition partition = (IPartition)(object)face3DObject;

                return (T)Analytical.Create.Partition(partition, partition.Guid, face3D, tolerance);
            }


            return default;
        }
    }
}