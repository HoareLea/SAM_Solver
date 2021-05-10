using Rhino.Geometry;
using System.Collections.Generic;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        public static Vector3d AverageDirection(this List<Line> lines, List<int> bucket)
        {
            Vector3d dir = lines[bucket[0]].To - lines[bucket[0]].From;
            Vector3d aver = Vector3d.Zero;

            foreach (int id in bucket)
            {
                Vector3d thisvec = lines[id].To - lines[id].From;
                if (Vector3d.VectorAngle(-thisvec, dir) < Vector3d.VectorAngle(thisvec, dir))
                    thisvec = -thisvec;

                thisvec.Unitize();
                aver += thisvec;
            }

            aver.Unitize();
            return aver;
        }
    }
}
