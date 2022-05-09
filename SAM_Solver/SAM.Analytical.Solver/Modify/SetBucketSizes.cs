using System;
using System.Collections.Generic;
using System.Linq;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        public static void SetBucketSizes<T>(this List<T> face3DObjects, bool @override = true, double factor = 0.6, double minBucketSize = 0.2) where T : Core.IParameterizedSAMObject, IFace3DObject
        {
            if (face3DObjects == null)
            {
                return;
            }

            List<Tuple<int, double>> tuples = new List<Tuple<int, double>>();
            for (int i = 0; i < face3DObjects.Count; i++)
            {
                T face3DObject = face3DObjects[i];
                if(face3DObject == null)
                {
                    continue;
                }

                if(@override || !face3DObject.HasValue(SolverParameter.BucketSize))
                {
                    face3DObject.SetValue(SolverParameter.BucketSize, minBucketSize * factor);
                }

                double thickness = face3DObject.Thickness();
                if(double.IsNaN(thickness))
                {
                    thickness = 0;
                }

                tuples.Add(new Tuple<int, double>(i, thickness));
            }

            double max = tuples.ConvertAll(x => x.Item2).Max();
            double min = tuples.ConvertAll(x => x.Item2).Min();

            if(max > minBucketSize)
            {
                foreach (Tuple<int, double> tuple in tuples)
                {
                    if (@override || !face3DObjects[tuple.Item1].HasValue(SolverParameter.BucketSize))
                    {
                        double bucketSize = Math.Query.Remap(tuple.Item2, min, max, minBucketSize, max);

                        face3DObjects[tuple.Item1].SetValue(SolverParameter.BucketSize, bucketSize * factor);
                    }
                }
            }
        }
    }
}