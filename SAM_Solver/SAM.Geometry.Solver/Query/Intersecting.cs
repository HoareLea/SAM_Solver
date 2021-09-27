using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        public static bool Intersecting(this Face3D face, Plane plane, out List<Segment3D> resultingSegments3D)
        {
            var intersectionResult = plane.PlanarIntersectionResult(face);

            resultingSegments3D = new List<Segment3D>();
            resultingSegments3D.AddRange(intersectionResult.GetGeometry3Ds<Segment3D>());

            return intersectionResult.Intersecting;
        }
    }
}
