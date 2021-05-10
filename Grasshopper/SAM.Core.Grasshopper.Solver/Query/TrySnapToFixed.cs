using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Solver
{
    public static partial class Query
    {
        public static bool TrySnapToFixed(Box bucketBox, List<Line> fixedLines, out Line newAxis)
        {
            List<Line> candidates = new List<Line>();
            List<double> distances = new List<double>();

            bucketBox.Inflate(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 0, 0);

            foreach (Line line in fixedLines)
            {
                //TODO: extending add varaiblke to extra extendsio
                //BucketBox.Inflate(0.15, 0, 0);
                Line boxbottom = new Line(bucketBox.PointAt(0, 0, 0), bucketBox.PointAt(1, 0, 0));
                Line boxtop = new Line(bucketBox.PointAt(0, 1, 0), bucketBox.PointAt(1, 1, 0));

                double bota, botb, topa, topb;

                bool int1 = Rhino.Geometry.Intersect.Intersection.LineLine(boxbottom, line, out bota, out botb);
                bool int2 = Rhino.Geometry.Intersect.Intersection.LineLine(boxtop, line, out topa, out topb);

                if (int1 | int2)
                {
                    if (bota >= 0 && bota <= 1 && topa >= 0 && topa <= 1)
                    {
                        candidates.Add(new Line(boxbottom.PointAt(bota), boxtop.PointAt(topa)));
                        Point3d boxmidbot = bucketBox.PointAt(0.5, 0, 0);
                        Point3d boxmidtop = bucketBox.PointAt(0.5, 1, 0);
                        distances.Add(line.DistanceTo(boxmidbot, false) + line.DistanceTo(boxmidtop, false));
                    }
                }
            }

            if (candidates.Count > 0)
            {
                double[] distarr = distances.ToArray();
                Line[] candarr = candidates.ToArray();
                Array.Sort(distarr, candarr);
                newAxis = candarr[0];
                return true;
            }

            newAxis = Line.Unset;
            return false;
        }
    }
}