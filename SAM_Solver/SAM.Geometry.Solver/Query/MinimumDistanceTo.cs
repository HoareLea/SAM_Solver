using SAM.Geometry.Planar;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// Returns the shortest distance between this Segmant2D as a finite segment 
        /// and an input point.
        /// Similar to Rhino.Geometry.Line.MinimumDistanceTo(Point3d)
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static double MinimumDistanceTo(this Segment2D segment, Point2D point)
        {
            var pointClosestToSegment = segment.Closest(point, true);
            var minDistance = pointClosestToSegment.Distance(point);            

            return minDistance;
        }
    }
}
