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
        public static double ClosestParameter(this Segment2D segment, Point2D point)
        {           
            var pointClosestToSegment = segment.Closest(point, false);

            if (segment.Distance(pointClosestToSegment) > SAM.Core.Tolerance.Distance)
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
