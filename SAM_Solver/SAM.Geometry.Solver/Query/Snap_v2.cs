using System.Collections.Generic;
using System.Linq;
using SAM.Core;
using SAM.Geometry.Spatial;


namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        public static void Snap_v2(this IEnumerable<Face3D> face3Ds, IEnumerable<double> bucketSizes, 
            IEnumerable<double> weights, IEnumerable<double> maxExtensions, List<Range<double>> levels,
            double levelSectionOffset, double nakedNodeSnapDistance, double minWallSegmentLength,
            double toleranceDistance, double toleranceAngleRad, double arcToleranceAngleRad,
            out List<List<Face3D>> snappedWallsFace3Ds,
            out List<List<int>> snappedWallSources,
            out List<List<Point3D>> nakedEnds,
            out List<string> debug)
        {
            var SnapSolver = new SnapSolver(face3Ds.ToList(), bucketSizes.ToList(), weights.ToList(), maxExtensions.ToList(), levels,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength, 
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

            SnapSolver.Execute();

            snappedWallsFace3Ds = SnapSolver.SnappedWalls;
            snappedWallSources = SnapSolver.SnappedSources;
            nakedEnds = SnapSolver.NakedEnds;
            debug = SnapSolver.Debug;
        }
    }
}
