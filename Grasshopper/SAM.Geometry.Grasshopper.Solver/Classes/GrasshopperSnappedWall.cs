using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Geometry.Grasshopper.Solver
{
    public class GrasshopperSnappedWall
    {
        private Line _projectedAxis = default(Line);
        public Line ProjectedAxis
        {
            get
            {
                return _projectedAxis;
            }
            private set
            {
                _projectedAxis = value;
                Length = _projectedAxis.Length;
            }
        }
        public List<int> SourceIndices { get; private set; }
        public List<Line> SourceSegments { get; private set; }
        public Interval OriginalHeight { get; private set; }
        public double Elevation { get; private set; }
        public double Weight { get; private set; }
        public double BucketSize { get; private set; }
        public double MaxExtension { get; private set; }
        public double Length { get; private set; }
        public bool NakedStart { get; private set; }
        public bool NakedEnd { get; private set; }

        public GrasshopperSnappedWall(int sourceIndex, Line axis, double weight, double bucketSize, double maxExtension, Interval originalHeight)
        {
            Elevation = (axis.From.Z + axis.To.Z) / 2;
            axis.Transform(Transform.PlanarProjection(Plane.WorldXY));
            SourceIndices = new List<int>();
            SourceIndices.Add(sourceIndex);
            SourceSegments = new List<Line>();
            SourceSegments.Add(axis);
            ProjectedAxis = axis;
            Weight = weight;
            BucketSize = bucketSize;
            MaxExtension = maxExtension;
            OriginalHeight = originalHeight;
            NakedStart = true;
            NakedEnd = true;
        }

        public void ResetNakedStatus()
        {
            NakedStart = true;
            NakedEnd = true;
        }

        public void UpdateNakedStatus(GrasshopperSnappedWall other)
        {
            if (!RhinoMath.EpsilonEquals(this.Elevation, other.Elevation, GrasshopperSnapSolver.ModelTolerance))
            { // different levels, doesn't matter
                return;
            }
            Point3d start = ProjectedAxis.From;
            Point3d end = ProjectedAxis.To;
            if (NakedStart && other.ProjectedAxis.MinimumDistanceTo(start) <= GrasshopperSnapSolver.SAMTolerance)
            {
                NakedStart = false;
            }
            if (NakedEnd && other.ProjectedAxis.MinimumDistanceTo(end) <= GrasshopperSnapSolver.SAMTolerance)
            {
                NakedEnd = false;
            }
        }

        public Brep GetBrep()
        {
            LineCurve bottomLine = new LineCurve(ProjectedAxis);
            bottomLine.Transform(Transform.Translation(new Vector3d(0, 0, OriginalHeight.Min)));
            LineCurve topLine = new LineCurve(ProjectedAxis);
            topLine.Transform(Transform.Translation(new Vector3d(0, 0, OriginalHeight.Max)));

            var surface = NurbsSurface.CreateRuledSurface(bottomLine, topLine);
            return surface.ToBrep();
        }

        private Brep GetBrep(Line segment)
        {
            LineCurve bottomLine = new LineCurve(segment);
            bottomLine.Transform(Transform.Translation(new Vector3d(0, 0, OriginalHeight.Min)));
            LineCurve topLine = new LineCurve(segment);
            topLine.Transform(Transform.Translation(new Vector3d(0, 0, OriginalHeight.Max)));

            var surface = NurbsSurface.CreateRuledSurface(bottomLine, topLine);
            return surface.ToBrep();
        }

        public List<Brep> GetBreps(out List<List<int>> sourceIndices)
        {
            List<Brep> surfaces = new List<Brep>();
            sourceIndices = new List<List<int>>();

            //HashSet<double> splitParams = new HashSet<double>();
            //splitParams.Add(0);
            //splitParams.Add(1);
            //foreach (Line segment in SourceSegments) {
            //    double fromParam = RhinoMath.Clamp(Math.Round(ProjectedAxis.ClosestParameter(segment.From), 6), 0, 1);
            //    double toParam = RhinoMath.Clamp(Math.Round(ProjectedAxis.ClosestParameter(segment.To), 6), 0, 1);
            //    splitParams.Add(fromParam);
            //    splitParams.Add(toParam);
            //}
            //List<double> t = splitParams.ToList();
            //t.Sort();
            //List<Line> splitSegments = new List<Line>();
            //for (int i = 0; i < t.Count - 1; i++) {
            //    Line piece = new Line(ProjectedAxis.PointAt(t[i]), ProjectedAxis.PointAt(t[i + 1]));
            //    // shorten the current piece to avoid taking neighbors indices
            //    Line testPiece = piece;
            //    testPiece.Extend(-1.5 * _ModelTolerance, -1.5 * _ModelTolerance);
            //    List<int> indices = new List<int>();
            //    for (int j = 0; j < SourceSegments.Count; j++) {
            //        if (testPiece.MinimumDistanceTo(SourceSegments[j]) < _ModelTolerance) {
            //            indices.Add(SourceIndices[j]);
            //        }
            //    }
            //    splitSegments.Add(piece);
            //    sourceIndices.Add(indices);
            //}

            // split the segments wherever the split hasn't been made yet
            List<double> splitParams = new List<double>();
            splitParams.Add(0);
            splitParams.Add(1);
            foreach (Line segment in SourceSegments)
            {
                double fromParam = RhinoMath.Clamp(ProjectedAxis.ClosestParameter(segment.From), 0, 1);
                double toParam = RhinoMath.Clamp(ProjectedAxis.ClosestParameter(segment.To), 0, 1);
                // check if the params are already on the list
                bool isNew = true; // check the START
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (RhinoMath.EpsilonEquals(splitParams[i], fromParam, GrasshopperSnapSolver.SAMTolerance))
                    {
                        isNew = false;
                        if (splitParams[i] != 0 && splitParams[i] != 1)
                        { // if not the end, adjust to average
                            splitParams[i] = (splitParams[i] + fromParam) / 2;
                        }
                    }
                }
                if (isNew)
                {
                    splitParams.Add(fromParam);
                }
                isNew = true; // same for the END param
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (RhinoMath.EpsilonEquals(splitParams[i], toParam, GrasshopperSnapSolver.SAMTolerance))
                    {
                        isNew = false;
                        if (splitParams[i] != 0 && splitParams[i] != 1)
                        { // if not the end, adjust to average
                            splitParams[i] = (splitParams[i] + toParam) / 2;
                        }
                    }
                }
                if (isNew)
                {
                    splitParams.Add(toParam);
                }
            }
            splitParams.Sort();

            List<Line> splitSegments = new List<Line>();
            for (int i = 0; i < splitParams.Count - 1; i++)
            {
                Line piece = new Line(ProjectedAxis.PointAt(splitParams[i]), ProjectedAxis.PointAt(splitParams[i + 1]));
                // shorten the current piece to avoid taking neighbors indices
                Line testPiece = piece;
                testPiece.Extend(-1.5 * GrasshopperSnapSolver.ModelTolerance, -1.5 * GrasshopperSnapSolver.ModelTolerance);
                List<int> indices = new List<int>();
                for (int j = 0; j < SourceSegments.Count; j++)
                {
                    if (testPiece.MinimumDistanceTo(SourceSegments[j]) < GrasshopperSnapSolver.ModelTolerance)
                    {
                        indices.Add(SourceIndices[j]);
                    }
                }
                splitSegments.Add(piece);
                sourceIndices.Add(indices);
            }

            foreach (Line s in splitSegments)
            {
                surfaces.Add(GetBrep(s));
            }
            //foreach (Line s in SourceSegments) {
            //    surfaces.Add(GetBrep(s));
            //}

            return surfaces;
        }


        private double OrthoDistance2d(Point3d ptA, Point3d ptB)
        {
            double distance = Math.Abs(ptA.X - ptB.X);
            distance += Math.Abs(ptA.Y - ptB.Y);
            return distance;
        }

        public bool TrySnapIfNaked(List<Point3d> possibleAnchors, double snappingDistance, out string report)
        {
            // TODO: checking if the node is naked should not be limited to the nodes - we need to check if it touches any wall as well
            //snappingDistance = MaxExtension;
            double maxOrtho2dDistance = snappingDistance * Math.Sqrt(2); // don't look for points further than this
            Point3d start = ProjectedAxis.From;
            Point3d end = ProjectedAxis.To;
            var startSnapCandidates = new List<Point3d>();
            var endSnapCandidates = new List<Point3d>();
            bool startIsNaked = this.NakedStart;
            bool endIsNaked = this.NakedEnd;
            foreach (Point3d anchorCandidate in possibleAnchors)
            {
                if (startIsNaked)
                {
                    double orthoDistStart = OrthoDistance2d(start, anchorCandidate);
                    //if (RhinoMath.EpsilonEquals(orthoDistStart, 0, _SAMTolerance)) {
                    //    startIsNaked = false;
                    //    this.NakedStart = false;
                    //}
                    if (orthoDistStart <= maxOrtho2dDistance)
                    {
                        startSnapCandidates.Add(anchorCandidate);
                    }
                }
                if (endIsNaked)
                {
                    double orthoDistEnd = OrthoDistance2d(end, anchorCandidate);
                    //if (RhinoMath.EpsilonEquals(orthoDistEnd, 0, _SAMTolerance)) {
                    //    endIsNaked = false;
                    //    this.NakedEnd = false;
                    //}
                    if (orthoDistEnd <= maxOrtho2dDistance)
                    {
                        endSnapCandidates.Add(anchorCandidate);
                    }
                }
                if (!startIsNaked && !endIsNaked)
                {
                    report = "Not snapped because no nodes are naked";
                    return false;
                }
            }
            if (!startIsNaked && !endIsNaked)
            {
                report = "Not snapped because no nodes are naked";
                return false;
            }
            Point3d newStart = Point3d.Unset;
            Point3d newEnd = Point3d.Unset;
            double minDot = Math.Cos(2 * Math.PI / 3); // allow for 120 degrees
                                                       //double minDot = -1; // allow for 120 degrees

            if (startIsNaked && startSnapCandidates.Count > 0) // find the best anchor
            {
                Vector3d startExtensionDirection = ProjectedAxis.UnitTangent * -1;
                Point3d closestCandidate = Point3d.Unset;
                double shortestDistance = double.PositiveInfinity;
                foreach (Point3d candidate in startSnapCandidates)
                {
                    Vector3d snapDirection = candidate - start;
                    snapDirection.Unitize();
                    double dot = startExtensionDirection * snapDirection;
                    if (dot <= minDot) // snap only forward
                    {
                        continue;
                    }
                    double distance = candidate.DistanceTo(start);
                    if (distance < shortestDistance && distance < snappingDistance)
                    {
                        closestCandidate = candidate;
                        shortestDistance = distance;
                    }
                }
                if (shortestDistance < double.PositiveInfinity)
                {
                    newStart = closestCandidate;
                }
            }

            if (endIsNaked && endSnapCandidates.Count > 0) // find the best anchor
            {
                Vector3d endExtensionDirection = ProjectedAxis.UnitTangent;
                Point3d closestCandidate = Point3d.Unset;
                double shortestDistance = double.PositiveInfinity;
                foreach (Point3d candidate in endSnapCandidates)
                {
                    Vector3d snapDirection = candidate - end;
                    snapDirection.Unitize();
                    double dot = endExtensionDirection * snapDirection;
                    if (dot <= minDot) // snap only forward
                    {
                        continue;
                    }
                    double distance = candidate.DistanceTo(end);
                    if (distance < shortestDistance && distance < snappingDistance)
                    {
                        closestCandidate = candidate;
                        shortestDistance = distance;
                    }
                }
                if (shortestDistance < double.PositiveInfinity)
                {
                    newEnd = closestCandidate;
                }
            }

            if ((newStart == Point3d.Unset) && (newEnd == Point3d.Unset))
            {
                report = "Not snapped because no anchors are within range";
                return false;
            }

            if (newStart != Point3d.Unset)
            {
                this.NakedStart = false;
            }
            if (newEnd != Point3d.Unset)
            {
                this.NakedEnd = false;
            }
            newStart = (newStart == Point3d.Unset) ? ProjectedAxis.From : newStart;
            newEnd = (newEnd == Point3d.Unset) ? ProjectedAxis.To : newEnd;
            UpdateEndPoints(newStart, newEnd, stretch: true);
            //ProjectedAxis = new Line(newStart, newEnd);
            report = "Snapped.";
            return true;
        }

        public void UpdateWithTrim(Line newAxis)
        {
            UpdateEndPoints(newAxis.From, newAxis.To, stretch: true);
        }

        public bool TryBucketSnap(GrasshopperSnappedWall other, out bool otherMergedIn)
        {
            otherMergedIn = false;

            //double projectedDistance = LineProjectionDistance(this.ProjectedAxis, other.ProjectedAxis);
            double projectedDistance = MaxProjectionDistance(this.ProjectedAxis, other.ProjectedAxis);

            //Debug.Print("this{0}, other{1}, distance:{2}", this.SourceIndices[0], other.SourceIndices[0], projectedDistance);
            //Debug.Print("thisLine{0}\n, otherLine{1}", this.ProjectedAxis, other.ProjectedAxis, projectedDistance);
            if (projectedDistance > BucketSize)
            {
                return false;
            }
            bool fully = false;
            bool containsInBucket = BucketContains(other, out fully);
            //Debug.Print("this{0}, other{1}, fully:{2}", this.SourceIndices[0], other.SourceIndices[0], fully);
            if (!containsInBucket)
            {
                return false;
            }
            double angleToleranceRad = fully ? GrasshopperSnapSolver.ToleranceAngleRad : GrasshopperSnapSolver.ArcToleranceAngleRad; // if the neighbour is fully contained in the bucket, allow for larger angle tolerance

            bool areColinear = IsRoughlyColinearWith(other, angleToleranceRad);
            if (!areColinear)
            {
                return false;
            }

            double snappedStartParam = this.ProjectedAxis.ClosestParameter(other.ProjectedAxis.From);
            double snappedEndParam = this.ProjectedAxis.ClosestParameter(other.ProjectedAxis.To);
            Interval otherRange = new Interval(snappedStartParam, snappedEndParam);
            otherRange.MakeIncreasing();

            if (RhinoMath.EpsilonEquals(this.Elevation, other.Elevation, GrasshopperSnapSolver.ModelTolerance)) // same level - merge in
            {
                otherMergedIn = true;
                Interval thisNewRange = new Interval(Math.Min(otherRange.Min, 0), Math.Max(otherRange.Max, 1));
                Point3d newStart = this.ProjectedAxis.PointAt(thisNewRange.Min);
                Point3d newEnd = this.ProjectedAxis.PointAt(thisNewRange.Max);
                SourceIndices.AddRange(other.SourceIndices);
                SourceSegments.AddRange(other.SourceSegments);
                // todo: compare weights and decide whether to average or snap
                double weightTolerance = this.Weight * 0.01;
                if (RhinoMath.EpsilonEquals(this.Weight, other.Weight, weightTolerance)) // both have the same weight
                {
                    // find an average position and increase the bucket size accordingly
                    Vector3d mergeDirection = this.ProjectedAxis.ClosestPoint(other.ProjectedAxis.From, limitToFiniteSegment: false) - other.ProjectedAxis.From;
                    mergeDirection /= 2;
                    this.BucketSize += mergeDirection.Length;
                    newStart -= mergeDirection;
                    newEnd -= mergeDirection;
                }
                UpdateEndPoints(newStart, newEnd, stretch: false);
            }
            else // just snap the other
            {
                otherMergedIn = false;
                Point3d newStart = this.ProjectedAxis.PointAt(snappedStartParam);
                Point3d newEnd = this.ProjectedAxis.PointAt(snappedEndParam);
                other.UpdateEndPoints(newStart, newEnd, stretch: true);

                //check if anything has changed
                double delta = newStart.DistanceTo(other.ProjectedAxis.From) + newEnd.DistanceTo(other.ProjectedAxis.To);
                if (delta < GrasshopperSnapSolver.SAMTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateEndPoints(Point3d newStart, Point3d newEnd, bool stretch = false)
        {
            Line previousAxis = ProjectedAxis;
            ProjectedAxis = new Line(newStart, newEnd);
            // source segments need to be adjusted here
            SnapSourceSegments(previousAxis, GrasshopperSnapSolver.MinWallSegmentLength, stretch);
        }

        public List<GrasshopperSnappedWall> SnapSegmentsSplitAndExplode(List<Point3d> additionalSplitLocations, double snappingDistance, bool allowMovingEnds = false)
        {
            additionalSplitLocations = additionalSplitLocations.Select(pt => ProjectedAxis.ClosestPoint(pt, true)).ToList(); // make sure they lie on the axis
            bool[] splitMade = new bool[additionalSplitLocations.Count];

            // first, snap the segments' end points to the split locations
            Point3d[] segmentEndPoints = new Point3d[SourceSegments.Count * 2];
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                segmentEndPoints[2 * i] = SourceSegments[i].From;
                segmentEndPoints[2 * i + 1] = SourceSegments[i].To;
            }

            for (int i = 0; i < segmentEndPoints.Length; i++)
            {
                if (!allowMovingEnds)
                {
                    double closestParam = ProjectedAxis.ClosestParameter(segmentEndPoints[i]);
                    if (RhinoMath.EpsilonEquals(closestParam, 0, GrasshopperSnapSolver.ModelTolerance) || 
                        RhinoMath.EpsilonEquals(closestParam, 1, GrasshopperSnapSolver.ModelTolerance))
                    {
                        continue;
                    }
                }
                for (int j = 0; j < additionalSplitLocations.Count; j++)
                {
                    if (segmentEndPoints[i].DistanceTo(additionalSplitLocations[j]) <= snappingDistance)
                    {
                        segmentEndPoints[i] = additionalSplitLocations[j];
                        splitMade[j] = true;
                    }
                }
            }
            //recreate the segments
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                Line snappedSegment = new Line(segmentEndPoints[2 * i], segmentEndPoints[2 * i + 1]);
                SourceSegments[i] = snappedSegment;
            }

            // get segments' ends as split params
            List<double> splitParams = new List<double>();
            splitParams.Add(0.0);
            splitParams.Add(1.0);
            foreach (Line segment in SourceSegments)
            {
                double fromParam = RhinoMath.Clamp(ProjectedAxis.ClosestParameter(segment.From), 0, 1);
                double toParam = RhinoMath.Clamp(ProjectedAxis.ClosestParameter(segment.To), 0, 1);

                // check if the params are already on the list
                bool isNew = true; // check the START
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (RhinoMath.EpsilonEquals(splitParams[i], fromParam, GrasshopperSnapSolver.SAMTolerance))
                    {
                        isNew = false;
                        if (splitParams[i] != 0 && splitParams[i] != 1)
                        { // if not the end, adjust to average
                            splitParams[i] = (splitParams[i] + fromParam) / 2;
                        }
                    }
                }
                if (isNew)
                {
                    splitParams.Add(fromParam);
                }
                isNew = true; // same for the END param
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (RhinoMath.EpsilonEquals(splitParams[i], toParam, GrasshopperSnapSolver.SAMTolerance))
                    {
                        isNew = false;
                        if (splitParams[i] != 0 && splitParams[i] != 1)
                        { // if not the end, adjust to average
                            splitParams[i] = (splitParams[i] + toParam) / 2;
                        }
                    }
                }
                if (isNew)
                {
                    splitParams.Add(toParam);
                }
            }

            // add new split params
            for (int i = 0; i < additionalSplitLocations.Count; i++)
            {
                if (splitMade[i])
                { // already covered by adjusting the segments
                    continue;
                }
                double newSplit = RhinoMath.Clamp(ProjectedAxis.ClosestParameter(additionalSplitLocations[i]), 0, 1);
                bool isNew = true; // same for the END param
                for (int j = 0; j < splitParams.Count; j++)
                {
                    if (RhinoMath.EpsilonEquals(splitParams[j], newSplit, GrasshopperSnapSolver.SAMTolerance))
                    {
                        isNew = false;
                        if (splitParams[j] != 0 && splitParams[j] != 1)
                        { // if not the end, adjust to average
                            splitParams[j] = (splitParams[j] + newSplit) / 2;
                        }
                    }
                }
                if (isNew)
                {
                    splitParams.Add(newSplit);
                }
            }
            splitParams.Sort();

            List<Line> splitSegments = new List<Line>();
            List<List<int>> sourceIndices = new List<List<int>>();
            for (int i = 0; i < splitParams.Count - 1; i++)
            {
                Line piece = new Line(ProjectedAxis.PointAt(splitParams[i]), ProjectedAxis.PointAt(splitParams[i + 1]));
                // shorten the current piece to avoid taking neighbours' indices
                Line testPiece = piece;
                testPiece.Extend(-1.5 * GrasshopperSnapSolver.ModelTolerance, -1.5 * GrasshopperSnapSolver.ModelTolerance);
                List<int> indices = new List<int>();
                for (int j = 0; j < SourceSegments.Count; j++)
                {
                    if (testPiece.MinimumDistanceTo(SourceSegments[j]) < GrasshopperSnapSolver.ModelTolerance)
                    {
                        indices.Add(SourceIndices[j]);
                    }
                }
                splitSegments.Add(piece);
                sourceIndices.Add(indices);
            }

            // create a new wall from each segment
            List<GrasshopperSnappedWall> splitWalls = new List<GrasshopperSnappedWall>();
            for (int i = 0; i < splitSegments.Count; i++)
            {
                if (splitSegments[i].Length < GrasshopperSnapSolver.SAMTolerance || sourceIndices[i].Count < 1)
                {
                    continue;
                }
                Line newAxis = splitSegments[i];
                newAxis.Transform(Transform.Translation(new Vector3d(0, 0, Elevation)));
                GrasshopperSnappedWall wallSegment = new GrasshopperSnappedWall(sourceIndices[i][0], newAxis, Weight, BucketSize, MaxExtension, OriginalHeight);
                // add the rest of the source indices
                for (int j = 1; j < sourceIndices[i].Count; j++)
                {
                    wallSegment.SourceSegments.Add(splitSegments[i]); // ! segments and indices list lengths have to be the same, so duplicates of the current segment (PROJECTED!!! - this should be somehow secured through accessor or something!) are required
                    wallSegment.SourceIndices.Add(sourceIndices[i][j]);
                }
                splitWalls.Add(wallSegment);
            }
            return splitWalls;
        }

        private void SnapSourceSegments(Line previousAxis, double snappingTolerance, bool stretchEnds = false)
        {
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                Line currentSegment = SourceSegments[i];

                Point3d snappedStart = ProjectedAxis.ClosestPoint(currentSegment.From, false);
                Point3d snappedEnd = ProjectedAxis.ClosestPoint(currentSegment.To, false);

                if (stretchEnds)
                {
                    Point3d previousStart = previousAxis.ClosestPoint(currentSegment.From, false);
                    Point3d previousEnd = previousAxis.ClosestPoint(currentSegment.To, false);

                    if (previousStart.DistanceTo(previousAxis.From) <= snappingTolerance)
                    {
                        snappedStart = ProjectedAxis.From;
                    }
                    if (previousStart.DistanceTo(previousAxis.To) <= snappingTolerance)
                    {
                        snappedStart = ProjectedAxis.To;
                    }
                    if (previousEnd.DistanceTo(previousAxis.From) <= snappingTolerance)
                    {
                        snappedEnd = ProjectedAxis.From;
                    }
                    if (previousEnd.DistanceTo(previousAxis.To) <= snappingTolerance)
                    {
                        snappedEnd = ProjectedAxis.To;
                    }
                }

                // clamp to finite segment
                double startParam = ProjectedAxis.ClosestParameter(snappedStart);
                RhinoMath.Clamp(startParam, 0, 1);
                double endParam = ProjectedAxis.ClosestParameter(snappedEnd);
                RhinoMath.Clamp(endParam, 0, 1);

                snappedStart = ProjectedAxis.PointAt(startParam);
                snappedEnd = ProjectedAxis.PointAt(endParam);

                if (snappedStart.DistanceTo(ProjectedAxis.From) <= snappingTolerance)
                {
                    snappedStart = ProjectedAxis.From;
                }
                if (snappedEnd.DistanceTo(ProjectedAxis.To) <= snappingTolerance)
                {
                    snappedEnd = ProjectedAxis.To;
                }

                SourceSegments[i] = new Line(snappedStart, snappedEnd);
            }

            // now snap the points together to remove gaps
            Point3d[] segmentEndPoints = new Point3d[SourceSegments.Count * 2];
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                segmentEndPoints[2 * i] = SourceSegments[i].From;
                segmentEndPoints[2 * i + 1] = SourceSegments[i].To;
            }


            // snapping:
            HashSet<int> snapped = new HashSet<int>();
            for (int i = 0; i < segmentEndPoints.Length - 1; i++)
            {
                if (snapped.Contains(i))
                {
                    continue;
                }
                List<int> indicesToSnap = new List<int>();
                indicesToSnap.Add(i);
                for (int j = i + 1; j < segmentEndPoints.Length; j++)
                {
                    if (snapped.Contains(j))
                    {
                        continue;
                    }
                    if (segmentEndPoints[i].DistanceTo(segmentEndPoints[j]) <= snappingTolerance)
                    {
                        indicesToSnap.Add(j);
                    }
                }

                if (indicesToSnap.Count < 2)
                {
                    continue;
                }

                //Debug.Print("Segment snapping");
                //Debug.Print("All endpoints:");
                //foreach (Point3d pt in segmentEndPoints) {
                //    Debug.Print(pt.ToString());
                //}

                //Debug.Print("Selected for snapping:");
                //foreach (int n in indicesToSnap) {
                //    Debug.Print(n.ToString());
                //}


                // check if any point is at start or end
                bool snapToStart = false;
                bool snapToEnd = false;
                foreach (int p in indicesToSnap)
                {
                    if (segmentEndPoints[p].DistanceTo(ProjectedAxis.From) <= snappingTolerance)
                    {
                        snapToStart = true;
                    }
                    if (segmentEndPoints[p].DistanceTo(ProjectedAxis.To) <= snappingTolerance)
                    {
                        snapToEnd = true;
                    }
                }
                if (snapToStart && snapToEnd)
                { // the segment is probably very short
                    continue;
                }

                Point3d averagePt = new Point3d();
                if (snapToStart)
                {
                    averagePt = ProjectedAxis.From;
                }
                else if (snapToEnd)
                {
                    averagePt = ProjectedAxis.To;
                }
                else
                { // calculate an average position
                    foreach (int p in indicesToSnap)
                    {
                        averagePt += segmentEndPoints[p];
                    }
                    averagePt /= indicesToSnap.Count;
                }
                foreach (int p in indicesToSnap)
                {
                    segmentEndPoints[p] = averagePt;
                    snapped.Add(p);
                }
            }
            //Debug.Print("Snapped endpoints:");
            //foreach (Point3d pt in segmentEndPoints) {
            //    Debug.Print(pt.ToString());
            //}

            for (int i = 0; i < SourceSegments.Count; i++)
            {
                Line snappedSegment = new Line(segmentEndPoints[2 * i], segmentEndPoints[2 * i + 1]);
                SourceSegments[i] = snappedSegment;
            }
        }

        public bool IsRoughlyColinearWith(GrasshopperSnappedWall other, double angleToleranceRad)
        {
            double minAbsDot = Math.Cos(angleToleranceRad);
            Vector3d directionA = this.ProjectedAxis.Direction;
            Vector3d directionB = other.ProjectedAxis.Direction;
            directionA.Unitize();
            directionB.Unitize();
            double absDotProduct = Math.Abs(directionA * directionB);
            if (absDotProduct < minAbsDot) // too large difference in direction
            {
                return false;
            }
            return true;
        }

        private double MaxProjectionDistance(Line lineA, Line lineB)
        {
            // calculate average projection distance between lines' end points
            double maxDistance = lineA.ClosestPoint(lineB.From, limitToFiniteSegment: false).DistanceTo(lineB.From);
            maxDistance = Math.Max(maxDistance, lineA.ClosestPoint(lineB.To, limitToFiniteSegment: false).DistanceTo(lineB.To));

            return maxDistance;
        }


        private double LineProjectionDistance(Line lineA, Line lineB)
        {
            // calculate average projection distance between lines' end points
            double averageDistance = 0;
            averageDistance += lineA.ClosestPoint(lineB.From, limitToFiniteSegment: false).DistanceTo(lineB.From);
            averageDistance += lineA.ClosestPoint(lineB.To, limitToFiniteSegment: false).DistanceTo(lineB.To);
            averageDistance += lineB.ClosestPoint(lineA.From, limitToFiniteSegment: false).DistanceTo(lineA.From);
            averageDistance += lineB.ClosestPoint(lineA.To, limitToFiniteSegment: false).DistanceTo(lineA.To);
            averageDistance /= 4;

            return averageDistance;
        }

        private bool BucketContains(GrasshopperSnappedWall other, out bool fully)
        {
            fully = false;
            Line dominantLine = this.ProjectedAxis;
            Line otherLine = other.ProjectedAxis;
            double paramFrom = dominantLine.ClosestParameter(otherLine.From);
            double paramTo = dominantLine.ClosestParameter(otherLine.To);

            double paramBucketMargin = this.MaxExtension / this.Length;
            Interval dominantRange = new Interval(-paramBucketMargin, 1 + paramBucketMargin);
            //Interval dominantRange = new Interval(-0.05, 1.05);

            bool containsStart = dominantRange.IncludesParameter(paramFrom, strict: false);
            bool containsEnd = dominantRange.IncludesParameter(paramTo, strict: false);
            fully = containsStart && containsEnd;

            if (containsStart || containsEnd)
            {
                //Debug.Print("this{0}, other{1}, fully:{2}", this.SourceIndices[0], other.SourceIndices[0], fully);
                return true;
            }

            Interval otherRange = new Interval(paramFrom, paramTo);
            if (otherRange.IncludesInterval(dominantRange, strict: false))
            {
                fully = true;
                //Debug.Print("By other - this{0}, other{1}, fully:{2}", this.SourceIndices[0], other.SourceIndices[0], fully);
                return true;
            }
            return false;
        }
    }
}
