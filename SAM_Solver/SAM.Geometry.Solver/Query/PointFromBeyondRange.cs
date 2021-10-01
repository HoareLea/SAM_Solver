using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// Returns a point on an infinite line based on the given Segment2D evaluated at
        /// an input parameter.
        /// Where paramter 0 is Segment2D.Start and parameter 1 is Segment2D.End
        /// Similar to Rhino.Geometry.Line.PointAt(double)
        /// In this case it's best to change 
        /// Segment2D.GetPoint(double, inverted, add bool finite)
        /// </summary>
        /// <param name="face"></param>
        /// <param name="plane"></param>
        /// <param name="resultingSegments3D"></param>
        /// <returns></returns>
        public static Point2D PointFromBeyondRange(this Segment2D segment, double parameter)
        {
            if (parameter > 0 && parameter < 1) return segment.Point2D(parameter);
            if (parameter == 0) return segment.Start;
            if (parameter == 1) return segment.End;

            return new Point2D(segment.Start.GetMoved(segment.Vector * parameter));
        }
    }
}
