using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Solver
{
    public static partial class Query
    {
        public static void Snap(this List<Brep> breps,
           List<Interval> levels,
           double bucketSize,
           double gridSize,
           double maxGap,
           double angleSnapDeg,
           double tolerance,
           Point3d origin,
           List<Line> fixedLines,
           out SortedList<Interval, List<Brep>> outputBreps,
           out SortedList<Interval, List<List<int>>> outputIds,
           out List<List<Curve>> slabOutlines,
           out List<Line> axes
            )
        {
            outputBreps = new SortedList<Interval, List<Brep>>();
            outputIds = new SortedList<Interval, List<List<int>>>();
            slabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> wallbreps = new List<Brep>();
            List<int> wallbrepsids = new List<int>();

            List<Brep> slabs = new List<Brep>();
            List<double> slabsZ = new List<double>();

            for (int i = 0; i < breps.Count; i++)
            {
                Brep panel = breps[i];
                BoundingBox bb = panel.GetBoundingBox(false);
                if (bb.Diagonal.Z > tolerance)
                {
                    wallbreps.Add(panel);
                    wallbrepsids.Add(i);
                }
                else if (bb.Diagonal.Z <= tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            levels.Sort();

            //project the fixed lines
            Transform projection = Transform.PlanarProjection(Plane.WorldXY);
            for (int i = 0; i < fixedLines.Count; i++)
            {
                Line thisline = fixedLines[i];
                thisline.Transform(projection);
                fixedLines[i] = thisline;
            }

            //project the wall sections
            List<Line> projectedWallLines = new List<Line>();
            SortedList<Interval, List<int>> wallLineIds = new SortedList<Interval, List<int>>(); //stores the line indices per level
            SortedList<Interval, List<int>> wallSourceIds = new SortedList<Interval, List<int>>(); //stores the line to brep mapping

            foreach (Interval level in levels)
            {
                outputBreps.Add(level, new List<Brep>());
                outputIds.Add(level, new List<List<int>>());
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

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(tolerance))
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
            List<List<int>> buckets = FindBuckets(projectedWallLines, bucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            axes = new List<Line>();
            List<List<int>> connections = TipToLine(projectedWallLines, maxGap);

            projectedWallLines = Snap(projectedWallLines, buckets, gridSize, angleSnapDeg, origin, fixedLines, out axes);
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

                foreach (Interval level in levels)
                {
                    if (Math.Abs(level.T0 - zh) <= tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= tolerance)
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

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, tolerance))
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
                                double sim = Similarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= bucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, tolerance));
                    }
                }

                slabOutlines.Add(thisSlabCurves);
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
                    List<Line> merged = Merge(thisLevelLines, thisLevelSourceIds, thislevelBucket, axes[i], out mergedSources);

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

                outputBreps[level].AddRange(thisLevelMergedWalls);
                outputIds[level].AddRange(thisLevelMergedIds);
            }
        }

        public static void Snap(this List<Brep> breps,
         List<Interval> levels,
         double bucketSize,
         double gridSize,
         double maxGap,
         double angleSnapDeg,
         double tolerance,
         Point3d origin,
         List<Line> fixedLines,
         out SortedList<Interval, List<Brep>> outputWalls,
         out List<List<Curve>> slabOutlines
         )
        {
            outputWalls = new SortedList<Interval, List<Brep>>();
            slabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> walls = new List<Brep>();
            List<Brep> slabs = new List<Brep>();

            List<double> slabsZ = new List<double>();

            for (int i = 0; i < breps.Count; i++)
            {
                Brep panel = breps[i];
                BoundingBox bb = panel.GetBoundingBox(false);

                if (bb.Diagonal.Z > tolerance)
                    walls.Add(panel);
                else if (bb.Diagonal.Z <= tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            levels.Sort();

            //project the fixed lines
            Transform projection = Transform.PlanarProjection(Plane.WorldXY);
            for (int i = 0; i < fixedLines.Count; i++)
            {
                Line thisline = fixedLines[i];
                thisline.Transform(projection);
                fixedLines[i] = thisline;
            }

            //sections
            List<Line> wallsLines = new List<Line>();
            SortedList<Interval, List<int>> wallsId = new SortedList<Interval, List<int>>();

            foreach (Interval level in levels)
            {
                outputWalls.Add(level, new List<Brep>());
                wallsId.Add(level, new List<int>());

                double mid = level.ParameterAt(0.5);
                Plane thiscut = new Plane(new Point3d(0, 0, mid), Vector3d.ZAxis);

                for (int i = 0; i < walls.Count; i++)
                {
                    Brep thiswall = walls[i];
                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(tolerance))
                            {
                                Line thissect = new Line(curve.PointAtStart, curve.PointAtEnd);
                                thissect.Transform(Transform.PlanarProjection(Plane.WorldXY));
                                wallsId[level].Add(wallsLines.Count);
                                wallsLines.Add(thissect);
                            }
                }
            }

            List<List<int>> buckets = FindBuckets(wallsLines, bucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            List<Line> axes = new List<Line>();
            List<List<int>> connections = TipToLine(wallsLines, maxGap);

            wallsLines = Snap(wallsLines, buckets, gridSize, angleSnapDeg, origin, fixedLines, out axes);
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

                foreach (Interval level in levels)
                {
                    if (Math.Abs(level.T0 - zh) <= tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= tolerance)
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

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, tolerance))
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
                                double sim = Similarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= bucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, tolerance));
                    }
                }

                slabOutlines.Add(thisSlabCurves);
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
                    List<Line> merged = Merge(thisLevelLines, thislevelBucket, axes[i]);

                    foreach (Line thisline in merged)
                    {
                        thisline.Transform(Transform.Translation(0, 0, level.T0));
                        Surface srf = Surface.CreateExtrusion(thisline.ToNurbsCurve(), new Vector3d(0, 0, level.Length));
                        thisLevelWalls.Add(Brep.CreateFromSurface(srf));
                    }
                }

                outputWalls[level].AddRange(thisLevelWalls);
            }
        }

        public static void Snap(this List<Brep> breps,
            List<Interval> levels,
            double bucketSize,
            double gridSize,
            double maxGap,
            double angleSnapDeg,
            double tolerance,
            Point3d origin,
            out SortedList<Interval, List<Brep>> outputWalls,
            out List<List<Curve>> slabOutlines
            )
        {
            outputWalls = new SortedList<Interval, List<Brep>>();
            slabOutlines = new List<List<Curve>>();

            //split slabs and walls
            List<Brep> walls = new List<Brep>();
            List<Brep> slabs = new List<Brep>();

            List<double> slabsZ = new List<double>();

            for (int i = 0; i < breps.Count; i++)
            {
                Brep panel = breps[i];
                BoundingBox bb = panel.GetBoundingBox(false);

                if (bb.Diagonal.Z > tolerance)
                    walls.Add(panel);
                else if (bb.Diagonal.Z <= tolerance)
                {
                    slabs.Add(panel);
                    slabsZ.Add(bb.Center.Z);
                }
            }

            levels.Sort();

            //sections
            List<Line> wallsLines = new List<Line>();
            SortedList<Interval, List<int>> wallsId = new SortedList<Interval, List<int>>();

            foreach (Interval level in levels)
            {
                outputWalls.Add(level, new List<Brep>());
                wallsId.Add(level, new List<int>());

                double mid = level.ParameterAt(0.5);
                Plane thiscut = new Plane(new Point3d(0, 0, mid), Vector3d.ZAxis);

                for (int i = 0; i < walls.Count; i++)
                {
                    Brep thiswall = walls[i];
                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, thiscut, tolerance, out intc, out intp))
                        foreach (Curve curve in intc)
                            if (curve.IsLinear(tolerance))
                            {
                                Line thissect = new Line(curve.PointAtStart, curve.PointAtEnd);
                                thissect.Transform(Transform.PlanarProjection(Plane.WorldXY));
                                wallsId[level].Add(wallsLines.Count);
                                wallsLines.Add(thissect);
                            }
                }
            }

            List<List<int>> buckets = FindBuckets(wallsLines, bucketSize);

            foreach (List<int> bucket in buckets)
                bucket.Sort();

            List<Line> axes = new List<Line>();
            List<List<int>> connections = TipToLine(wallsLines, maxGap);

            wallsLines = Snap(wallsLines, buckets, gridSize, angleSnapDeg, origin, out axes);
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

                foreach (Interval level in levels)
                {
                    if (Math.Abs(level.T0 - zh) <= tolerance)
                    {
                        zh = level.T0;
                        break;
                    }
                    else if (Math.Abs(level.T1 - zh) <= tolerance)
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

                foreach (Curve crv in Curve.JoinCurves(thisslab.Curves3D, tolerance))
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
                                double sim = Similarity(segment, axis);
                                if (sim < minsim)
                                {
                                    minsim = sim;
                                    bestAxis = axis;
                                }
                            }

                            if (bestAxis != Line.Unset)
                                if (minsim <= bucketSize)
                                {
                                    segment.From = bestAxis.ClosestPoint(segment.From, false);
                                    segment.To = bestAxis.ClosestPoint(segment.To, false);
                                }

                            rounded.Add(segment);
                        }

                        rounded = FixSlab(rounded, tolerance);
                        List<Curve> slabCurves = new List<Curve>();

                        for (int j = 0; j < rounded.Count; j++)
                        {
                            LineCurve line = new LineCurve(rounded[j]);
                            line.Transform(transform);
                            slabCurves.Add(line);
                        }

                        thisSlabCurves.AddRange(Curve.JoinCurves(slabCurves, tolerance));
                    }
                }

                slabOutlines.Add(thisSlabCurves);
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
                    List<Line> merged = Merge(thisLevelLines, thislevelBucket, axes[i]);

                    foreach (Line thisline in merged)
                    {
                        thisline.Transform(Transform.Translation(0, 0, level.T0));
                        Surface srf = Surface.CreateExtrusion(thisline.ToNurbsCurve(), new Vector3d(0, 0, level.Length));
                        thisLevelWalls.Add(Brep.CreateFromSurface(srf));
                    }
                }

                outputWalls[level].AddRange(thisLevelWalls);
            }
        }

        public static List<Line> Snap(this List<Line> lines, 
            List<List<int>> buckets, 
            double gridSize, 
            double angleSnapDeg, 
            Point3d gridOrigin, 
            List<Line> fixedLines, 
            out List<Line> bucketsAxes
            )
        {
            List<Line> snapped = new List<Line>(lines);
            double angleSnapRad = Rhino.RhinoMath.ToRadians(angleSnapDeg);

            bucketsAxes = new List<Line>();

            for (int i = 0; i < buckets.Count; i++)
            {
                Vector3d aver = AverageDirection(lines, buckets[i]);
                double thisang = Vector3d.VectorAngle(Vector3d.XAxis, aver, Plane.WorldXY);
                thisang = Math.Round(thisang / angleSnapRad) * angleSnapRad;
                aver = Vector3d.XAxis;
                aver.Rotate(thisang, Vector3d.ZAxis);

                Box bb = BucketBox(lines, buckets[i], aver, gridOrigin);

                Line axis = Line.Unset;

                if (!TrySnapToFixed(bb, fixedLines, out axis))
                {
                    axis = new Line(bb.PointAt(0.5, 0, 0), bb.PointAt(0.5, 1, 0));
                    double distance = axis.ClosestPoint(gridOrigin, false).DistanceTo(gridOrigin);
                    double roundedDistance = Math.Round(distance / gridSize) * gridSize;
                    double diff = roundedDistance - distance;
                    Vector3d perp = axis.ClosestPoint(gridOrigin, false) - gridOrigin;
                    perp.Unitize();
                    perp *= diff;
                    axis.Transform(Transform.Translation(perp));
                }

                bucketsAxes.Add(axis);

                for (int j = 0; j < buckets[i].Count; j++)
                {
                    Line thisline = lines[buckets[i][j]];
                    thisline.From = axis.ClosestPoint(thisline.From, false);
                    thisline.To = axis.ClosestPoint(thisline.To, false);
                    snapped[buckets[i][j]] = thisline;
                }
            }

            return snapped;
        }

        public static List<Line> Snap(this List<Line> lines, 
            List<List<int>> buckets, 
            double gridSize, 
            double angleSnapDeg, 
            Point3d gridOrigin, 
            out List<Line> bucketsAxes
            )
        {
            List<Line> snapped = new List<Line>(lines);
            double angleSnapRad = Rhino.RhinoMath.ToRadians(angleSnapDeg);

            bucketsAxes = new List<Line>();

            for (int i = 0; i < buckets.Count; i++)
            {
                Vector3d aver = AverageDirection(lines, buckets[i]);
                double thisang = Vector3d.VectorAngle(Vector3d.XAxis, aver, Plane.WorldXY);
                thisang = Math.Round(thisang / angleSnapRad) * angleSnapRad;
                aver = Vector3d.XAxis;
                aver.Rotate(thisang, Vector3d.ZAxis);

                Box bb = BucketBox(lines, buckets[i], aver, gridOrigin);
                Line axis = new Line(bb.PointAt(0.5, 0, 0), bb.PointAt(0.5, 1, 0));

                double distance = axis.ClosestPoint(gridOrigin, false).DistanceTo(gridOrigin);
                double roundedDistance = Math.Round(distance / gridSize) * gridSize;
                double diff = roundedDistance - distance;
                Vector3d perp = axis.ClosestPoint(gridOrigin, false) - gridOrigin;
                perp.Unitize();
                perp *= diff;
                axis.Transform(Transform.Translation(perp));

                bucketsAxes.Add(axis);

                for (int j = 0; j < buckets[i].Count; j++)
                {
                    Line thisline = lines[buckets[i][j]];
                    thisline.From = axis.ClosestPoint(thisline.From, false);
                    thisline.To = axis.ClosestPoint(thisline.To, false);
                    snapped[buckets[i][j]] = thisline;
                }
            }

            return snapped;
        }
    }
}