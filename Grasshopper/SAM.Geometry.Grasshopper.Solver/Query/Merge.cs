using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Geometry.Grasshopper.Solver
{
    public static partial class Query
    {
        public static List<Line> Merge(this List<Line> lines, List<int> sources, List<int> bucket, Line bucketAxis, out List<List<int>> mergedSources)
        {
            List<Interval> ranges = new List<Interval>();
            List<int> sources_Temp = new List<int>();

            double axisLength = bucketAxis.Length;

            foreach (int id in bucket)
            {
                Line line = lines[id];

                Interval interval = new Interval(bucketAxis.ClosestParameter(line.From) * axisLength, bucketAxis.ClosestParameter(line.To) * axisLength);
                interval.MakeIncreasing();
                ranges.Add(interval);
                sources_Temp.Add(sources[id]);
            }

            Interval[] rangesarray = ranges.ToArray();
            int[] sourcesarray = sources_Temp.ToArray();
            Array.Sort(rangesarray, sourcesarray);

            ranges.Clear();
            sources_Temp.Clear();
            ranges.AddRange(rangesarray);
            sources_Temp.AddRange(sourcesarray);

            HashSet<double> ends = new HashSet<double>();
            foreach (Interval item in ranges)
            {
                ends.Add(item.T0);
                ends.Add(item.T1);
            }

            List<double> sorted = new List<double>(ends);
            sorted.Sort();

            List<Interval> splitted = new List<Interval>();
            List<List<int>> splittedSources = new List<List<int>>();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                double thisfrom = sorted[i];
                double thisto = sorted[i + 1];
                if (Math.Abs(thisfrom - thisto) <= global::Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) continue;

                List<int> rangeSources = new List<int>();

                double thismid = (thisfrom + thisto) / 2;
                bool isin = false;

                for (int j = 0; j < ranges.Count; j++)
                    if (ranges[j].IncludesParameter(thismid))
                    {
                        rangeSources.Add(sources_Temp[j]);
                        isin = true;
                    }

                if (isin)
                {
                    splitted.Add(new Interval(thisfrom, thisto));
                    splittedSources.Add(rangeSources);
                }
            }

            List<Line> linecut = new List<Line>();
            foreach (Interval item in splitted)
                linecut.Add(new Line(bucketAxis.PointAtLength(item.T0), bucketAxis.PointAtLength(item.T1)));

            mergedSources = splittedSources;
            return linecut;
        }

        public static List<Line> Merge(this List<Line> Lines, List<int> bucket, Line bucketAxis)
        {
            List<Interval> ranges = new List<Interval>();

            double axisLength = bucketAxis.Length;

            foreach (int id in bucket)
            {
                Line line = Lines[id];
                Interval interval = new Interval(bucketAxis.ClosestParameter(line.From) * axisLength, bucketAxis.ClosestParameter(line.To) * axisLength);
                interval.MakeIncreasing();
                ranges.Add(interval);
            }

            ranges.Sort();

            HashSet<double> ends = new HashSet<double>();
            foreach (Interval item in ranges)
            {
                ends.Add(item.T0);
                ends.Add(item.T1);
            }

            List<double> sorted = new List<double>(ends);
            sorted.Sort();

            List<Interval> splitted = new List<Interval>();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                double thisfrom = sorted[i];
                double thisto = sorted[i + 1];
                if (Math.Abs(thisfrom - thisto) <= global::Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) continue;

                double thismid = (thisfrom + thisto) / 2;
                bool isin = false;

                for (int j = 0; j < ranges.Count; j++)
                    if (ranges[j].IncludesParameter(thismid))
                    {
                        isin = true;
                        break;
                    }

                if (isin) splitted.Add(new Interval(thisfrom, thisto));
            }

            List<Line> linecut = new List<Line>();
            foreach (Interval item in splitted)
                linecut.Add(new Line(bucketAxis.PointAtLength(item.T0), bucketAxis.PointAtLength(item.T1)));

            return linecut;
        }
    }
}
