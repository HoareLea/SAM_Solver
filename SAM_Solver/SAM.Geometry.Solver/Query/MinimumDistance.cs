using SAM.Geometry.Planar;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static double MinimumDistance(this Segment2D segment, Point2D point)
        {
            var pointClosestToSegment = segment.Closest(point, true);
            var minDistance = pointClosestToSegment.Distance(point);            

            return minDistance;
        }
    }
}
