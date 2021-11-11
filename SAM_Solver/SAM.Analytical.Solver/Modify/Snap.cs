
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
            double arcToleranceAngleRad,
            out List<Point3D> nakedPoint3Ds)
        {
            nakedPoint3Ds = null;

            if (panels == null)
            {
                return;
            }

            if(bucketSizes == null)
            {
                SetBucketSizes(panels, false);
                
                List<double> bucketSizes_Temp = new List<double>();
                foreach(Panel panel in panels)
                {
                    if(!panel.TryGetValue(SolverParameter.BucketSize, out double bucketSize) || double.IsNaN(bucketSize))
                    {
                        bucketSizes_Temp.Add(0);
                    }
                    else
                    {
                        bucketSizes_Temp.Add(bucketSize);
                    }
                }
                bucketSizes = bucketSizes_Temp;
            }

            if (weights == null)
            {
                SetWeights(panels, false);

                List<double> weights_Temp = new List<double>();
                foreach (Panel panel in panels)
                {
                    if (!panel.TryGetValue(SolverParameter.Weight, out double weight) || double.IsNaN(weight))
                    {
                        weights_Temp.Add(0);
                    }
                    else
                    {
                        weights_Temp.Add(weight);
                    }
                }

                weights = weights_Temp;
            }

            if (maxExtensions == null)
            {
                SetMaxExtends(panels, false);
                
                List<double> maxExtensions_Temp = new List<double>();
                foreach (Panel panel in panels)
                {
                    if (!panel.TryGetValue(SolverParameter.MaxExtend, out double maxExtend) || double.IsNaN(maxExtend))
                    {
                        maxExtensions_Temp.Add(0);
                    }
                    else
                    {
                        maxExtensions_Temp.Add(maxExtend);
                    }
                }

                maxExtensions = maxExtensions_Temp;
            }

            if(levels == null)
            {
                List<double> levels_Temp = new List<double>();
                Dictionary<double, List<Panel>> elevationDictionary = Geometry.Spatial.Query.ElevationDictionary(panels, out double maxElevation, toleranceDistance);
                foreach(KeyValuePair<double, List<Panel>> keyValuePair in elevationDictionary)
                {
                    levels_Temp.Add(keyValuePair.Key);
                }

                levels_Temp.Add(maxElevation);

                levels = new List<Range<double>>();
                for(int i = 1; i < levels_Temp.Count; i++)
                {
                    levels.Add(new Range<double>(levels_Temp[i - 1], levels_Temp[i]));
                }
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
                out List<List<Point3D>> nakedPoint3DsList);

            panels.Clear();

            if(nakedPoint3DsList != null)
            {
                nakedPoint3Ds = new List<Point3D>();
                foreach(List<Point3D> point3Ds in nakedPoint3DsList)
                {
                    nakedPoint3Ds.AddRange(point3Ds);
                }
            }

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
