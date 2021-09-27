using Rhino.Geometry;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Solver
{
    public static partial class Query
    {
        public static List<Line> FixSlab(List<Line> slabOutline, double tolerance)
        {
            List<Line> outs = new List<Line>();

            for (int i = 0; i <= slabOutline.Count - 1; i++)
            {
                Line thisline = slabOutline[i];
                Line prevline = slabOutline[(i - 1 + slabOutline.Count) % slabOutline.Count];
                Line thatline = slabOutline[(i + 1 + slabOutline.Count) % slabOutline.Count];
                double thisparam, thatparam;

                if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, thatline, out thisparam, out thatparam, tolerance, false))
                    thisline.To = thisline.PointAt(thisparam);
                else
                    thisline.To = thatline.From;

                if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, prevline, out thisparam, out thatparam, tolerance, false))
                    thisline.From = thisline.PointAt(thisparam);
                else
                    thisline.From = prevline.To;

                outs.Add(thisline);
            }

            return outs;
        }
    }
}
