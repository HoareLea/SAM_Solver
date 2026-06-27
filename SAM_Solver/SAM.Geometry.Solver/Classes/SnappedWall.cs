// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using SAM.Core;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Geometry.Solver
{
    public class SnappedWall
    {
        private Segment2D _projectedAxis = new Segment2D(Point2D.Invalid, Point2D.Invalid);
        public Segment2D ProjectedAxis
        {
            get
            {
                return _projectedAxis;
            }
            private set
            {
                _projectedAxis = value;
                Length = _projectedAxis.GetLength();
            }
        }
        public static readonly double ExtensionLimitLengthRatio = 0.49;
        public static readonly double OpenNodeSnapAngleRangeRad = 2 * System.Math.PI / 3; // allow for 120 degrees in both directions
        public List<int> SourceIndices { get; private set; }
        public List<Segment2D> SourceSegments { get; private set; }
        public Range<double> OriginalHeight { get; private set; }
        public double Elevation { get; private set; }
        public double Weight { get; private set; }
        public double BucketSize { get; private set; }
        public double MaxExtension { get; private set; }
        public double Length { get; private set; }
        public bool NakedStart { get; private set; }
        public bool NakedEnd { get; private set; }

        public SnappedWall(int sourceIndex, Segment3D axis, double weight, double bucketSize, double maxExtension, Range<double> originalHeight)
        {
            Elevation = (axis.GetStart().Z + axis.GetEnd().Z) / 2;
            SourceIndices = new List<int>();
            SourceIndices.Add(sourceIndex);
            SourceSegments = new List<Segment2D>();
            var projectedAxis = Segment3DPlanarProjection(axis, Plane.WorldXY);
            SourceSegments.Add(projectedAxis);
            ProjectedAxis = projectedAxis;
            Weight = weight;
            BucketSize = bucketSize;
            MaxExtension = maxExtension;
            OriginalHeight = originalHeight;
            NakedStart = true;
            NakedEnd = true;
        }
        private Segment2D Segment3DPlanarProjection(Segment3D inputSegment, Plane destinationPlane)
        {
            var z = destinationPlane.Origin.Z;
            var inputStart = inputSegment.GetStart();
            var inputEnd = inputSegment.GetEnd();
            var segment2D = new Segment2D(
                new Point2D(inputStart.X, inputStart.Y),
                new Point2D(inputEnd.X, inputEnd.Y));

            return segment2D;
        }

        public void ResetNakedStatus()
        {
            NakedStart = true;
            NakedEnd = true;
        }

        public void UpdateNakedStatus(SnappedWall other)
        {
            //if (!RhinoMath.EpsilonEquals(this.Elevation, other.Elevation, SAM_SnapSolver_GH.ModelTolerance))
            if (!Core.Query.AlmostEqual(this.Elevation, other.Elevation, SnapSolver.ModelTolerance))
                { // different levels, doesn't matter
                return;
            }
            Point2D start = this.ProjectedAxis.Start;
            Point2D end = this.ProjectedAxis.End;
            if (NakedStart && other.ProjectedAxis.MinimumDistanceTo(start) <= SnapSolver.SAMTolerance)
            {
                NakedStart = false;
            }
            if (NakedEnd && other.ProjectedAxis.MinimumDistanceTo(end) <= SnapSolver.SAMTolerance)
            {
                NakedEnd = false;
            }
        }

        //not supposed to have references
        public Face3D GetFace3D()
        {
            var base3d = SnapSolver.ProjectionPlane.Convert(this.ProjectedAxis);
            var bottomElevation = this.OriginalHeight.Min;
            var topElevation = this.OriginalHeight.Max;

            var bottomVector = new Vector3D(0, 0, bottomElevation);
            var topVector = new Vector3D(0, 0, topElevation);

            var facePoints = new List<Point3D> {
                (Point3D) base3d[0].GetMoved(bottomVector), (Point3D) base3d[1].GetMoved(bottomVector),
                (Point3D) base3d[1].GetMoved(topVector), (Point3D) base3d[0].GetMoved(topVector)};

            var faceContour = new Polygon3D(facePoints);

            return new Face3D(faceContour);
        }

        private Face3D GetBrep(Segment2D segment)
        {
            var base3d = SnapSolver.ProjectionPlane.Convert(segment);
            var bottomElevation = this.OriginalHeight.Min;
            var topElevation = this.OriginalHeight.Max;

            var bottomVector = new Vector3D(0, 0, bottomElevation);
            var topVector = new Vector3D(0, 0, topElevation);

            var facePoints = new List<Point3D> {
                (Point3D) base3d[0].GetMoved(bottomVector), (Point3D) base3d[1].GetMoved(bottomVector),
                (Point3D) base3d[1].GetMoved(topVector), (Point3D) base3d[0].GetMoved(topVector)};

            var faceContour = new Polygon3D(facePoints);

            return new Face3D(faceContour);
        }

        public List<Face3D> GetFaces3D(out List<List<int>> sourceIndices)
        {
            List<Face3D> surfaces = new List<Face3D>();
            sourceIndices = new List<List<int>>();

            // split the segments wherever the split hasn't been made yet
            List<double> splitParams = new List<double>();
            splitParams.Add(0);
            splitParams.Add(1);
            foreach (Segment2D segment in SourceSegments)
            {
                double fromParam = ProjectedAxis.ClosestParameter(segment.Start).Clamp(0, 1);
                double toParam = ProjectedAxis.ClosestParameter(segment.End).Clamp(0, 1);
                // check if the params are already on the list
                bool isNew = true; // check the START
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (Core.Query.AlmostEqual(splitParams[i], fromParam, SnapSolver.SAMTolerance))
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
                    if (Core.Query.AlmostEqual(splitParams[i], toParam, SnapSolver.SAMTolerance))
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
            splitParams = splitParams.Where(t => !double.IsNaN(t)).ToList();

            List<Segment2D> splitSegments = new List<Segment2D>();
            for (int i = 0; i < splitParams.Count - 1; i++)
            {
                Segment2D piece = new Segment2D(ProjectedAxis.GetPoint(splitParams[i]), ProjectedAxis.GetPoint(splitParams[i + 1]));
                // shorten the current piece to avoid taking neighbors indices
                Segment2D testPiece = piece;
                testPiece = testPiece.Extend(-0.49 * SnapSolver.MinWallSegmentLength, true, true);
                List<int> indices = new List<int>();
                for (int j = 0; j < SourceSegments.Count; j++)
                {
                    if (testPiece.Distance(SourceSegments[j]) < SnapSolver.ModelTolerance)
                    {
                        indices.Add(SourceIndices[j]);
                    }
                }
                splitSegments.Add(piece);
                sourceIndices.Add(indices);
            }

            foreach (Segment2D s in splitSegments)
            {
                surfaces.Add(GetBrep(s));
            }

            return surfaces;
        }


        private double OrthoDistance2d(Point2D ptA, Point2D ptB)
        {
            double distance = System.Math.Abs(ptA.X - ptB.X);
            distance += System.Math.Abs(ptA.Y - ptB.Y);
            return distance;
        }

        public bool TrySnapIfNaked(List<Point2D> possibleAnchors, double snappingDistance, out string report)
        {
            // TODO: checking if the node is naked should not be limited to the nodes - we need to check if it touches any wall as well
            //snappingDistance = MaxExtension;
            double maxOrtho2dDistance = snappingDistance * System.Math.Sqrt(2); // don't look for points further than this
            Point2D start = ProjectedAxis.Start;
            Point2D end = ProjectedAxis.End;
            var startSnapCandidates = new List<Point2D>();
            var endSnapCandidates = new List<Point2D>();
            bool startIsNaked = this.NakedStart;
            bool endIsNaked = this.NakedEnd;
            foreach (Point2D anchorCandidate in possibleAnchors)
            {
                if (startIsNaked)
                {
                    double orthoDistStart = OrthoDistance2d(start, anchorCandidate);
                    if (orthoDistStart <= maxOrtho2dDistance)
                    {
                        startSnapCandidates.Add(anchorCandidate);
                    }
                }
                if (endIsNaked)
                {
                    double orthoDistEnd = OrthoDistance2d(end, anchorCandidate);
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
            Point2D newStart = Point2D.Invalid;
            Point2D newEnd = Point2D.Invalid;
            double minDot = System.Math.Cos(OpenNodeSnapAngleRangeRad);

            if (startIsNaked && startSnapCandidates.Count > 0) // find the best anchor
            {
                Vector2D startExtensionDirection = ProjectedAxis.Direction.Unit * -1;

                Point2D closestCandidate = Point2D.Invalid;
                double shortestDistance = double.PositiveInfinity;
                foreach (Point2D candidate in startSnapCandidates)
                {
                    Vector2D snapDirection = new Vector2D(
                        candidate.X - start.X, candidate.Y - start.Y);
                    //if(snapDirection.Length > 0)
                        snapDirection = snapDirection.Unit;
    
                    double dot = startExtensionDirection * snapDirection;
                    if (dot <= minDot) // snap only forward
                    {
                        continue;
                    }
                    double distance = candidate.Distance(start);
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
                Vector2D endExtensionDirection = ProjectedAxis.Direction.Unit;
                Point2D closestCandidate = Point2D.Invalid;
                double shortestDistance = double.PositiveInfinity;
                foreach (Point2D candidate in endSnapCandidates)
                {
                    Vector2D snapDirection = new Vector2D(
                        candidate.X - end.X, candidate.Y - end.Y);
                    //if (snapDirection.Length > 0)
                        snapDirection = snapDirection.Unit;
                    double dot = endExtensionDirection * snapDirection;
                    if (dot <= minDot) // snap only forward
                    {
                        continue;
                    }
                    double distance = candidate.Distance(end);
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

            if ((newStart.IsNaN() || newStart == Point2D.Invalid) && 
                (newEnd.IsNaN() || newEnd == Point2D.Invalid))
            {
                report = "Not snapped because no anchors are within range";
                return false;
            }

            if (!newStart.IsNaN())
            {
                this.NakedStart = false;
            }
            if (!newEnd.IsNaN())
            {
                this.NakedEnd = false;
            }
            newStart = (newStart.IsNaN()) ? ProjectedAxis.Start : newStart;
            newEnd = (newEnd.IsNaN()) ? ProjectedAxis.End : newEnd;
            UpdateEndPoints(newStart, newEnd, stretch: true);
            //ProjectedAxis = new Line(newStart, newEnd);
            report = "Snapped.";
            return true;
        }

        public void UpdateWithTrim(Segment2D newAxis)
        {
            UpdateEndPoints(newAxis.Start, newAxis.End, stretch: true);
        }

        public bool TryBucketSnap(SnappedWall other, out bool otherMergedIn)
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
            double angleToleranceRad = fully ? SnapSolver.ToleranceAngleRad : SnapSolver.ArcToleranceAngleRad; // if the neighbour is fully contained in the bucket, allow for larger angle tolerance

            bool areColinear = IsRoughlyColinearWith(other, angleToleranceRad);
            if (!areColinear)
            {
                return false;
            }

            //var tempProjectionAxis = this.ProjectedAxis.Extend(1000, true, true);
            double snappedStartParam = this.ProjectedAxis.ClosestParameter(other.ProjectedAxis.Start);
            double snappedEndParam = this.ProjectedAxis.ClosestParameter(other.ProjectedAxis.End);
            Range<double> otherRange = new Range<double>(snappedStartParam, snappedEndParam);
            otherRange = MakeRangeIncreasing(otherRange);

            if (Core.Query.AlmostEqual(this.Elevation, other.Elevation, SnapSolver.ModelTolerance)) // same level - merge in
            {
                otherMergedIn = true;
                Range<double> thisNewRange = new Range<double>(System.Math.Min(otherRange.Min, 0), System.Math.Max(otherRange.Max, 1));
                Point2D newStart = this.ProjectedAxis.GetPoint(thisNewRange.Min);
                Point2D newEnd = this.ProjectedAxis.GetPoint(thisNewRange.Max);

                //check if there's a gap between the walls and eventually stretch them to the middle
                if ((snappedStartParam < 0 && snappedEndParam < 0) || (snappedStartParam > 1 && snappedEndParam > 1)) { // there's a gap                   
                    bool directionsAligned = this.ProjectedAxis.Direction.Unit * other.ProjectedAxis.Direction.Unit > 0;
                    if (snappedStartParam < 0) {
                        double midParam = (otherRange.Max + 0) / 2;
                        Point2D snapPoint = ProjectedAxis.GetPoint(midParam);
                        UpdateEndPoints(snapPoint, ProjectedAxis.End, stretch: true);
                        if (directionsAligned) {
                            other.UpdateEndPoints(other.ProjectedAxis.Start, snapPoint, stretch: true);
                        }
                        else {
                            other.UpdateEndPoints(snapPoint, other.ProjectedAxis.End, stretch: true);
                        }
                    }
                    else {
                        double midParam = (otherRange.Min + 1) / 2;
                        Point2D snapPoint = ProjectedAxis.GetPoint(midParam);
                        UpdateEndPoints(ProjectedAxis.Start, snapPoint, stretch: true);
                        if (directionsAligned) {
                            other.UpdateEndPoints(snapPoint, other.ProjectedAxis.End, stretch: true);
                        }
                        else {
                            other.UpdateEndPoints(other.ProjectedAxis.Start, snapPoint, stretch: true);
                        }
                    }
                }

                SourceIndices.AddRange(other.SourceIndices);
                SourceSegments.AddRange(other.SourceSegments);
                // todo: maybe compare weights and decide whether to average or snap?
                double weightTolerance = this.Weight * 0.01;
                if (Core.Query.AlmostEqual(this.Weight, other.Weight, weightTolerance)) // both have the same weight
                {
                    // find an average position and increase the bucket size accordingly
                    Vector2D mergeDirection = this.ProjectedAxis.Closest(other.ProjectedAxis.Start, false) - other.ProjectedAxis.Start;
                    mergeDirection /= 2;
                    this.BucketSize += mergeDirection.Length;
                    newStart = new Point2D(newStart.X - mergeDirection.X, newStart.Y - mergeDirection.Y);
                    newEnd = new Point2D(newEnd.X - mergeDirection.X, newEnd.Y - mergeDirection.Y);
                }
                UpdateEndPoints(newStart, newEnd, stretch: false);
            }
            else // just snap the other
            {
                otherMergedIn = false;
                Point2D newStart = this.ProjectedAxis.GetPoint(snappedStartParam);
                Point2D newEnd = this.ProjectedAxis.GetPoint(snappedEndParam);
                other.UpdateEndPoints(newStart, newEnd, stretch: true);

                //check if anything has changed
                double delta = newStart.Distance(other.ProjectedAxis.Start) + newEnd.Distance(other.ProjectedAxis.End);
                if (delta < SnapSolver.SAMTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private Range<double> MakeRangeIncreasing(Range<double> range)
        {
            if (range.Min > range.Max)
                return new Range<double>(range.Max, range.Min);
            else return range;
        }

        private void UpdateEndPoints(Point2D newStart, Point2D newEnd, bool stretch = false)
        {
            Segment2D previousAxis = ProjectedAxis;
            ProjectedAxis = new Segment2D(newStart, newEnd);
            // source segments need to be adjusted here
            SnapSourceSegments(previousAxis, SnapSolver.MinWallSegmentLength, stretch);
        }

        public List<SnappedWall> SnapSegmentsSplitAndExplode(List<Point2D> additionalSplitLocations, double snappingDistance, bool allowMovingEnds = false)
        {
            additionalSplitLocations = additionalSplitLocations.Select(pt => ProjectedAxis.Closest(pt, true)).ToList(); // make sure they lie on the axis
            bool[] splitMade = new bool[additionalSplitLocations.Count];

            // first, snap the segments' end points to the split locations
            Point2D[] segmentEndPoints = new Point2D[SourceSegments.Count * 2];
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                segmentEndPoints[2 * i] = SourceSegments[i].Start;
                segmentEndPoints[2 * i + 1] = SourceSegments[i].End;
            }

            for (int i = 0; i < segmentEndPoints.Length; i++)
            {
                if (!allowMovingEnds)
                {
                    double closestParam = ProjectedAxis.ClosestParameter(segmentEndPoints[i]);
                    if (Core.Query.AlmostEqual(closestParam, 0, SnapSolver.ModelTolerance) || Core.Query.AlmostEqual(closestParam, 1, SnapSolver.ModelTolerance))
                    {
                        continue;
                    }
                }
                for (int j = 0; j < additionalSplitLocations.Count; j++)
                {
                    if (segmentEndPoints[i].Distance(additionalSplitLocations[j]) <= snappingDistance)
                    {
                        segmentEndPoints[i] = additionalSplitLocations[j];
                        splitMade[j] = true;
                    }
                }
            }
            //recreate the segments
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                Segment2D snappedSegment = new Segment2D(segmentEndPoints[2 * i], segmentEndPoints[2 * i + 1]);
                SourceSegments[i] = snappedSegment;
            }

            // get segments' ends as split params
            List<double> splitParams = new List<double>();
            splitParams.Add(0.0);
            splitParams.Add(1.0);
            foreach (Segment2D segment in SourceSegments)
            {
                double fromParam = ProjectedAxis.ClosestParameter(segment.Start).Clamp(0, 1);
                double toParam = ProjectedAxis.ClosestParameter(segment.End).Clamp(0, 1);

                // check if the params are already on the list
                bool isNew = true; // check the START
                for (int i = 0; i < splitParams.Count; i++)
                {
                    if (Core.Query.AlmostEqual(splitParams[i], fromParam, SnapSolver.SAMTolerance))
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
                    if (Core.Query.AlmostEqual(splitParams[i], toParam, SnapSolver.SAMTolerance))
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
                double newSplit = ProjectedAxis.ClosestParameter(additionalSplitLocations[i]);           
                newSplit = newSplit.Clamp(0, 1);
                bool isNew = true; // same for the END param
                for (int j = 0; j < splitParams.Count; j++)
                {
                    if (Core.Query.AlmostEqual(splitParams[j], newSplit, SnapSolver.SAMTolerance))
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
            splitParams = splitParams.Where(t => !double.IsNaN(t)).ToList();

            List<Segment2D> splitSegments = new List<Segment2D>();
            List<List<int>> sourceIndices = new List<List<int>>();
            for (int i = 0; i < splitParams.Count - 1; i++)
            {
                Segment2D piece = new Segment2D(ProjectedAxis.GetPoint(splitParams[i]), ProjectedAxis.GetPoint(splitParams[i + 1]));
                // shorten the current piece to avoid taking neighbours' indices
                Segment2D testPiece = piece.Clone<Segment2D>();
                testPiece = testPiece.Extend(-0.49 * SnapSolver.MinWallSegmentLength, true, true);
                List<int> indices = new List<int>();
                for (int j = 0; j < SourceSegments.Count; j++)
                {
                    if (testPiece.Distance(SourceSegments[j]) < SnapSolver.ModelTolerance)
                    {
                        indices.Add(SourceIndices[j]);
                    }
                }
                splitSegments.Add(piece);
                sourceIndices.Add(indices);
            }

            // create a new wall from each segment
            List<SnappedWall> splitWalls = new List<SnappedWall>();
            for (int i = 0; i < splitSegments.Count; i++)
            {
                if (splitSegments[i].GetLength() < SnapSolver.SAMTolerance || sourceIndices[i].Count < 1)
                {
                    continue;
                }
                Segment2D newAxis = splitSegments[i];
                var newAxis3D = new Segment3D(
                    new Point3D(newAxis.Start.X, newAxis.Start.Y, Elevation),
                    new Point3D(newAxis.End.X, newAxis.End.Y, Elevation));
                //var newAxis3D = SnapSolver.ProjectionPlane.Convert(newAxis);

                //newAxis3D.GetMoved(new Vector3D(0, 0, Elevation));
                SnappedWall wallSegment = new SnappedWall(sourceIndices[i][0], newAxis3D, Weight, BucketSize, MaxExtension, OriginalHeight);
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
        private void SnapSourceSegments(Segment2D previousAxis, double snappingTolerance, bool stretchEnds = false)
        {
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                Segment2D currentSegment = SourceSegments[i];

                Point2D snappedStart = ProjectedAxis.Closest(currentSegment.Start, false);
                Point2D snappedEnd = ProjectedAxis.Closest(currentSegment.End, false);

                if (stretchEnds)
                {
                    Point2D previousStart = previousAxis.Closest(currentSegment.Start, false);
                    Point2D previousEnd = previousAxis.Closest(currentSegment.End, false);

                    if (previousStart.Distance(previousAxis.Start) <= snappingTolerance)
                    {
                        snappedStart = ProjectedAxis.Start;
                    }
                    if (previousStart.Distance(previousAxis.End) <= snappingTolerance)
                    {
                        snappedStart = ProjectedAxis.End;
                    }
                    if (previousEnd.Distance(previousAxis.Start) <= snappingTolerance)
                    {
                        snappedEnd = ProjectedAxis.Start;
                    }
                    if (previousEnd.Distance(previousAxis.End) <= snappingTolerance)
                    {
                        snappedEnd = ProjectedAxis.End;
                    }
                }

                // clamp to finite segment
                double startParam = ProjectedAxis.ClosestParameter(snappedStart);
                startParam = startParam.Clamp(0, 1);
                double endParam = ProjectedAxis.ClosestParameter(snappedEnd);
                endParam = endParam.Clamp(0, 1);

                snappedStart = ProjectedAxis.GetPoint(startParam);
                snappedEnd = ProjectedAxis.GetPoint(endParam);

                //null problem in some cases - TODO definitely fix that
                if (snappedStart == null || snappedStart.IsNaN() || snappedEnd == null || snappedEnd.IsNaN())
                {
                    snappedStart = ProjectedAxis.Start;
                    snappedEnd = ProjectedAxis.End;
                    // Keep the safe fallback but make the previously invisible failure investigable:
                    // record which source wall hit the null/NaN path so the root cause can be traced.
                    SnapSolver.SolverWarnings.Add("SnappedWall (source " + (SourceIndices.Count > 0 ? SourceIndices[0] : -1) + "): a snapped endpoint was null/NaN; fell back to the original projected axis.");
                }


                if (snappedStart.Distance(ProjectedAxis.Start) <= snappingTolerance)
                {
                    snappedStart = ProjectedAxis.Start;
                }
                if (snappedEnd.Distance(ProjectedAxis.End) <= snappingTolerance)
                {
                    snappedEnd = ProjectedAxis.End;
                }

                SourceSegments[i] = new Segment2D(snappedStart, snappedEnd);
            }

            // now snap the points together to remove gaps
            Point2D[] segmentEndPoints = new Point2D[SourceSegments.Count * 2];
            for (int i = 0; i < SourceSegments.Count; i++)
            {
                segmentEndPoints[2 * i] = SourceSegments[i].Start;
                segmentEndPoints[2 * i + 1] = SourceSegments[i].End;
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
                    if (segmentEndPoints[i].Distance(segmentEndPoints[j]) <= snappingTolerance)
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
                    if (segmentEndPoints[p].Distance(ProjectedAxis.Start) <= snappingTolerance)
                    {
                        snapToStart = true;
                    }
                    if (segmentEndPoints[p].Distance(ProjectedAxis.End) <= snappingTolerance)
                    {
                        snapToEnd = true;
                    }
                }
                if (snapToStart && snapToEnd)
                { // the segment is probably very short
                    continue;
                }

                Point2D averagePt = new Point2D();
                if (snapToStart)
                {
                    averagePt = ProjectedAxis.Start;
                }
                else if (snapToEnd)
                {
                    averagePt = ProjectedAxis.End;
                }
                else
                { // calculate an average position
                    foreach (int p in indicesToSnap)
                    {
                        averagePt = new Point2D(averagePt.X + segmentEndPoints[p].X, averagePt.Y + segmentEndPoints[p].Y);
                    }
                    averagePt = new Point2D(averagePt.X / indicesToSnap.Count, averagePt.Y / indicesToSnap.Count);
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
                Segment2D snappedSegment = new Segment2D(segmentEndPoints[2 * i], segmentEndPoints[2 * i + 1]);
                SourceSegments[i] = snappedSegment;
            }
        }

        public bool IsRoughlyColinearWith(SnappedWall other, double angleToleranceRad)
        {
            double minAbsDot = System.Math.Cos(angleToleranceRad);
            Vector2D directionA = this.ProjectedAxis.Direction.Unit;
            Vector2D directionB = other.ProjectedAxis.Direction.Unit;

            double absDotProduct = System.Math.Abs(directionA * directionB);
            if (absDotProduct < minAbsDot) // too large difference in direction
            {
                return false;
            }
            return true;
        }

        private double MaxProjectionDistance(Segment2D lineA, Segment2D lineB)
       {
            // calculate average projection distance between lines' end points
            double maxDistance = lineA.Closest(lineB.Start, false).Distance(lineB.Start);
            maxDistance = System.Math.Max(maxDistance, lineA.Closest(lineB.End, false).Distance(lineB.End));

            return maxDistance;
        }


        private double LineProjectionDistance(Segment2D lineA, Segment2D lineB)
        {
            // calculate average projection distance between lines' end points
            double averageDistance = 0;
            averageDistance += lineA.Closest(lineB.Start, false).Distance(lineB.Start);
            averageDistance += lineA.Closest(lineB.End, false).Distance(lineB.End);
            averageDistance += lineB.Closest(lineA.Start, false).Distance(lineA.Start);
            averageDistance += lineB.Closest(lineA.End, false).Distance(lineA.End);
            averageDistance /= 4;

            return averageDistance;
        }
        private bool BucketContains(SnappedWall other, out bool fully)
        {
            fully = false;
            Segment2D dominantLine = this.ProjectedAxis;
            Segment2D otherLine = other.ProjectedAxis;
            double paramFrom = dominantLine.ClosestParameter(otherLine.Start);
            double paramTo = dominantLine.ClosestParameter(otherLine.End);

            double paramBucketMargin = System.Math.Min(this.MaxExtension, this.Length * ExtensionLimitLengthRatio) / this.Length;
            //double paramBucketMargin = this.MaxExtension / this.Length;
            Range<double> dominantRange = new Range<double>(-paramBucketMargin, 1 + paramBucketMargin);

            bool containsStart = dominantRange.In(paramFrom) || 
                paramFrom.AlmostEqual(dominantRange.Min) || 
                paramFrom.AlmostEqual(dominantRange.Max);
            bool containsEnd = dominantRange.In(paramTo) ||
                paramFrom.AlmostEqual(dominantRange.Min) ||
                paramFrom.AlmostEqual(dominantRange.Max);
            fully = containsStart && containsEnd;

            if (containsStart || containsEnd)
            {
                return true;
            }

            Range<double> otherRange = new Range<double>(paramFrom, paramTo);
            if (otherRange.In(dominantRange.Min) && otherRange.In(dominantRange.Max) ||
                otherRange.Min.AlmostEqual(dominantRange.Min) && otherRange.Max.AlmostEqual(dominantRange.Max))
            {
                fully = true;
                return true;
            }
            return false;
        }
    }
}