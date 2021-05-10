using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        public static Box BucketBox(this List<Line> lines, List<int> bucket, Vector3d bucketAxis, Point3d gridOrigin)
        {
            //collect pts
            List<Point3d> pts = new List<Point3d>();
            foreach (int id in bucket)
            {
                pts.Add(lines[id].From);
                pts.Add(lines[id].To);
            }

            Vector3d xaver = bucketAxis;
            xaver.Rotate(Math.PI / 2, Vector3d.ZAxis);
            Plane pl = new Plane(gridOrigin, xaver, bucketAxis);
            Box bb = new Box(pl, pts);

            return bb;
        }
    }
}
