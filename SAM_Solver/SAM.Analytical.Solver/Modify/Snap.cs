
using System.Collections.Generic;
using SAM.Core;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{

    public static partial class Modify
    {
        public static void Snap<T>(
            this List<T> face3DObjects,
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
            out List<Point3D> nakedPoint3Ds) where T: SAMObject, IFace3DObject
        {
            nakedPoint3Ds = null;

            if (face3DObjects == null)
            {
                return;
            }

            if(bucketSizes == null)
            {
                SetBucketSizes(face3DObjects, false);
                
                List<double> bucketSizes_Temp = new List<double>();
                foreach(T face3DObject in face3DObjects)
                {
                    if(!face3DObject.TryGetValue(SolverParameter.BucketSize, out double bucketSize) || double.IsNaN(bucketSize))
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
                SetWeights(face3DObjects, false);

                List<double> weights_Temp = new List<double>();
                foreach (T face3DObject in face3DObjects)
                {
                    if (!face3DObject.TryGetValue(SolverParameter.Weight, out double weight) || double.IsNaN(weight))
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
                SetMaxExtends(face3DObjects, false);
                
                List<double> maxExtensions_Temp = new List<double>();
                foreach (T face3DObject in face3DObjects)
                {
                    if (!face3DObject.TryGetValue(SolverParameter.MaxExtend, out double maxExtend) || double.IsNaN(maxExtend))
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
                Dictionary<double, List<T>> elevationDictionary = Geometry.Spatial.Query.ElevationDictionary(face3DObjects, out double maxElevation, toleranceDistance);
                foreach(KeyValuePair<double, List<T>> keyValuePair in elevationDictionary)
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
                face3DObjects,
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
                out List<List<T>> sourceFace3DObject,
                out List<List<Point3D>> nakedPoint3DsList);

            face3DObjects.Clear();

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
                    T face3DObject = sourceFace3DObject[i][j];
                    if (face3DObject == null || face3D == null)
                    {
                        continue;
                    }

                    T panel_Temp = Create.Face3DObject(face3DObject, face3D, toleranceDistance);
                    if (panel_Temp != null)
                    {
                        face3DObject = panel_Temp;
                    }

                    face3DObjects.Add(face3DObject);
                }
            }
        }
    }
}
