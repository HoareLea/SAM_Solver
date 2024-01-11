using SAM.Geometry.Object.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        public static double Thickness(this IFace3DObject face3DObject)
        {
            if(face3DObject == null)
            {
                return double.NaN;
            }

            if(face3DObject is Panel)
            {
                Panel panel = (Panel)face3DObject;
                if(panel.PanelType == PanelType.Air)
                {
                    return 0;
                }

                double thickness = double.NaN;

                Construction construction = panel.Construction;
                if (construction != null)
                {
                    return construction.GetThickness();
                }
                else
                {
                    if (!construction.TryGetValue(ConstructionParameter.DefaultThickness, out thickness))
                    {
                        thickness = double.NaN;
                    }
                }

                if (double.IsNaN(thickness))
                {
                    thickness = 0;
                }

                return thickness;
            }

            if(face3DObject is AirPartition)
            {
                return 0;
            }

            if(face3DObject is IHostPartition)
            {
                IHostPartition hostPartition = (IHostPartition)face3DObject;

                HostPartitionType hostPartitionType = hostPartition.Type();
                if(hostPartitionType == null)
                {
                    return 0;
                }

                return hostPartitionType.GetThickness();
            }


            return double.NaN;
        }
    }
}