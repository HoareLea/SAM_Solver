using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        public static List<List<int>> SplitBucket(List<int> bucket, double bucketSize, List<Line> lines)
        {
            Vector3d aver = AverageDirection(lines, bucket);
            aver.Rotate(Math.PI / 2, Vector3d.ZAxis);
            Line axis = new Line(Point3d.Origin, aver);

            double[] param = new double[bucket.Count];
            for (int i = 0; i < bucket.Count; i++)
                param[i] = axis.ClosestParameter(lines[bucket[i]].PointAt(0.5));

            List<List<int>> split = new List<List<int>>();
            List<Interval> bounds = new List<Interval>();

            split.Add(new List<int>());
            split[0].Add(bucket[0]);
            bounds.Add(new Interval(param[0], param[0]));

            for (int i = 1; i < bucket.Count; i++)
            {
                int thisid = bucket[i];
                double thisparam = param[i];
                bool found = false;

                for (int j = 0; j < split.Count; j++)
                {
                    Interval range = bounds[j];
                    Interval extended = new Interval(Math.Min(range.T0, thisparam), Math.Max(range.T1, thisparam));
                    if (extended.Length > bucketSize) continue;
                    found = true;
                    split[j].Add(thisid);
                    bounds[j] = extended;
                    break;
                }

                if (!found) //create another bucket
                {
                    split.Add(new List<int>());
                    split[split.Count - 1].Add(thisid);
                    bounds.Add(new Interval(thisparam, thisparam));
                }
            }

            return split;
        }
    }
}
