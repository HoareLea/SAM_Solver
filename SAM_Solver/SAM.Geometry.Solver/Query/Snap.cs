using System;
using System.Collections.Generic;
using System.Linq;
using SAM.Core;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;


namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// </summary>
        /// <param name="face3Ds"></param>
        /// <param name="bucketSizes"></param>
        /// <param name="weights"></param>
        /// <param name="maxExtensions"></param>
        /// <param name="levels"></param>
        /// <param name="levelSectionOffset"></param>
        /// <param name="nakedNodeSnapDistance"></param>
        /// <param name="minSegmentLength"></param>
        /// <param name="toleranceDistance"></param>
        /// <param name="toleranceAngleRad"></param>
        /// <param name="arcToleranceAngleRad"></param>
        /// <param name="snappedFace3Ds"></param>
        /// <param name="snappedFace3DsSources"></param>
        /// <param name="nakedEnds"></param>
        public static void Snap(
            this IEnumerable<Face3D> face3Ds,
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
            out List<List<Face3D>> snappedFace3Ds,
            out List<List<List<int>>> snappedFace3DsSources,
            out List<List<Point3D>> nakedEnds)
        {
            var SnapSolver = new SnapSolver(face3Ds.ToList(), bucketSizes.ToList(), weights.ToList(), maxExtensions.ToList(), levels,
                levelSectionOffset, nakedNodeSnapDistance, minSegmentLength, 
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

            SnapSolver.Execute();

            snappedFace3Ds = SnapSolver.SnappedWalls;
            snappedFace3DsSources = SnapSolver.SnappedSources;
            nakedEnds = SnapSolver.NakedEnds;
        }

        /// <summary>
        /// </summary>
        /// <param name="face3DObjects"></param>
        /// <param name="bucketSizes"></param>
        /// <param name="weights"></param>
        /// <param name="maxExtensions"></param>
        /// <param name="levels"></param>
        /// <param name="levelSectionOffset"></param>
        /// <param name="nakedNodeSnapDistance"></param>
        /// <param name="minSegmentLength"></param>
        /// <param name="toleranceDistance"></param>
        /// <param name="toleranceAngleRad"></param>
        /// <param name="arcToleranceAngleRad"></param>
        /// <param name="snappedFace3Ds"></param>
        /// <param name="sourceFace3DObjects"></param>
        /// <param name="nakedEnds"></param>
        public static void Snap<T>(
            this IEnumerable<T> face3DObjects,
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
            out List<List<Face3D>> snappedFace3Ds,
            out List<List<T>> sourceFace3DObjects,
            out List<List<Point3D>> nakedEnds) where T : IFace3DObject
        {
            snappedFace3Ds = null;
            sourceFace3DObjects = null;
            nakedEnds = null;

            if(face3DObjects == null)
            {
                return;
            }

            List<Tuple<Face3D, T>> tuples = new List<Tuple<Face3D, T>>();
            foreach(T face3DObject in face3DObjects)
            {
                Face3D face3D = face3DObject?.Face3D;
                if(face3D == null || !face3D.IsValid())
                {
                    continue;
                }

                tuples.Add(new Tuple<Face3D, T>(face3D, face3DObject));
            }

            SnapSolver snapSolver = new SnapSolver(tuples.ConvertAll(x => x.Item1), bucketSizes.ToList(), weights.ToList(), maxExtensions.ToList(), levels,
                levelSectionOffset, nakedNodeSnapDistance, minSegmentLength,
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

            snapSolver.Execute();

            snappedFace3Ds = snapSolver.SnappedWalls;
            nakedEnds = snapSolver.NakedEnds;

            List<List<List<int>>> snappedSources = snapSolver.SnappedSources;
            if(snappedSources != null)
            {
                sourceFace3DObjects = new List<List<T>>();
                foreach (List<List<int>> sources in snappedSources)
                {
                    List<T> face3DObjects_Sources = new List<T>();
                    foreach(List<int> source in sources)
                    {
                        if(source == null || source.Count == 0)
                        {
                            continue;
                        }
                        
                        face3DObjects_Sources.Add(tuples[source[0]].Item2);
                    }

                    sourceFace3DObjects.Add(face3DObjects_Sources);
                }
            }
        }
    }
}
