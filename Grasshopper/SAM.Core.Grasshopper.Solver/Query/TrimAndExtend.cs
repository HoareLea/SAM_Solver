using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        public static List<Line> TrimAndExtend(List<List<int>> tipLine, List<Line> lines, List<List<int>> buckets)
        {
            List<Line> outs = new List<Line>();

            int[] lineBucket = new int[lines.Count];
            for (int i = 0; i < buckets.Count; i++)
                foreach (int id in buckets[i])
                    lineBucket[id] = i;

            for (int i = 0; i < lines.Count; i++)
            {
                Line thisline = lines[i];
                int thisbucket = lineBucket[i];
                List<int> fromline = tipLine[i * 2];
                List<int> toline = tipLine[i * 2 + 1];

                double fromMin = double.MaxValue;
                double toMax = double.MinValue;

                for (int j = 0; j < fromline.Count; j++)
                {
                    int thatid = fromline[j];
                    int thatbucket = lineBucket[thatid];
                    if (thatbucket == thisbucket) continue;
                    Line thatline = lines[thatid];
                    double thisparam, thatparam;

                    if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, thatline, out thisparam, out thatparam, 0, false))
                    {
                        fromMin = Math.Min(fromMin, thisparam);
                    }
                }

                if ((fromMin != double.MaxValue)) { thisline.From = thisline.PointAt(fromMin); }

                for (int j = 0; j < toline.Count; j++)
                {
                    int thatid = toline[j];
                    int thatbucket = lineBucket[thatid];
                    if (thatbucket == thisbucket) continue;
                    Line thatline = lines[thatid];
                    double thisparam, thatparam;

                    if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, thatline, out thisparam, out thatparam, 0, false))
                    {
                        toMax = Math.Max(toMax, thisparam);
                    }
                }

                if ((toMax != double.MinValue)) { thisline.To = thisline.PointAt(toMax); }

                outs.Add(thisline);
            }

            return outs;
        }
    }
}
