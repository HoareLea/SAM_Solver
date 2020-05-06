using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Solver
{
    public static class SnapSolver
    {
        public static void SnapPanels(List<Brep> Panels,
           List<Interval> Levels,
           double BucketSize,
           double GridSize,
           double MaxGap,
           double AngleSnapDeg,
           double Tolerance,
           Point3d Origin,
           List<Line> Fixed,
           out SortedList<Interval, List<Brep>> OutputWalls,
           out SortedList<Interval, List<List<int>>> OutputIds,
           out List<List<Curve>> SlabOutlines,
           out List<Line> Axes
            )
        {
            OutputWalls = new SortedList<Interval, List<Brep>>();
            OutputIds = new SortedList<Interval, List<List<int>>>();
            SlabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> wallbreps = new List<Brep>();
            List<int> wallbrepsids = new List<int>();

            List<Brep> slabs = new List<Brep>();
            List<double> slabsZ = new List<double>();

            for (int i = 0; i < Panels.Count; i++)
            {
                Brep panel = Panels[i];
                BoundingBox bb = panel.GetBoundingBox(false);
                if (bb.Diagonal.Z > Tolerance)
                {
                    wallbreps.Add(panel);
                    wallbrepsids.Add(i);
                }
                else if (bb.Diagonal.Z <= Tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            Levels.Sort();

            //project the fixed lines
            Transform projection = Transform.PlanarProjection(Plane.WorldXY);
            for (int i = 0; i < Fixed.Count; i++)
            {
                Line thisline = Fixed[i];
                thisline.Transform(projection);
                Fixed[i] = thisline;
            }

            //project the wall sections
            List<Line> projectedWallLines = new List<Line>();
            SortedList<Interval, List<int>> wallLineIds = new SortedList<Interval, List<int>>(); //stores the line indices per level
            SortedList<Interval, List<int>> wallSourceIds = new SortedList<Interval, List<int>>(); //stores the line to brep mapping

            foreach (Interval level in Levels)
            {
                OutputWalls.Add(level, new List<Brep>());
                OutputIds.Add(level, new List<List<int>>());
                wallLineIds.Add(level, new List<int>());
                wallSourceIds.Add(level, new List<int>());

                double mid = level.ParameterAt(0.5);
                Plane thiscut = new Plane(new Point3d(0, 0, mid), Vector3d.ZAxis);

                for (int i = 0; i < wallbreps.Count; i++)
                {
                    Brep thiswall = wallbreps[i];
                    int thisSourceIndex = wallbrepsids[i];

                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, Tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(Tolerance))
                            {
                                Line thissect = new Line(curve.PointAtStart, curve.PointAtEnd);
                                thissect.Transform(Transform.PlanarProjection(Plane.WorldXY));
                                wallLineIds[level].Add(projectedWallLines.Count);
                                wallSourceIds[level].Add(thisSourceIndex);
                                projectedWallLines.Add(thissect);
                            }
                }
            }

            //this part actually modifies the lines
            List<List<int>> buckets = FindBuckets(projectedWallLines, BucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            Axes = new List<Line>();
            List<List<int>> connections = Tip2Line(projectedWallLines, MaxGap);

            projectedWallLines = SnapLines(projectedWallLines, buckets, GridSize, AngleSnapDeg, Origin, Fixed, out Axes);
            //TODO bucket axes output

            projectedWallLines = TrimAndExtend(connections, projectedWallLines, buckets);

            //sort the indices per level for later binary search
            foreach (Interval level in wallLineIds.Keys)
            {
                List<int> ids = wallLineIds[level];
                List<int> sourceids = wallSourceIds[level];

                int[] idsarray = ids.ToArray();
                int[] sourceidsarray = sourceids.ToArray();
                Array.Sort(idsarray, sourceidsarray);

                ids = new List<int>(idsarray);
                sourceids = new List<int>(sourceidsarray);
            }

            //fix the slabs
            for (int i = 0; i < slabs.Count; i++)
            {
                Brep thisslab = slabs[i];
                List<Curve> thisSlabCurves = new List<Curve>();
                double zh = slabsZ[i];

                foreach (Interval level in Levels)
                {
                    if (Math.Abs(level.T0 - zh) <= Tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= Tolerance)
                    {
                        zh = level.T1;
                        break;
                    }
                    else if (level.IncludesParameter(zh))
                    {
                        if (level.NormalizedParameterAt(zh) < 0.5)
                            zh = level.T0;
                        else
                            zh = level.T1;
                        break;
                    }
                }

                Transform transform = Transform.Translation(new Vector3d(0, 0, zh));

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, Tolerance))
                {
                    Polyline poly = null;
                    if (crv.TryGetPolyline(out poly))
                    {
                        poly.Transform(Transform.PlanarProjection(Plane.WorldXY));
                        List<Line> rounded = new List<Line>();

                        Line[] segments = poly.GetSegments();
                        for (int j = 0; j < segments.Length; j++)
                        {
                            Line segment = segments[j];
                            double minsim = double.MaxValue;
                            Line bestAxis = Line.Unset;

                            for (int k = 0; k < Axes.Count; k++)
                            {
                                Line axis = Axes[k];
                                double sim = LineSimilarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= BucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, Tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, Tolerance));
                    }
                }

                SlabOutlines.Add(thisSlabCurves);
            }

            //get all the output geometry and merge lines
            foreach (Interval level in wallLineIds.Keys)
            {
                List<int> lineIds = wallLineIds[level];
                List<int> sourceIds = wallSourceIds[level];

                List<Line> thisLevelLines = new List<Line>();
                List<int> thisLevelSourceIds = new List<int>();

                List<Brep> thisLevelMergedWalls = new List<Brep>();
                List<List<int>> thisLevelMergedIds = new List<List<int>>();

                //collect lines from the current level
                for (int i = 0; i < lineIds.Count; i++)
                {
                    int lineId = lineIds[i];
                    int sourceId = sourceIds[i];

                    Line thisline = projectedWallLines[lineId];
                    thisLevelLines.Add(thisline);
                    thisLevelSourceIds.Add(sourceId);
                }

                //for each bucket get list of lines at the current level
                for (int i = 0; i < buckets.Count; i++)
                {
                    List<int> bucket = buckets[i];
                    List<int> thislevelBucket = new List<int>();

                    for (int j = 0; j < lineIds.Count; j++)
                    {
                        int index = lineIds[j];
                        if (bucket.BinarySearch(index) >= 0)
                            thislevelBucket.Add(j);
                    }

                    if (thislevelBucket.Count == 0) continue;
                    List<List<int>> mergedSources = new List<List<int>>();
                    List<Line> merged = MergeLines(thisLevelLines, thisLevelSourceIds, thislevelBucket, Axes[i], out mergedSources);

                    for (int j = 0; j < merged.Count; j++)
                    {
                        Line thisline = merged[j];
                        List<int> thislinesouces = mergedSources[j];

                        thisline.Transform(Transform.Translation(0, 0, level.T0));
                        Surface srf = Surface.CreateExtrusion(thisline.ToNurbsCurve(), new Vector3d(0, 0, level.Length));
                        thisLevelMergedWalls.Add(Brep.CreateFromSurface(srf));
                        thisLevelMergedIds.Add(thislinesouces);
                    }
                }

                OutputWalls[level].AddRange(thisLevelMergedWalls);
                OutputIds[level].AddRange(thisLevelMergedIds);
            }
        }

        public static void SnapPanels(List<Brep> Panels,
         List<Interval> Levels,
         double BucketSize,
         double GridSize,
         double MaxGap,
         double AngleSnapDeg,
         double Tolerance,
         Point3d Origin,
         List<Line> Fixed,
         out SortedList<Interval, List<Brep>> OutputWalls,
         out List<List<Curve>> SlabOutlines
         )
        {
            OutputWalls = new SortedList<Interval, List<Brep>>();
            SlabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> walls = new List<Brep>();
            List<Brep> slabs = new List<Brep>();

            List<double> slabsZ = new List<double>();

            for (int i = 0; i < Panels.Count; i++)
            {
                Brep panel = Panels[i];
                BoundingBox bb = panel.GetBoundingBox(false);

                if (bb.Diagonal.Z > Tolerance)
                    walls.Add(panel);
                else if (bb.Diagonal.Z <= Tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            Levels.Sort();

            //project the fixed lines
            Transform projection = Transform.PlanarProjection(Plane.WorldXY);
            for (int i = 0; i < Fixed.Count; i++)
            {
                Line thisline = Fixed[i];
                thisline.Transform(projection);
                Fixed[i] = thisline;
            }

            //sections
            List<Line> wallsLines = new List<Line>();
            SortedList<Interval, List<int>> wallsId = new SortedList<Interval, List<int>>();

            foreach (Interval level in Levels)
            {
                OutputWalls.Add(level, new List<Brep>());
                wallsId.Add(level, new List<int>());

                double mid = level.ParameterAt(0.5);
                Plane thiscut = new Plane(new Point3d(0, 0, mid), Vector3d.ZAxis);

                for (int i = 0; i < walls.Count; i++)
                {
                    Brep thiswall = walls[i];
                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, Tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(Tolerance))
                            {
                                Line thissect = new Line(curve.PointAtStart, curve.PointAtEnd);
                                thissect.Transform(Transform.PlanarProjection(Plane.WorldXY));
                                wallsId[level].Add(wallsLines.Count);
                                wallsLines.Add(thissect);
                            }
                }
            }

            List<List<int>> buckets = FindBuckets(wallsLines, BucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            List<Line> axes = new List<Line>();
            List<List<int>> connections = Tip2Line(wallsLines, MaxGap);

            wallsLines = SnapLines(wallsLines, buckets, GridSize, AngleSnapDeg, Origin, Fixed, out axes);
            wallsLines = TrimAndExtend(connections, wallsLines, buckets);

            List<Surface> surfaces = new List<Surface>();

            foreach (Interval level in wallsId.Keys)
            {
                List<int> ids = wallsId[level];
                ids.Sort();
            }

            for (int i = 0; i < slabs.Count; i++)
            {
                Brep thisslab = slabs[i];
                List<Curve> thisSlabCurves = new List<Curve>();
                double zh = slabsZ[i];

                foreach (Interval level in Levels)
                {
                    if (Math.Abs(level.T0 - zh) <= Tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= Tolerance)
                    {
                        zh = level.T1;
                        break;
                    }
                    else if (level.IncludesParameter(zh))
                    {
                        if (level.NormalizedParameterAt(zh) < 0.5)
                            zh = level.T0;
                        else
                            zh = level.T1;
                        break;
                    }
                }

                Transform transform = Transform.Translation(new Vector3d(0, 0, zh));

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, Tolerance))
                {
                    Polyline poly = null;
                    if (crv.TryGetPolyline(out poly))
                    {
                        poly.Transform(Transform.PlanarProjection(Plane.WorldXY));
                        List<Line> rounded = new List<Line>();

                        Line[] segments = poly.GetSegments();
                        for (int j = 0; j < segments.Length; j++)
                        {
                            Line segment = segments[j];
                            double minsim = double.MaxValue;
                            Line bestAxis = Line.Unset;

                            for (int k = 0; k < axes.Count; k++)
                            {
                                Line axis = axes[k];
                                double sim = LineSimilarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= BucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, Tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, Tolerance));
                    }
                }

                SlabOutlines.Add(thisSlabCurves);
            }

            foreach (Interval level in wallsId.Keys)
            {
                List<int> ids = wallsId[level];
                List<Line> thisLevelLines = new List<Line>();
                List<Brep> thisLevelWalls = new List<Brep>();

                foreach (int id in ids)
                {
                    Line thisline = wallsLines[id];
                    thisLevelLines.Add(thisline);
                }

                for (int i = 0; i < buckets.Count; i++)
                {
                    List<int> bucket = buckets[i];
                    List<int> thislevelBucket = new List<int>();

                    for (int j = 0; j < ids.Count; j++)
                    {
                        int index = ids[j];
                        if (bucket.BinarySearch(index) >= 0)
                            thislevelBucket.Add(j);
                    }

                    if (thislevelBucket.Count == 0) continue;
                    List<Line> merged = MergeLines(thisLevelLines, thislevelBucket, axes[i]);

                    foreach (Line thisline in merged)
                    {
                        thisline.Transform(Transform.Translation(0, 0, level.T0));
                        Surface srf = Surface.CreateExtrusion(thisline.ToNurbsCurve(), new Vector3d(0, 0, level.Length));
                        thisLevelWalls.Add(Brep.CreateFromSurface(srf));
                    }
                }

                OutputWalls[level].AddRange(thisLevelWalls);
            }
        }

        public static void SnapPanels(List<Brep> Panels,
            List<Interval> Levels,
            double BucketSize,
            double GridSize,
            double MaxGap,
            double AngleSnapDeg,
            double Tolerance,
            Point3d Origin,
            out SortedList<Interval, List<Brep>> OutputWalls,
            out List<List<Curve>> SlabOutlines
            )
        {
            OutputWalls = new SortedList<Interval, List<Brep>>();
            SlabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> walls = new List<Brep>();
            List<Brep> slabs = new List<Brep>();

            List<double> slabsZ = new List<double>();

            for (int i = 0; i < Panels.Count; i++)
            {
                Brep panel = Panels[i];
                BoundingBox bb = panel.GetBoundingBox(false);

                if (bb.Diagonal.Z > Tolerance)
                    walls.Add(panel);
                else if (bb.Diagonal.Z <= Tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            Levels.Sort();

            //sections
            List<Line> wallsLines = new List<Line>();
            SortedList<Interval, List<int>> wallsId = new SortedList<Interval, List<int>>();

            foreach (Interval level in Levels)
            {
                OutputWalls.Add(level, new List<Brep>());
                wallsId.Add(level, new List<int>());

                double mid = level.ParameterAt(0.5);
                Plane thiscut = new Plane(new Point3d(0, 0, mid), Vector3d.ZAxis);

                for (int i = 0; i < walls.Count; i++)
                {
                    Brep thiswall = walls[i];
                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, Tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(Tolerance))
                            {
                                Line thissect = new Line(curve.PointAtStart, curve.PointAtEnd);
                                thissect.Transform(Transform.PlanarProjection(Plane.WorldXY));
                                wallsId[level].Add(wallsLines.Count);
                                wallsLines.Add(thissect);
                            }
                }
            }

            List<List<int>> buckets = FindBuckets(wallsLines, BucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            List<Line> axes = new List<Line>();
            List<List<int>> connections = Tip2Line(wallsLines, MaxGap);

            wallsLines = SnapLines(wallsLines, buckets, GridSize, AngleSnapDeg, Origin, out axes);
            wallsLines = TrimAndExtend(connections, wallsLines, buckets);

            List<Surface> surfaces = new List<Surface>();

            foreach (Interval level in wallsId.Keys)
            {
                List<int> ids = wallsId[level];
                ids.Sort();
            }

            for (int i = 0; i < slabs.Count; i++)
            {
                Brep thisslab = slabs[i];
                List<Curve> thisSlabCurves = new List<Curve>();
                double zh = slabsZ[i];

                foreach (Interval level in Levels)
                {
                    if (Math.Abs(level.T0 - zh) <= Tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= Tolerance)
                    {
                        zh = level.T1;
                        break;
                    }
                    else if (level.IncludesParameter(zh))
                    {
                        if (level.NormalizedParameterAt(zh) < 0.5)
                            zh = level.T0;
                        else
                            zh = level.T1;
                        break;
                    }
                }

                Transform transform = Transform.Translation(new Vector3d(0, 0, zh));

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, Tolerance))
                {
                    Polyline poly = null;
                    if (crv.TryGetPolyline(out poly))
                    {
                        poly.Transform(Transform.PlanarProjection(Plane.WorldXY));
                        List<Line> rounded = new List<Line>();

                        Line[] segments = poly.GetSegments();
                        for (int j = 0; j < segments.Length; j++)
                        {
                            Line segment = segments[j];
                            double minsim = double.MaxValue;
                            Line bestAxis = Line.Unset;

                            for (int k = 0; k < axes.Count; k++)
                            {
                                Line axis = axes[k];
                                double sim = LineSimilarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= BucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, Tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, Tolerance));
                    }
                }

                SlabOutlines.Add(thisSlabCurves);
            }

            foreach (Interval level in wallsId.Keys)
            {
                List<int> ids = wallsId[level];
                List<Line> thisLevelLines = new List<Line>();
                List<Brep> thisLevelWalls = new List<Brep>();

                foreach (int id in ids)
                {
                    Line thisline = wallsLines[id];
                    thisLevelLines.Add(thisline);
                }

                for (int i = 0; i < buckets.Count; i++)
                {
                    List<int> bucket = buckets[i];
                    List<int> thislevelBucket = new List<int>();

                    for (int j = 0; j < ids.Count; j++)
                    {
                        int index = ids[j];
                        if (bucket.BinarySearch(index) >= 0)
                            thislevelBucket.Add(j);
                    }

                    if (thislevelBucket.Count == 0) continue;
                    List<Line> merged = MergeLines(thisLevelLines, thislevelBucket, axes[i]);

                    foreach (Line thisline in merged)
                    {
                        thisline.Transform(Transform.Translation(0, 0, level.T0));
                        Surface srf = Surface.CreateExtrusion(thisline.ToNurbsCurve(), new Vector3d(0, 0, level.Length));
                        thisLevelWalls.Add(Brep.CreateFromSurface(srf));
                    }
                }

                OutputWalls[level].AddRange(thisLevelWalls);
            }
        }

        private static List<Line> FixSlab(List<Line> SlabOutline, double Tolerance)
        {
            List<Line> outs = new List<Line>();

            for (int i = 0; i <= SlabOutline.Count - 1; i++)
            {
                Line thisline = SlabOutline[i];
                Line prevline = SlabOutline[(i - 1 + SlabOutline.Count) % SlabOutline.Count];
                Line thatline = SlabOutline[(i + 1 + SlabOutline.Count) % SlabOutline.Count];
                double thisparam, thatparam;

                if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, thatline, out thisparam, out thatparam, Tolerance, false))
                    thisline.To = thisline.PointAt(thisparam);
                else
                    thisline.To = thatline.From;

                if (Rhino.Geometry.Intersect.Intersection.LineLine(thisline, prevline, out thisparam, out thatparam, Tolerance, false))
                    thisline.From = thisline.PointAt(thisparam);
                else
                    thisline.From = prevline.To;

                outs.Add(thisline);
            }

            return outs;
        }

        public static List<Line> MergeLines(List<Line> Lines, List<int> Sources, List<int> Bucket, Line BucketAxis, out List<List<int>> MergedSources)
        {
            List<Interval> ranges = new List<Interval>();
            List<int> sources = new List<int>();

            double axisLength = BucketAxis.Length;

            foreach (int id in Bucket)
            {
                Line line = Lines[id];

                Interval interval = new Interval(BucketAxis.ClosestParameter(line.From) * axisLength, BucketAxis.ClosestParameter(line.To) * axisLength);
                interval.MakeIncreasing();
                ranges.Add(interval);
                sources.Add(Sources[id]);
            }

            Interval[] rangesarray = ranges.ToArray();
            int[] sourcesarray = sources.ToArray();
            Array.Sort(rangesarray, sourcesarray);

            ranges.Clear();
            sources.Clear();
            ranges.AddRange(rangesarray);
            sources.AddRange(sourcesarray);

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
                if (Math.Abs(thisfrom - thisto) <= Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) continue;

                List<int> rangeSources = new List<int>();

                double thismid = (thisfrom + thisto) / 2;
                bool isin = false;

                for (int j = 0; j < ranges.Count; j++)
                    if (ranges[j].IncludesParameter(thismid))
                    {
                        rangeSources.Add(sources[j]);
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
                linecut.Add(new Line(BucketAxis.PointAtLength(item.T0), BucketAxis.PointAtLength(item.T1)));

            MergedSources = splittedSources;
            return linecut;
        }

        public static List<Line> MergeLines(List<Line> Lines, List<int> Bucket, Line BucketAxis)
        {
            List<Interval> ranges = new List<Interval>();

            double axisLength = BucketAxis.Length;

            foreach (int id in Bucket)
            {
                Line line = Lines[id];
                Interval interval = new Interval(BucketAxis.ClosestParameter(line.From) * axisLength, BucketAxis.ClosestParameter(line.To) * axisLength);
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
                if (Math.Abs(thisfrom - thisto) <= Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) continue;

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
                linecut.Add(new Line(BucketAxis.PointAtLength(item.T0), BucketAxis.PointAtLength(item.T1)));

            return linecut;
        }

        public static List<Line> TrimAndExtend(List<List<int>> TipLine,
            List<Line> Lines,
            List<List<int>> Buckets)
        {
            List<Line> outs = new List<Line>();

            int[] lineBucket = new int[Lines.Count];
            for (int i = 0; i < Buckets.Count; i++)
                foreach (int id in Buckets[i])
                    lineBucket[id] = i;

            for (int i = 0; i < Lines.Count; i++)
            {
                Line thisline = Lines[i];
                int thisbucket = lineBucket[i];
                List<int> fromline = TipLine[i * 2];
                List<int> toline = TipLine[i * 2 + 1];

                double fromMin = double.MaxValue;
                double toMax = double.MinValue;

                for (int j = 0; j < fromline.Count; j++)
                {
                    int thatid = fromline[j];
                    int thatbucket = lineBucket[thatid];
                    if (thatbucket == thisbucket) continue;
                    Line thatline = Lines[thatid];
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
                    Line thatline = Lines[thatid];
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

        public static bool TrySnapToFixed(Box BucketBox, List<Line> Fixed, out Line NewAxis)
        {
            List<Line> candidates = new List<Line>();
            List<double> distances = new List<double>();

            BucketBox.Inflate(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 0, 0);

            foreach (Line line in Fixed)
            {
                //TODO: extending add varaiblke to extra extendsio
                //BucketBox.Inflate(0.15, 0, 0);
                Line boxbottom = new Line(BucketBox.PointAt(0, 0, 0), BucketBox.PointAt(1, 0, 0));
                Line boxtop = new Line(BucketBox.PointAt(0, 1, 0), BucketBox.PointAt(1, 1, 0));

                double bota, botb, topa, topb;

                bool int1 = Rhino.Geometry.Intersect.Intersection.LineLine(boxbottom, line, out bota, out botb);
                bool int2 = Rhino.Geometry.Intersect.Intersection.LineLine(boxtop, line, out topa, out topb);

                if (int1 | int2)
                {
                    if (bota >= 0 && bota <= 1 && topa >= 0 && topa <= 1)
                    {
                        candidates.Add(new Line(boxbottom.PointAt(bota), boxtop.PointAt(topa)));
                        Point3d boxmidbot = BucketBox.PointAt(0.5, 0, 0);
                        Point3d boxmidtop = BucketBox.PointAt(0.5, 1, 0);
                        distances.Add(line.DistanceTo(boxmidbot, false) + line.DistanceTo(boxmidtop, false));
                    }
                }
            }

            if (candidates.Count > 0)
            {
                double[] distarr = distances.ToArray();
                Line[] candarr = candidates.ToArray();
                Array.Sort(distarr, candarr);
                NewAxis = candarr[0];
                return true;
            }

            NewAxis = Line.Unset;
            return false;
        }

        public static List<Line> SnapLines(List<Line> Lines, List<List<int>> Buckets, double GridSize, double AngleSnapDeg, Point3d GridOrigin, List<Line> Fixed, out List<Line> BucketsAxes)
        {
            List<Line> snapped = new List<Line>(Lines);
            double angleSnapRad = Rhino.RhinoMath.ToRadians(AngleSnapDeg);

            BucketsAxes = new List<Line>();

            for (int i = 0; i < Buckets.Count; i++)
            {
                Vector3d aver = AverageDirection(Lines, Buckets[i]);
                double thisang = Vector3d.VectorAngle(Vector3d.XAxis, aver, Plane.WorldXY);
                thisang = Math.Round(thisang / angleSnapRad) * angleSnapRad;
                aver = Vector3d.XAxis;
                aver.Rotate(thisang, Vector3d.ZAxis);

                Box bb = BucketBox(Lines, Buckets[i], aver, GridOrigin);

                Line axis = Line.Unset;

                if (!TrySnapToFixed(bb, Fixed, out axis))
                {
                    axis = new Line(bb.PointAt(0.5, 0, 0), bb.PointAt(0.5, 1, 0));
                    double distance = axis.ClosestPoint(GridOrigin, false).DistanceTo(GridOrigin);
                    double roundedDistance = Math.Round(distance / GridSize) * GridSize;
                    double diff = roundedDistance - distance;
                    Vector3d perp = axis.ClosestPoint(GridOrigin, false) - GridOrigin;
                    perp.Unitize();
                    perp *= diff;
                    axis.Transform(Transform.Translation(perp));
                }

                BucketsAxes.Add(axis);

                for (int j = 0; j < Buckets[i].Count; j++)
                {
                    Line thisline = Lines[Buckets[i][j]];
                    thisline.From = axis.ClosestPoint(thisline.From, false);
                    thisline.To = axis.ClosestPoint(thisline.To, false);
                    snapped[Buckets[i][j]] = thisline;
                }
            }

            return snapped;
        }

        public static List<Line> SnapLines(List<Line> Lines, List<List<int>> Buckets, double GridSize, double AngleSnapDeg, Point3d GridOrigin, out List<Line> BucketsAxes)
        {
            List<Line> snapped = new List<Line>(Lines);
            double angleSnapRad = Rhino.RhinoMath.ToRadians(AngleSnapDeg);

            BucketsAxes = new List<Line>();

            for (int i = 0; i < Buckets.Count; i++)
            {
                Vector3d aver = AverageDirection(Lines, Buckets[i]);
                double thisang = Vector3d.VectorAngle(Vector3d.XAxis, aver, Plane.WorldXY);
                thisang = Math.Round(thisang / angleSnapRad) * angleSnapRad;
                aver = Vector3d.XAxis;
                aver.Rotate(thisang, Vector3d.ZAxis);

                Box bb = BucketBox(Lines, Buckets[i], aver, GridOrigin);
                Line axis = new Line(bb.PointAt(0.5, 0, 0), bb.PointAt(0.5, 1, 0));

                double distance = axis.ClosestPoint(GridOrigin, false).DistanceTo(GridOrigin);
                double roundedDistance = Math.Round(distance / GridSize) * GridSize;
                double diff = roundedDistance - distance;
                Vector3d perp = axis.ClosestPoint(GridOrigin, false) - GridOrigin;
                perp.Unitize();
                perp *= diff;
                axis.Transform(Transform.Translation(perp));

                BucketsAxes.Add(axis);

                for (int j = 0; j < Buckets[i].Count; j++)
                {
                    Line thisline = Lines[Buckets[i][j]];
                    thisline.From = axis.ClosestPoint(thisline.From, false);
                    thisline.To = axis.ClosestPoint(thisline.To, false);
                    snapped[Buckets[i][j]] = thisline;
                }
            }

            return snapped;
        }

        public static Box BucketBox(List<Line> Lines, List<int> Bucket, Vector3d BucketAxis, Point3d GridOrigin)
        {
            //collectpts
            List<Point3d> pts = new List<Point3d>();
            foreach (int id in Bucket)
            {
                pts.Add(Lines[id].From);
                pts.Add(Lines[id].To);
            }

            Vector3d xaver = BucketAxis;
            xaver.Rotate(Math.PI / 2, Vector3d.ZAxis);
            Plane pl = new Plane(GridOrigin, xaver, BucketAxis);
            Box bb = new Box(pl, pts);

            return bb;
        }

        public static Vector3d AverageDirection(List<Line> Lines, List<int> Bucket)
        {
            Vector3d dir = Lines[Bucket[0]].To - Lines[Bucket[0]].From;
            Vector3d aver = Vector3d.Zero;

            foreach (int id in Bucket)
            {
                Vector3d thisvec = Lines[id].To - Lines[id].From;
                if (Vector3d.VectorAngle(-thisvec, dir) < Vector3d.VectorAngle(thisvec, dir))
                    thisvec = -thisvec;

                thisvec.Unitize();
                aver += thisvec;
            }

            aver.Unitize();
            return aver;
        }

        public static List<List<int>> FindBuckets(List<Line> Lines, double BucketSize)
        {
            List<Line> extendedLines = new List<Line>(Lines);

            //extend lines so they're a bit longer than bucketsize
            for (int i = 0; i < Lines.Count; i++)
            {
                Line item = extendedLines[i];
                item.Extend(BucketSize * 2, BucketSize * 2);
                extendedLines[i] = item;
            }

            //find pairs of lines where score < bucketsize
            List<int>[] pairs = new List<int>[Lines.Count];

            for (int i = 0; i < Lines.Count; i++)
                pairs[i] = new List<int>();

            for (int i = 0; i < Lines.Count; i++)
                for (int j = 0; j < Lines.Count; j++)
                {
                    if (i == j) continue;
                    if (j > i) break;

                    double thisscore = LineSimilarity(extendedLines[i], extendedLines[j]);
                    if (thisscore <= BucketSize)
                    {
                        pairs[i].Add(j);
                        pairs[j].Add(i);
                    }
                }

            //buckets
            List<List<int>> buckets = new List<List<int>>();
            bool[] visited = new bool[Lines.Count];

            for (int i = 0; i < Lines.Count; i++)
            {
                List<int> thisbucket = new List<int>();
                //find not visited
                int start = -1;
                for (int j = 0; j < visited.Length; j++)
                    if (!visited[j])
                    {
                        start = j;
                        visited[j] = true;
                        break;
                    }
                if (start == -1) break;

                thisbucket.Add(start);
                RecursiveAdd(start, ref thisbucket, ref pairs, ref visited);
                if (thisbucket.Count > 0) buckets.Add(thisbucket);
            }

            //sort buckets by length
            for (int i = 0; i < buckets.Count; i++)
            {
                List<int> thisbucket = buckets[i];
                double[] len = new double[thisbucket.Count];
                int[] ids = new int[thisbucket.Count];

                for (int j = 0; j < thisbucket.Count; j++)
                {
                    len[j] = Lines[thisbucket[j]].Length;
                    ids[j] = thisbucket[j];
                }

                Array.Sort(len, ids);
                Array.Reverse(ids);

                thisbucket.Clear();
                thisbucket.AddRange(ids);
            }

            List<List<int>> fixedBuckets = new List<List<int>>();

            for (int i = 0; i < buckets.Count; i++)
                fixedBuckets.AddRange(SplitBucket(buckets[i], BucketSize, Lines));

            return fixedBuckets;
        }

        public static List<List<int>> SplitBucket(List<int> Bucket, double BucketSize, List<Line> Lines)
        {
            Vector3d aver = AverageDirection(Lines, Bucket);
            aver.Rotate(Math.PI / 2, Vector3d.ZAxis);
            Line axis = new Line(Point3d.Origin, aver);

            double[] param = new double[Bucket.Count];
            for (int i = 0; i < Bucket.Count; i++)
                param[i] = axis.ClosestParameter(Lines[Bucket[i]].PointAt(0.5));

            List<List<int>> split = new List<List<int>>();
            List<Interval> bounds = new List<Interval>();

            split.Add(new List<int>());
            split[0].Add(Bucket[0]);
            bounds.Add(new Interval(param[0], param[0]));

            for (int i = 1; i < Bucket.Count; i++)
            {
                int thisid = Bucket[i];
                double thisparam = param[i];
                bool found = false;

                for (int j = 0; j < split.Count; j++)
                {
                    Interval range = bounds[j];
                    Interval extended = new Interval(Math.Min(range.T0, thisparam), Math.Max(range.T1, thisparam));
                    if (extended.Length > BucketSize) continue;
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

        public static void RecursiveAdd(int Current, ref List<int> Bucket, ref List<int>[] Pairs, ref bool[] Visited)
        {
            List<int> nextlist = new List<int>();

            foreach (int pair in Pairs[Current])
                if (!Visited[pair])
                {
                    Bucket.Add(pair);
                    nextlist.Add(pair);
                    Visited[pair] = true;
                }

            for (int i = 0; i < nextlist.Count; i++)
                RecursiveAdd(nextlist[i], ref Bucket, ref Pairs, ref Visited);
        }

        public static double LineSimilarity(Line l, Line k)
        {
            Line prol = new Line(k.ClosestPoint(l.From, false), k.ClosestPoint(l.To, false));
            Line prok = new Line(l.ClosestPoint(k.From, false), l.ClosestPoint(k.To, false));
            double score = prol.From.DistanceTo(l.From);
            score += prol.To.DistanceTo(l.To);
            score += prok.From.DistanceTo(k.From);
            score += prok.To.DistanceTo(k.To);
            return score / 4;
        }

        public static List<List<int>> Tip2Line(List<Line> Lines, double Gap)
        {
            List<List<int>> tt = new List<List<int>>();

            for (int i = 0; i < Lines.Count; i++)
            {
                Line thisline = Lines[i];
                int fromi = i * 2;
                int toi = fromi + 1;

                tt.Add(new List<int>());
                tt.Add(new List<int>());

                for (int j = 0; j < Lines.Count; j++)
                {
                    if (i == j) continue;
                    Line thatline = Lines[j];
                    double fromdist = thatline.DistanceTo(thisline.From, true);
                    double todist = thatline.DistanceTo(thisline.To, true);

                    if (Math.Min(fromdist, todist) <= Gap)
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