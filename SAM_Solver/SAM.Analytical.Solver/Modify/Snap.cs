
using System.Collections.Generic;
using SAM.Core;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{

    public static partial class Modify
    {
        public static void Snap(
            this List<Panel> panels,
            IEnumerable<double> bucketSizes,
            IEnumerable<double> weights,
            IEnumerable<double> maxExtensions,
            List<Range<double>> levels,
            double levelSectionOffset,
            double nakedNodeSnapDistance,
            double minSegmentLength,
            double toleranceDistance,
            double toleranceAngleRad,
            double arcToleranceAngleRad)
        {
            if (panels == null)
            {
                return;
            }

            Geometry.Solver.Query.Snap(
                panels,
                bucketSizes,
                weights,
                maxExtensions,
                levels,
                levelSectionOffset,
                nakedNodeSnapDistance,
                minSegmentLength,
                toleranceDistance,
                toleranceAngleRad,
                arcToleranceAngleRad,
                out List<List<Face3D>> snappedFace3Ds,
                out List<List<Panel>> snappedPanels,
                out List<List<Point3D>> nakedEnds);

            panels.Clear();

            if (snappedFace3Ds == null)
            {
                return;
            }

            for (int i = 0; i < snappedFace3Ds.Count; i++)
            {
                if (snappedFace3Ds[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < snappedFace3Ds[i].Count; j++)
                {
                    Face3D face3D = snappedFace3Ds[i][j];
                    Panel panel = snappedPanels[i][j];
                    if (panel == null || face3D == null)
                    {
                        continue;
                    }

                    Panel panel_Temp = Create.Panel(panel.Guid, panel, face3D);
                    if (panel_Temp != null)
                    {
                        panel = panel_Temp;
                    }

                    panels.Add(panel);
                }
            }
        }
    }
}
