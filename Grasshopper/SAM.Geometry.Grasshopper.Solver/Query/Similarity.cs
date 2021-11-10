using Rhino.Geometry;

namespace SAM.Geometry.Grasshopper.Solver
{
    public static partial class Query
    {
        public static double Similarity(this Line line_1, Line line_2)
        {
            Line prol = new Line(line_2.ClosestPoint(line_1.From, false), line_2.ClosestPoint(line_1.To, false));
            Line prok = new Line(line_1.ClosestPoint(line_2.From, false), line_1.ClosestPoint(line_2.To, false));
            double score = prol.From.DistanceTo(line_1.From);
            score += prol.To.DistanceTo(line_1.To);
            score += prok.From.DistanceTo(line_2.From);
            score += prok.To.DistanceTo(line_2.To);
            return score / 4;
        }
    }
}
