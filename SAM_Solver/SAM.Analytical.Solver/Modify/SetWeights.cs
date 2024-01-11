using System;
using System.Collections.Generic;
using System.Linq;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        public static void SetWeights<T>(this List<T> face3DObjects, bool @override = true, double offset = 0.1) where T : Core.IParameterizedSAMObject, IFace3DObject 
        {
            if (face3DObjects == null || face3DObjects.Count == 0)
            {
                return;
            }

            List<Tuple<int, double>> tuples = new List<Tuple<int, double>>();
            for(int i =0; i < face3DObjects.Count; i++)
            {
                T face3DObject = face3DObjects[i];
                if(face3DObject == null)
                {
                    continue;
                }

                if (face3DObject.Air())
                {
                    if(@override || !face3DObject.HasValue(SolverParameter.Weight))
                    {
                        face3DObject.SetValue(SolverParameter.Weight, 0);
                    }
                }

                Face3D face3D = face3DObject.Face3D;

                BoundingBox3D boundingBox3D = face3D.GetBoundingBox();

                if (boundingBox3D.Height > offset)
                {
                    Plane plane = Geometry.Spatial.Create.Plane(boundingBox3D.Min.Z + offset);

                    Segment3D segment3D = Geometry.Spatial.Query.MaxIntersectionSegment3D(plane, face3D);
                    if(segment3D == null)
                    {
                        continue;
                    }

                    tuples.Add(new Tuple<int, double>(i, segment3D.GetLength()));
                }
                else
                {
                    Plane plane = face3D.GetPlane();
                    if (plane == null)
                    {
                        continue;
                    }

                    Geometry.Planar.Rectangle2D rectangle2D = Geometry.Planar.Create.Rectangle2D((plane.Convert(face3D).ExternalEdge2D as Geometry.Planar.ISegmentable2D)?.GetPoints());
                    if(rectangle2D == null)
                    {
                        continue;
                    }

                    tuples.Add(new Tuple<int, double>(i, System.Math.Max(rectangle2D.Height, rectangle2D.Width)));
                }
            }

            double min = tuples.ConvertAll(x => x.Item2).Min();
            double max = tuples.ConvertAll(x => x.Item2).Max();

            foreach(Tuple<int, double> tuple in tuples)
            {
                if (@override || !(face3DObjects[tuple.Item1]).HasValue(SolverParameter.Weight))
                {
                    double weight = Math.Query.Remap(tuple.Item2, min, max, 0.2, 1);
                    (face3DObjects[tuple.Item1]).SetValue(SolverParameter.Weight, weight);
                }
            }
        }
    }
}