using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Geometry.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="face"></param>
        /// <param name="plane"></param>
        /// <param name="resultingSegments3D"></param>
        /// <returns></returns>
        public static bool Intersecting(this Face3D face, Plane plane, out List<Segment3D> resultingSegments3D)
        {
            var intersectionResult = plane.PlanarIntersectionResult(face);

            resultingSegments3D = new List<Segment3D>();
            var intersectionGeometry = intersectionResult.GetGeometry3Ds<Segment3D>();
            if (intersectionGeometry == null) return false;
            resultingSegments3D?.AddRange(intersectionGeometry);

            return intersectionResult.Intersecting;
        }
    }
}
