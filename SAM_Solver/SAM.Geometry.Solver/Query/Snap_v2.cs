using System.Collections.Generic;
using System.Linq;
using SAM.Core;
using SAM.Geometry.Spatial;


namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// Snap Solver version 1.4.2 pure SAM approach 
        /// construction and execution
        /// </summary>
        /// <param name="face3Ds"></param>
        /// <param name="bucketSizes"></param>
        /// <param name="weights"></param>
        /// <param name="maxExtensions"></param>
        /// <param name="levels"></param>
        /// <param name="levelSectionOffset"></param>
        /// <param name="nakedNodeSnapDistance"></param>
        /// <param name="minWallSegmentLength"></param>
        /// <param name="toleranceDistance"></param>
        /// <param name="toleranceAngleRad"></param>
        /// <param name="arcToleranceAngleRad"></param>
        /// <param name="snappedWallsFace3Ds"></param>
        /// <param name="snappedWallSources"></param>
        /// <param name="nakedEnds"></param>
        /// <param name="debug"></param>
        public static void Snap_v2(this IEnumerable<Face3D> face3Ds, IEnumerable<double> bucketSizes, 
            IEnumerable<double> weights, IEnumerable<double> maxExtensions, List<Range<double>> levels,
            double levelSectionOffset, double nakedNodeSnapDistance, double minWallSegmentLength,
            double toleranceDistance, double toleranceAngleRad, double arcToleranceAngleRad,
            out List<List<Face3D>> snappedWallsFace3Ds,
            out List<List<int>> snappedWallSources,
            out List<List<Point3D>> nakedEnds)
        {
            var SnapSolver = new SnapSolver(face3Ds.ToList(), bucketSizes.ToList(), weights.ToList(), maxExtensions.ToList(), levels,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength, 
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

            SnapSolver.Execute();

            snappedWallsFace3Ds = SnapSolver.SnappedWalls;
            snappedWallSources = SnapSolver.SnappedSources;
            nakedEnds = SnapSolver.NakedEnds;
        }
    }
}
