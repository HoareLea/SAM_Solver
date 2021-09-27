using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Solver
{
    public static partial class Query
    {
        public static List<List<int>> TipToLine(this List<Line> lines, double gap)
        {
            List<List<int>> tt = new List<List<int>>();

            for (int i = 0; i < lines.Count; i++)
            {
                Line thisline = lines[i];
                int fromi = i * 2;
                int toi = fromi + 1;

                tt.Add(new List<int>());
                tt.Add(new List<int>());

                for (int j = 0; j < lines.Count; j++)
                {
                    if (i == j) continue;
                    Line thatline = lines[j];
                    double fromdist = thatline.DistanceTo(thisline.From, true);
                    double todist = thatline.DistanceTo(thisline.To, true);

                    if (Math.Min(fromdist, todist) <= gap)
                    {
                        if (fromdist < todist) { tt[fromi].Add(j); }
                        else { tt[toi].Add(j); }
                    }
                }
            }

            return tt;
        }
    }
}
