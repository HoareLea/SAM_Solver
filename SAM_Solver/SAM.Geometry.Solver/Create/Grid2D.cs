using System.Collections.Generic;
using SAM.Geometry.Solver;
using SAM.Geometry.Planar;

namespace SAM.Analytical.Solver
{
    public static partial class Create
    {
        public static Grid2D Grid2D(this BoundingBox2D boundingBox2D, Point2D origin, double distance)
        {
            if (boundingBox2D == null)
            {
                return null;
            }

            List<Segment2D> segment2Ds = Geometry.Planar.Create.Segment2Ds(boundingBox2D, distance, distance, origin, true);
            if (segment2Ds == null || segment2Ds.Count == 0)
                return null;

            return new Grid2D(segment2Ds);

        }
    }
}
