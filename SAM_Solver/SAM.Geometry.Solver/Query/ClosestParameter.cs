using SAM.Geometry.Planar;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// Finds the point on an infinite line based on the given Segment2D 
        /// closest to the input point and returns it's parameter
        /// Similar to Rhino.Geometry.Line.ClosestParameter(Point3d) 
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static double ClosestParameter(this Segment2D segment, Point2D point)
        {          
            var pointClosestToSegment = segment.Closest(point, false);

            if (segment.Distance(pointClosestToSegment) > Core.Tolerance.Distance)
            {
                var param = segment.Start.Distance(pointClosestToSegment) / segment.GetLength();
                if (segment.Start.Distance(pointClosestToSegment) <
                    segment.End.Distance(pointClosestToSegment))
                {
                    param = - param ;
                }
            }

            return segment.GetParameter(pointClosestToSegment);
        }
    }
}
