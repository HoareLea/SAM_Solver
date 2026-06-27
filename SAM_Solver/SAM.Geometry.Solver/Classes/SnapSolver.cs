// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Geometry.Solver
{
    //eliminate dataTrees, eleiminate gh references
    #region SnapSolver Description
    /// <summary>
    /// streszczenie całości - ogólne baaaardzo
    /// </summary>
    /// 
#endregion
    public class SnapSolver
    {
        public static Plane ProjectionPlane { get; } = Plane.WorldXY;
        private double _minTolerance = System.Math.Pow(10, -9);
        /// <summary>
        /// elevations will be rounded to 3 decimal places
        /// </summary>
        public static int ElevationToleranceDigits { get => _elevationToleranceDigits; private set { _elevationToleranceDigits = value; } }
        private static int _elevationToleranceDigits = 3;
        /// <summary>
        /// 
        /// </summary>
        public static double LevelSectionOffset { get => _levelSectionOffset; private set { _levelSectionOffset = value; } }
        private static double _levelSectionOffset = double.NaN;
        public static double DEFAULT_LevelSectionOffset { get => LEVEL_SECTION_OFFSET; }
        private const double LEVEL_SECTION_OFFSET = 0.25;
        /// <summary>
        /// 
        /// </summary>
        public static double NakedNodeSnapDistance { get => _nakedNodeSnapDistance; private set { _nakedNodeSnapDistance = value; } }
        private static double _nakedNodeSnapDistance = double.NaN;
        public static double DEFAULT_NakedNodeSnapDistance { get => NAKED_NODE_SNAP_DISTANCE; }
        private const double NAKED_NODE_SNAP_DISTANCE = 0.5;
        /// <summary>
        /// 10 centimeters
        /// </summary>
        public static double MinWallSegmentLength { get => _minWallSegmentLength; private set { _minWallSegmentLength = value; } }
        private static double _minWallSegmentLength = 0.1;
        public static double DEFAULT_MinWallSegmentLength { get => MIN_WALL_SEGMENT_LENGTH; }
        private const double MIN_WALL_SEGMENT_LENGTH = 0.1;
        /// <summary>
        /// for Rhino operations
        /// </summary>
        public static double ModelTolerance { get => _modelTolerance; private set { _modelTolerance = value; } }
        private static double _modelTolerance = System.Math.Pow(10, -3);
        /// <summary>
        /// necessary for SAM output
        /// </summary>
        /// 
        public static double SAMToleranceLarge { get; } = System.Math.Pow(10, -2);
        public static double SAMTolerance { get => _sAMTolerance; private set { _sAMTolerance = value; } }
        private static double _sAMTolerance = System.Math.Pow(10, -6);
        /// <summary>
        /// 5 degrees
        /// </summary>
        public static double ToleranceAngleRad { get => _toleranceAngleRad; private set { _toleranceAngleRad = value; } }
        private static double _toleranceAngleRad = 5 * (System.Math.PI / 180);
        /// <summary>
        /// 0.3 degrees
        /// </summary>
        public static double ArcToleranceAngleRad { get => _arcToleranceAngleRad; private set { _arcToleranceAngleRad = value; } }
        private static double _arcToleranceAngleRad = double.NaN;
        /// <summary>
        /// 0.001 degrees
        /// </summary>
        public static double ParallelToleranceAngleRad => 0.001 * (System.Math.PI / 180);
        public static double DEFAULT_ArcToleranceAngleRad { get => ARC_TOLERANCE_ANGLE_RAD; }
        private const double ARC_TOLERANCE_ANGLE_RAD = 0.3 * (System.Math.PI / 180);
        /// <summary>
        /// 
        /// </summary>
        public List<Face3D> PanelsFaces3D { get; private set; } = new List<Face3D>();
        /// <summary>
        /// 
        /// </summary>
        public List<double> BucketSizes { get; private set; } = new List<double>();
        /// <summary>
        /// 
        /// </summary>
        public List<double> Weights { get; private set; } = new List<double>();
        /// <summary>
        /// 
        /// </summary>
        public List<double> MaxExtensions { get; private set; } = new List<double>();
        /// <summary>
        /// 
        /// </summary>
        public List<Core.Range<double>> Levels { get; private set; } = new List<Core.Range<double>>();

        /// <summary>
        /// output
        /// </summary>
        public List<List<Face3D>> SnappedWalls { get; private set; } = new List<List<Face3D>>();
        /// <summary>
        /// output
        /// </summary>
        public List<List<Point3D>> NakedEnds { get; private set; } = new List<List<Point3D>>();
        /// <summary>
        /// output
        /// </summary>
        public List<List<List<int>>> SnappedSources { get; private set; } = new List<List<List<int>>>();

        private const double BUCKET_SIZE = 0.35;
        /// <summary>
        /// Upper bound on the SnapAndAdjustWalls fixed-point iteration so a non-converging
        /// model cannot hang the host. Mirrors the bounded loops in MergeColinearWalls / ExtensionSolver.
        /// </summary>
        private const int MAX_SNAP_ADJUST_ITERATIONS = 1000;
        /// <summary>
        /// Non-fatal diagnostics raised during the most recent solver operations
        /// (e.g. hitting an iteration cap). Inspect after a solve to detect degraded output.
        /// </summary>
        public static List<string> SolverWarnings { get; } = new List<string>();
        /// <summary>
        /// Enclosed-area change (m²) treated as noise when comparing closure signatures, so
        /// legitimate snap/trim jitter neither blocks a parallel-merge nor erases a real room.
        /// </summary>
        private const double AreaTolerance = 1e-3;
        /// <summary>
        /// A recognised loop counts as a real room only if the minimum side of its bounding
        /// rectangle is at least this (m). Smaller loops are slivers and are ignored. The parallel-merge
        /// guard raises this floor to the merge width (see <see cref="PerpendicularMergeTolerance"/>).
        /// </summary>
        private const double MinLoopRectangleSide = 0.2;
        /// <summary>
        /// When greater than zero, a final pass merges near-parallel, overlapping wall axes whose
        /// perpendicular offset is at most this distance (m) onto a single length-weighted axis -
        /// collapsing "double-wall" slits. 0 (default) disables it. The merge is closure-guarded per
        /// level: a merge that would reduce the count or enclosed area of rooms <b>wider than the merge
        /// width</b> is skipped. Choose a width below your narrowest real room/corridor - a genuine space
        /// narrower than the width is bounded by the same kind of parallel wall pair as a slit and cannot
        /// be told apart from one, so it may be collapsed.
        /// </summary>
        public static double PerpendicularMergeTolerance { get; set; } = 0;
        /// <summary>
        ///
        /// </summary>
        /// <param name="PanelsBrepInput"></param>
        /// <param name="BucketSizesInput"></param>
        /// <param name="WeightsInput"></param>
        /// <param name="MaxExtensionsInput"></param>
        /// <param name="LevelsInput"></param>
        /// <param name="LevelSectionOffsetInput"></param>
        /// <param name="NakedNodeSnapDistanceInput"></param>
        /// <param name="MinWallSegmentLengthInput"></param>
        /// <param name="ToleranceDistanceInput"></param>
        /// <param name="ToleranceAngleRadInput"></param>
        /// <param name="ArcToleranceAngleRadInput"></param>
        public SnapSolver(List<Face3D> PanelsBrepInput, List<double> BucketSizesInput, List<double> WeightsInput, List<double> MaxExtensionsInput,
            List<Core.Range<double>> LevelsInput, 
            double LevelSectionOffsetInput = LEVEL_SECTION_OFFSET, 
            double NakedNodeSnapDistanceInput = NAKED_NODE_SNAP_DISTANCE, 
            double MinWallSegmentLengthInput = MIN_WALL_SEGMENT_LENGTH,
            double ToleranceDistanceInput = Core.Tolerance.Distance, 
            double ToleranceAngleRadInput = Core.Tolerance.Angle, 
            double ArcToleranceAngleRadInput = ARC_TOLERANCE_ANGLE_RAD)
        {
            ModelTolerance = 0.001;
            SAMTolerance = ToleranceDistanceInput >= _minTolerance ? ToleranceDistanceInput : _minTolerance;

            LevelSectionOffset = LevelSectionOffsetInput;
            NakedNodeSnapDistance = NakedNodeSnapDistanceInput;
            MinWallSegmentLength = MinWallSegmentLengthInput;
            ToleranceAngleRad = ToleranceAngleRadInput;
            ArcToleranceAngleRad = ArcToleranceAngleRadInput;

            PanelsFaces3D = PanelsBrepInput;
            BucketSizes = BucketSizesInput;
            Weights = WeightsInput;
            MaxExtensions = MaxExtensionsInput;
            Levels = LevelsInput;
        }
        /// <summary>
        /// 
        /// </summary>
        public void Execute()
        {
            BucketSizes = AdjustListLength(BucketSizes, PanelsFaces3D.Count, defaultValue: 0.3);
            Weights = AdjustListLength(Weights, PanelsFaces3D.Count, defaultValue: 1.0);
            MaxExtensions = AdjustListLength(MaxExtensions, PanelsFaces3D.Count, defaultValue: 0.5);
            
            List<SnappedWall> snapped = RegisterWalls(PanelsFaces3D, BucketSizes, Weights, MaxExtensions, Levels, LevelSectionOffset);

            snapped = SolveStraightLine(snapped);

            if (PerpendicularMergeTolerance > 0)
            {
                snapped = MergeParallelWalls(snapped, PerpendicularMergeTolerance);
            }

            MarkNakedNodes(snapped); // metadata only (naked flags); never rolled back

            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = SortWallsByElevation(snapped);
            //GH_Path levelPath = new GH_Path(0);
            for (int i = 0; i < snappedWallsPerFloor.Keys.Count; i++)
            {
                NakedEnds.Add(new List<Point3D>());
                SnappedWalls.Add(new List<Face3D>());
                SnappedSources.Add(new List<List<int>>());
                List<SnappedWall> currentFloor = snappedWallsPerFloor[snappedWallsPerFloor.Keys[i]];
                for (int j = 0; j < currentFloor.Count; j++)
                {
                    List<List<int>> source = new List<List<int>>();
                    List<Face3D> wallSegments = currentFloor[j].GetFaces3D(out source);
                    SnappedWalls[i].AddRange(wallSegments);
                    SnappedSources[i].AddRange(source);
                    //foreach (var sour in source)
                    //{
                    //    SnappedSources[i].AddRange(sour);
                    //}

                    if (currentFloor[j].NakedStart)
                        NakedEnds[i].Add(ProjectionPlane.Convert(currentFloor[j].ProjectedAxis.Start));
                    if (currentFloor[j].NakedEnd)
                        NakedEnds[i].Add(ProjectionPlane.Convert(currentFloor[j].ProjectedAxis.End));
                }
            }
        }

        /// <summary>
        /// The solve pipeline: each pass runs exactly once, in order.
        /// </summary>
        private List<SnappedWall> SolveStraightLine(List<SnappedWall> snapped)
        {
            snapped = SnapAndAdjustWalls(snapped);
            TrimAndExtendWalls(snapped);
            snapped = ExplodeWallsAtIntersections(snapped);
            SnapOpenNodes(snapped, NakedNodeSnapDistance);
            snapped = CreateGraph(snapped, MinWallSegmentLength); // graph processing
            snapped = MergeColinearWalls(snapped, sameSourcePanelsOnly: true);
            return snapped;
        }

        private static SortedList<double, List<SnappedWall>> SortWallsByElevation(List<SnappedWall> walls)
        {
            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = new SortedList<double, List<SnappedWall>>();
            for (int i = 0; i < walls.Count; i++)
            {
                double currentLevel = System.Math.Round(walls[i].Elevation, ElevationToleranceDigits);
                if (!snappedWallsPerFloor.ContainsKey(currentLevel))
                {
                    snappedWallsPerFloor[currentLevel] = new List<SnappedWall>();
                }
                snappedWallsPerFloor[currentLevel].Add(walls[i]);
            }
            return snappedWallsPerFloor;
        }
        /// <summary>
        /// Number of significant closed loops (rooms) and their total enclosed area for one level.
        /// </summary>
        public struct ClosureSignature
        {
            public int LoopCount;
            public double Area;
        }

        /// <summary>
        /// Recognise closed loops from a set of 2D wall axes (via NTS polygonization) and summarise
        /// them, ignoring sliver loops whose bounding-rectangle minimum side is below
        /// <see cref="MinLoopRectangleSide"/>. Pure and side-effect free for unit testing.
        /// </summary>
        public static ClosureSignature ComputeClosure(IEnumerable<Segment2D> axes, double tolerance)
        {
            return ComputeClosure(axes, tolerance, MinLoopRectangleSide);
        }

        /// <summary>
        /// As <see cref="ComputeClosure(IEnumerable{Segment2D}, double)"/>, but with an explicit minimum
        /// room side: loops whose bounding-rectangle minimum side is below <paramref name="minRoomSide"/>
        /// are treated as slivers and excluded. The parallel-merge guard raises this to the merge width
        /// so a gap the caller declared a "slit" is not mistaken for a room.
        /// </summary>
        public static ClosureSignature ComputeClosure(IEnumerable<Segment2D> axes, double tolerance, double minRoomSide)
        {
            ClosureSignature signature = new ClosureSignature { LoopCount = 0, Area = 0 };
            if (axes == null)
            {
                return signature;
            }

            List<Segment2D> segments = axes.Where(axis => axis != null).ToList();
            if (segments.Count < 3)
            {
                return signature; // cannot enclose a region
            }

            List<Polygon2D> loops = SAM.Geometry.Planar.Create.Polygon2Ds(segments, tolerance);
            if (loops == null)
            {
                return signature;
            }

            foreach (Polygon2D loop in loops)
            {
                if (loop == null)
                {
                    continue;
                }

                List<Point2D> points = loop.GetPoints();
                if (points == null || points.Count < 3)
                {
                    continue;
                }

                Rectangle2D rectangle = SAM.Geometry.Planar.Create.Rectangle2D(points);
                if (rectangle == null || System.Math.Min(rectangle.Width, rectangle.Height) < minRoomSide)
                {
                    continue; // sliver loop: not a real room, exclude from the signature
                }

                signature.LoopCount++;
                signature.Area += System.Math.Abs(loop.GetArea());
            }

            return signature;
        }

        /// <summary>
        /// Axis-aligned bounding box of a projected axis, optionally grown by <paramref name="expansion"/>.
        /// </summary>
        private static Envelope AxisEnvelope(Segment2D axis, double expansion)
        {
            Point2D start = axis.Start;
            Point2D end = axis.End;
            Envelope envelope = new Envelope(
                System.Math.Min(start.X, end.X), System.Math.Max(start.X, end.X),
                System.Math.Min(start.Y, end.Y), System.Math.Max(start.Y, end.Y));
            if (expansion > 0)
            {
                envelope.ExpandBy(expansion);
            }
            return envelope;
        }

        /// <summary>
        /// Spatial index of wall projected-axis bounding boxes, keyed by list position.
        /// Querying an expanded envelope returns a superset of the walls the equivalent all-pairs
        /// loop would have examined, so callers keep their original inner checks unchanged.
        /// </summary>
        private static STRtree<int> BuildAxisIndex(List<SnappedWall> walls)
        {
            STRtree<int> index = new STRtree<int>();
            for (int i = 0; i < walls.Count; i++)
            {
                index.Insert(AxisEnvelope(walls[i].ProjectedAxis, 0), i);
            }
            return index;
        }

        private static void MarkNakedNodes(List<SnappedWall> walls)
        {
            if (walls.Count == 0)
            {
                return;
            }

            STRtree<int> index = BuildAxisIndex(walls);
            for (int i = 0; i < walls.Count; i++)
            {
                walls[i].ResetNakedStatus();
                // A wall can only affect wall i's naked status if it passes within SAMTolerance of
                // wall i's endpoints, i.e. its box overlaps wall i's box grown by SAMTolerance.
                foreach (int j in index.Query(AxisEnvelope(walls[i].ProjectedAxis, SAMTolerance)))
                {
                    if (i == j)
                    {
                        continue;
                    }
                    walls[i].UpdateNakedStatus(walls[j]);
                }
            }
        }
        private static List<SnappedWall> CreateGraph(List<SnappedWall> walls, double snappingDistance)
        {
            GraphSolver solver = new GraphSolver(walls.Select(w => w.ProjectedAxis).ToList(), 
                walls.Select(w => w.Weight).ToList(), snappingDistance);
            List<List<int>> sourceIndices = new List<List<int>>();
            List<Segment2D> newAxes = solver.Solve(out sourceIndices);

            List<SnappedWall> graph = new List<SnappedWall>();
            // different levels are taken into consideration
            for (int i = 0; i < newAxes.Count; i++)
            {
                Segment2D currentAxis = newAxes[i];
                List<SnappedWall> representedByThisAxis = new List<SnappedWall>();
                foreach (int j in sourceIndices[i])
                {
                    representedByThisAxis.Add(walls[j]);
                }
                SortedList<double, List<SnappedWall>> thisAxisSortedByLevels = SortWallsByElevation(representedByThisAxis);

                foreach (var floor in thisAxisSortedByLevels)
                {
                    for (int k = 0; k < floor.Value.Count; k++)
                    {
                        floor.Value[k].UpdateWithTrim(currentAxis);
                    }
                    double elevation = floor.Key;
                    Segment3D axis = new Segment3D(
                        new Point3D(currentAxis.Start.X, currentAxis.Start.Y, elevation),
                        new Point3D(currentAxis.End.X, currentAxis.End.Y, elevation)
                        );
                    //Segment3D axis = ProjectionPlane.Convert(currentAxis);
                    //axis = axis.GetMoved(new Vector3D(0, 0, elevation));
                    // TODO: why those values are assigned to all new walls?
                    List<int> sources = floor.Value.SelectMany(wall => wall.SourceIndices).ToList();
                    double weight = floor.Value.Select(wall => wall.Weight).Max();
                    double bucketSize = floor.Value.Select(wall => wall.BucketSize).Max();
                    double maxExtension = floor.Value.Select(wall => wall.MaxExtension).Max();
                    Core.Range<double> height = floor.Value.Select(wall => wall.OriginalHeight).First();

                    SnappedWall mergedWall = new SnappedWall(sources[0], axis, weight, bucketSize, maxExtension, height);
                    for (int m = 1; m < sources.Count; m++)
                    {
                        mergedWall.SourceIndices.Add(sources[m]);
                        mergedWall.SourceSegments.Add(currentAxis); // again... source segments have to be projected to the proper level
                    }
                    graph.Add(mergedWall);
                }
            }

            return graph;
        }
        private static List<SnappedWall> MergeColinearWalls(List<SnappedWall> walls, bool sameSourcePanelsOnly = true)
        {
            double angleRadTol = ParallelToleranceAngleRad;
            double distTol = ModelTolerance;

            SortedList<double, List<SnappedWall>> sortedByElevation = SortWallsByElevation(walls);

            List<SnappedWall> merged = new List<SnappedWall>();
            foreach (var floor in sortedByElevation)
            {
                List<SnappedWall> thisLevelWalls = floor.Value;
                bool[] processed = new bool[thisLevelWalls.Count];
                for (int i = 0; i < thisLevelWalls.Count; i++)
                {
                    if (processed[i])
                    {
                        continue;
                    }
                    processed[i] = true;
                    SnappedWall masterWall = thisLevelWalls[i];
                    Segment2D masterAxis = masterWall.ProjectedAxis;
                    HashSet<int> masterSources = new HashSet<int>(masterWall.SourceIndices);
                    double weight = masterWall.Weight;
                    double bucketSize = masterWall.BucketSize;
                    double maxExtension = masterWall.MaxExtension;
                    Core.Range<double> height = masterWall.OriginalHeight;

                    bool anyMatch = false;
                    int safetyCounter = 0;

                    List<int> colinearIndices = new List<int>();
                    // mark all colinear segments with the same sources
                    for (int j = 0; j < thisLevelWalls.Count; j++)
                    {
                        if (processed[j])
                        {
                            continue;
                        }
                        if (masterWall.IsRoughlyColinearWith(thisLevelWalls[j], angleRadTol))
                        {
                            HashSet<int> candidateSources = new HashSet<int>(thisLevelWalls[j].SourceIndices);
                            if (sameSourcePanelsOnly) { // additional check required
                                if (candidateSources.All(id => masterSources.Contains(id)) && candidateSources.Count == masterSources.Count) {
                                    colinearIndices.Add(j);
                                }
                            }
                            else {
                                colinearIndices.Add(j);
                            }
                        }
                    }

                    // recursively check which colinear segments touch the master wall and update the masterAxis
                    List<Point2D> anchors = new List<Point2D>();
                    anchors.Add(masterAxis.Start);
                    anchors.Add(masterAxis.End);
                    List<int> mergedColinearIndices = new List<int>();
                    do
                    {
                        foreach (int colinearId in colinearIndices)
                        {
                            if (processed[colinearId])
                            {
                                continue;
                            }
                            if (anchors.Any(a => OrthoDistance2d(thisLevelWalls[colinearId].ProjectedAxis.Start, a) < distTol) || 
                                anchors.Any(b => OrthoDistance2d(thisLevelWalls[colinearId].ProjectedAxis.End, b) < distTol))
                            {
                                processed[colinearId] = true;
                                anyMatch = true;
                                anchors.Add(thisLevelWalls[colinearId].ProjectedAxis.Start);
                                anchors.Add(thisLevelWalls[colinearId].ProjectedAxis.End);
                                mergedColinearIndices.Add(colinearId);
                            }
                        }
                        safetyCounter++;
                    } while (anyMatch && safetyCounter < 1000);

                    double elevation = floor.Key;

                    List<double> parameters = anchors.Select(a => masterAxis.ClosestParameter(a)).ToList();
                    double minParam = parameters.Min();
                    double maxParam = parameters.Max();

                    masterAxis = new Segment2D(anchors[parameters.IndexOf(minParam)], anchors[parameters.IndexOf(maxParam)]);
                    var masterAxis3D = new Segment3D(
                        new Point3D(masterAxis.Start.X, masterAxis.Start.Y, elevation),
                        new Point3D(masterAxis.End.X, masterAxis.End.Y, elevation));       

                    SnappedWall mergedWall = new SnappedWall(masterWall.SourceIndices[0], masterAxis3D, 
                        masterWall.Weight, masterWall.BucketSize, masterWall.MaxExtension, masterWall.OriginalHeight);
                    if (sameSourcePanelsOnly) {
                        var projectedMasterAxis = new Segment2D(
                            new Point2D(masterAxis.Start.X, masterAxis.Start.Y),
                            new Point2D(masterAxis.End.X, masterAxis.End.Y));

                        for (int m = 1; m < masterWall.SourceIndices.Count; m++) {
                            mergedWall.SourceIndices.Add(masterWall.SourceIndices[m]);
                            mergedWall.SourceSegments.Add(projectedMasterAxis); // source segments have to be projected to XY plane (this should be handled by a proper "AddSegment" method in SnappedWall class)
                        }
                    }
                    else {
                        mergedWall.SourceIndices[0] = masterWall.SourceIndices[0]; // replace the master axis
                        mergedWall.SourceSegments[0] = masterWall.SourceSegments[0];
                        for (int m = 1; m < masterWall.SourceIndices.Count; m++) {
                            mergedWall.SourceIndices.Add(masterWall.SourceIndices[m]);
                            mergedWall.SourceSegments.Add(masterWall.SourceSegments[m]);
                        }
                        // add all other segments
                        foreach (int mergedId in mergedColinearIndices) {
                            var currentMerged = thisLevelWalls[mergedId];
                            for (int m = 0; m < currentMerged.SourceIndices.Count; m++) {
                                mergedWall.SourceIndices.Add(currentMerged.SourceIndices[m]);
                                mergedWall.SourceSegments.Add(currentMerged.SourceSegments[m]);
                            }
                        }
                    }
                    merged.Add(mergedWall);
                }
            }
            return merged;
        }

        /// <summary>
        /// Merge near-parallel, overlapping wall axes whose perpendicular offset is within
        /// <paramref name="tolerance"/> onto a single length-weighted axis (collapsing "double-wall"
        /// slits). Closure-guarded per level: a level whose merge would reduce the count or enclosed area
        /// of rooms wider than the merge width is left unmerged, so any room wider than the chosen width
        /// is never erased (a genuine space narrower than the width is indistinguishable from a slit).
        /// </summary>
        private static List<SnappedWall> MergeParallelWalls(List<SnappedWall> walls, double tolerance)
        {
            if (walls == null || walls.Count == 0 || tolerance <= 0)
            {
                return walls;
            }

            // A gap up to the merge width is, by the caller's choice, a "slit" - so the guard must not
            // treat the thin loop it forms as a room. Protect only rooms wider than the merge width
            // (never below the normal sliver floor). Otherwise a 0.21 m slit reads as a 0.2 m+ room and
            // the merge is wrongly skipped.
            double minRoomSide = System.Math.Max(MinLoopRectangleSide, tolerance);

            List<SnappedWall> result = new List<SnappedWall>();
            foreach (KeyValuePair<double, List<SnappedWall>> floor in SortWallsByElevation(walls))
            {
                result.AddRange(MergeParallelOneLevel(floor.Value, floor.Key, tolerance, minRoomSide));
            }
            return result;
        }

        private static List<SnappedWall> MergeParallelOneLevel(List<SnappedWall> walls, double elevation, double tolerance, double minRoomSide)
        {
            int count = walls.Count;
            if (count < 2)
            {
                return new List<SnappedWall>(walls);
            }

            int[] parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (AreParallelWithinTolerance(walls[i].ProjectedAxis, walls[j].ProjectedAxis, tolerance))
                    {
                        UnionFind_Union(parent, i, j);
                    }
                }
            }

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < count; i++)
            {
                int root = UnionFind_Find(parent, i);
                if (!groups.TryGetValue(root, out List<int> group))
                {
                    group = new List<int>();
                    groups[root] = group;
                }
                group.Add(i);
            }

            // Closure of this level as-is, counting only rooms wider than the merge width. The open-end
            // count guards against a merge that bounds no room but still breaks connectivity (an averaged
            // axis that no longer lands on the perpendicular stubs that touched the original slit sides).
            List<Segment2D> originalAxes = walls.Select(wall => wall.ProjectedAxis).ToList();
            ClosureSignature baseline = ComputeClosure(originalAxes, ModelTolerance, minRoomSide);
            int baselineOpenEnds = OpenEndpointCount(originalAxes, ModelTolerance);

            List<SnappedWall> result = new List<SnappedWall>();
            foreach (List<int> group in groups.Values)
            {
                if (group.Count == 1)
                {
                    result.Add(walls[group[0]]);
                    continue;
                }

                SnappedWall mergedWall = BuildMergedParallelWall(walls, group, elevation);

                // Per-group guard: merge this group only if doing so (with every other wall left as-is)
                // does not reduce the count/area of rooms wider than the merge width, and does not open
                // any new naked end. This unblocks safe merges even when another group on the same level
                // must be protected.
                HashSet<int> groupMembers = new HashSet<int>(group);
                List<Segment2D> candidateAxes = new List<Segment2D>();
                for (int k = 0; k < count; k++)
                {
                    if (!groupMembers.Contains(k))
                    {
                        candidateAxes.Add(walls[k].ProjectedAxis);
                    }
                }
                candidateAxes.Add(mergedWall.ProjectedAxis);

                ClosureSignature after = ComputeClosure(candidateAxes, ModelTolerance, minRoomSide);
                int afterOpenEnds = OpenEndpointCount(candidateAxes, ModelTolerance);
                if (after.LoopCount < baseline.LoopCount || after.Area < baseline.Area - AreaTolerance)
                {
                    foreach (int id in group)
                    {
                        result.Add(walls[id]); // keep this group unmerged - it bounds a real room
                    }
                    SolverWarnings.Add("Parallel merge skipped for a wall group: it would reduce the count/area of rooms wider than the merge width.");
                }
                else if (afterOpenEnds > baselineOpenEnds)
                {
                    foreach (int id in group)
                    {
                        result.Add(walls[id]); // keep this group unmerged - merging would open a naked end
                    }
                    SolverWarnings.Add("Parallel merge skipped for a wall group: it would open a new naked end.");
                }
                else
                {
                    result.Add(mergedWall);
                }
            }
            return result;
        }

        /// <summary>
        /// Number of segment endpoints in <paramref name="axes"/> that are not within
        /// <paramref name="tolerance"/> of any other axis (i.e. open / naked ends). Used by the parallel
        /// merge guard to reject a merge that would disconnect a wall it previously touched.
        /// </summary>
        internal static int OpenEndpointCount(List<Segment2D> axes, double tolerance)
        {
            int open = 0;
            for (int i = 0; i < axes.Count; i++)
            {
                Segment2D axis = axes[i];
                if (axis == null)
                {
                    continue;
                }

                foreach (Point2D end in new[] { axis.Start, axis.End })
                {
                    bool connected = false;
                    for (int j = 0; j < axes.Count; j++)
                    {
                        if (j == i || axes[j] == null)
                        {
                            continue;
                        }

                        if (axes[j].Distance(end) <= tolerance)
                        {
                            connected = true;
                            break;
                        }
                    }

                    if (!connected)
                    {
                        open++;
                    }
                }
            }
            return open;
        }

        internal static bool AreParallelWithinTolerance(Segment2D a, Segment2D b, double tolerance)
        {
            Vector2D ua = a.Direction.Unit;
            Vector2D ub = b.Direction.Unit;
            if (System.Math.Abs((ua.X * ub.X) + (ua.Y * ub.Y)) < 0.99)
            {
                return false; // not parallel
            }

            Point2D a0 = a.Start;
            Point2D a1 = a.End;
            Point2D b0 = b.Start;
            Point2D b1 = b.End;

            // Require a real overlap along the shared axis (project both axes onto a's direction). Without
            // it, two staggered walls that only come close at adjacent endpoints - e.g. either side of a
            // doorway - would be grouped and bridged into one long wall across the gap. A midpoint-only
            // test missed short-overlap slits; a minimum-distance-only test over-merges these end-to-end
            // pairs. Overlap + perpendicular gap captures genuine "double-wall" slits and nothing else.
            double ta0 = (a0.X * ua.X) + (a0.Y * ua.Y);
            double ta1 = (a1.X * ua.X) + (a1.Y * ua.Y);
            double tb0 = (b0.X * ua.X) + (b0.Y * ua.Y);
            double tb1 = (b1.X * ua.X) + (b1.Y * ua.Y);

            // Degenerate (zero-length) axes can't form a slit.
            if (System.Math.Abs(ta1 - ta0) <= ModelTolerance || System.Math.Abs(tb1 - tb0) <= ModelTolerance)
            {
                return false;
            }

            double lo = System.Math.Max(System.Math.Min(ta0, ta1), System.Math.Min(tb0, tb1));
            double hi = System.Math.Min(System.Math.Max(ta0, ta1), System.Math.Max(tb0, tb1));
            if (hi - lo <= ModelTolerance)
            {
                return false; // no axial overlap: not a double-wall slit
            }

            // The two axes may be slightly skewed within the 0.99 direction tolerance, so the perpendicular
            // gap varies along the overlap. Measure it at both ends of the shared overlap (points at the
            // same axial coordinate differ only perpendicularly) and require the larger within tolerance,
            // so a pair that is close at one end but splays apart at the other is not merged.
            double gapLo = PointAtAxial(a0, a1, ta0, ta1, lo).Distance(PointAtAxial(b0, b1, tb0, tb1, lo));
            double gapHi = PointAtAxial(a0, a1, ta0, ta1, hi).Distance(PointAtAxial(b0, b1, tb0, tb1, hi));
            return System.Math.Max(gapLo, gapHi) <= tolerance;
        }

        /// <summary>
        /// Point on the segment p0->p1 at axial coordinate <paramref name="t"/> (the projection onto the
        /// shared direction, where <paramref name="t0"/>/<paramref name="t1"/> are the projections of
        /// p0/p1). Used to compare the two parallel axes at the same axial position.
        /// </summary>
        private static Point2D PointAtAxial(Point2D p0, Point2D p1, double t0, double t1, double t)
        {
            double denom = t1 - t0;
            double s = System.Math.Abs(denom) < 1e-12 ? 0 : (t - t0) / denom;
            return new Point2D(p0.X + (s * (p1.X - p0.X)), p0.Y + (s * (p1.Y - p0.Y)));
        }

        private static SnappedWall BuildMergedParallelWall(List<SnappedWall> walls, List<int> group, double elevation)
        {
            int lead = group[0];
            double bestLength = walls[lead].ProjectedAxis.GetLength();
            foreach (int id in group)
            {
                double length = walls[id].ProjectedAxis.GetLength();
                if (length > bestLength)
                {
                    bestLength = length;
                    lead = id;
                }
            }

            Segment2D leadAxis = walls[lead].ProjectedAxis;
            Vector2D dir = leadAxis.Direction.Unit;
            double nx = -dir.Y;
            double ny = dir.X;
            Point2D anchor = leadAxis.Start;

            double weightSum = 0, perpSum = 0, minParam = double.MaxValue, maxParam = double.MinValue;
            foreach (int id in group)
            {
                Segment2D axis = walls[id].ProjectedAxis;
                double weight = axis.GetLength();
                if (weight <= 0)
                {
                    weight = 1e-6;
                }
                foreach (Point2D point in new Point2D[] { axis.Start, axis.End })
                {
                    double param = ((point.X - anchor.X) * dir.X) + ((point.Y - anchor.Y) * dir.Y);
                    double perp = ((point.X - anchor.X) * nx) + ((point.Y - anchor.Y) * ny);
                    perpSum += (weight / 2.0) * perp; // each endpoint carries half the wall's weight
                    if (param < minParam) minParam = param;
                    if (param > maxParam) maxParam = param;
                }
                weightSum += weight;
            }
            double perpAverage = weightSum > 0 ? perpSum / weightSum : 0;

            Point2D start2D = new Point2D(anchor.X + (minParam * dir.X) + (perpAverage * nx), anchor.Y + (minParam * dir.Y) + (perpAverage * ny));
            Point2D end2D = new Point2D(anchor.X + (maxParam * dir.X) + (perpAverage * nx), anchor.Y + (maxParam * dir.Y) + (perpAverage * ny));

            Segment3D axis3D = new Segment3D(
                new Point3D(start2D.X, start2D.Y, elevation),
                new Point3D(end2D.X, end2D.Y, elevation));

            SnappedWall master = walls[lead];
            SnappedWall mergedWall = new SnappedWall(master.SourceIndices[0], axis3D, master.Weight, master.BucketSize, master.MaxExtension, master.OriginalHeight);

            Segment2D projected = new Segment2D(start2D, end2D);
            for (int m = 1; m < master.SourceIndices.Count; m++)
            {
                mergedWall.SourceIndices.Add(master.SourceIndices[m]);
                mergedWall.SourceSegments.Add(projected);
            }
            foreach (int id in group)
            {
                if (id == lead)
                {
                    continue;
                }
                SnappedWall other = walls[id];
                for (int m = 0; m < other.SourceIndices.Count; m++)
                {
                    mergedWall.SourceIndices.Add(other.SourceIndices[m]);
                    mergedWall.SourceSegments.Add(projected);
                }
            }
            return mergedWall;
        }

        private static int UnionFind_Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void UnionFind_Union(int[] parent, int a, int b)
        {
            int rootA = UnionFind_Find(parent, a);
            int rootB = UnionFind_Find(parent, b);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
            }
        }

        private static double OrthoDistance2d(Point2D ptA, Point2D ptB)
        {
            double distance = System.Math.Abs(ptA.X - ptB.X);
            distance += System.Math.Abs(ptA.Y - ptB.Y);
            return distance;
        }

        private static List<SnappedWall> ExplodeWallsAtIntersections(List<SnappedWall> walls)
        {
            List<SnappedWall> split = new List<SnappedWall>();

            STRtree<int> index = walls.Count > 0 ? BuildAxisIndex(walls) : null;
            // Both axes are extended by ModelTolerance before the intersection test (within SAMTolerance),
            // so a generous box grows by twice that extension plus the test tolerance to stay a superset.
            double explodeExpansion = (2 * ModelTolerance) + SAMTolerance;
            for (int i = 0; i < walls.Count; i++)
            {
                // find all intersections between this wall and walls on the same level
                List<Point2D> intersections = new List<Point2D>();
                foreach (int j in index.Query(AxisEnvelope(walls[i].ProjectedAxis, explodeExpansion)))
                {
                    if (i == j)
                    {
                        continue;
                    }
                    if (!Core.Query.AlmostEqual(walls[i].Elevation, walls[j].Elevation, ModelTolerance))
                    {// in this version of the algorithm the split is created even if the walls are on different levels. Uncomment "continue" to split only on the same level
                     //continue;
                    }
                    Segment2D currentExtended = walls[i].ProjectedAxis;
                    Segment2D otherExtended = walls[j].ProjectedAxis;
                    currentExtended = currentExtended.Extend(ModelTolerance, true, true);
                    otherExtended = otherExtended.Extend(ModelTolerance, true, true);
                    if(currentExtended.Intersect(otherExtended, SAMTolerance))
                    {
                        intersections.Add(currentExtended.Intersection(otherExtended, true, SAMTolerance));
                    }
                }

                var currentSplit = walls[i].SnapSegmentsSplitAndExplode(intersections, MinWallSegmentLength, false);
                for (int j = 0; j < currentSplit.Count; j++)
                {
                    split.Add(currentSplit[j]);
                }
            }
            MarkNakedNodes(split);
            return split;
        }

        private static void SnapOpenNodes(List<SnappedWall> walls, double snappingDistance)
        {
            List<SnappedWall> wallsByLength = walls.OrderBy(w => w.Length).ToList(); // start snapping from the shortest
            STRtree<int> index = wallsByLength.Count > 0 ? BuildAxisIndex(wallsByLength) : null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < wallsByLength.Count; i++)
            {
                SnappedWall currentWall = wallsByLength[i];
                //Print("Current wall [{0}] , its first source: {1}", i, currentWall.SourceIndices[0]);
                List<Point2D> anchorCandidates = new List<Point2D>();
                // Only walls within the snap reach (MaxExtension) can supply anchors; sort the queried
                // candidates so the anchor ordering matches the original ascending scan exactly.
                List<int> candidateIndices = index.Query(AxisEnvelope(currentWall.ProjectedAxis, currentWall.MaxExtension)).ToList();
                candidateIndices.Sort();
                foreach (int j in candidateIndices)
                {
                    if (!Core.Query.AlmostEqual(currentWall.Elevation, wallsByLength[j].Elevation, ModelTolerance)) // same level only
                    {
                        continue;
                    }
                    if (i == j)
                    {
                        continue;
                    }
                    anchorCandidates.Add(wallsByLength[j].ProjectedAxis.Start);
                    anchorCandidates.Add(wallsByLength[j].ProjectedAxis.End);
                }
                //Print("Current wall [{0}] anchor count: {1}, its first source: {2}", i, anchorCandidates.Count, currentWall.SourceIndices[0]);
                string report = "";
                //bool snapped = currentWall.TrySnapIfNaked(anchorCandidates, snappingDistance, out report);
                bool snapped = currentWall.TrySnapIfNaked(anchorCandidates, currentWall.MaxExtension, out report);
                sb.AppendLine(report);
            }
            //walls.RemoveAll(w => w.ProjectedAxis.GetLength() < SAM.Core.Tolerance.Distance);
            //string text = sb.ToString();
            //sb.AppendLine("End");
        }
        private static List<SnappedWall> SnapAndAdjustWalls(List<SnappedWall> walls)
        {

            // arrange the walls from the most to the least important
            walls = walls.OrderBy(w => w.Length).ToList(); // ternary order
            walls = walls.OrderBy(w => w.BucketSize).ToList(); // secondary order
            walls = walls.OrderBy(w => w.Weight).ToList(); // primary order
            walls.Reverse(); // from the largest to the smallest
                             // the walls are now arranged first by weight, then by bucket size

            // create a list of walls that will be iteratively modified
            List<SnappedWall> processedWalls = new List<SnappedWall>(walls);

            bool anySnapped = false;
            // snap iteratively until there are no changes in the model
            int safetyCounter = 0;
            do
            {
                anySnapped = false;
                bool[] isSnapped = new bool[processedWalls.Count];
                bool[] isMerged = new bool[processedWalls.Count];

                // Left as an all-pairs scan deliberately: `current` absorbs candidates and grows during
                // the pass, so a precomputed bounding-box filter could miss walls that come into reach
                // after a merge. The outer loop is now bounded by MAX_SNAP_ADJUST_ITERATIONS.
                for (int i = 0; i < processedWalls.Count - 1; i++)
                {
                    if (isMerged[i]) {
                        continue;
                    }
                    SnappedWall current = processedWalls[i];
                    for (int j = i + 1; j < processedWalls.Count; j++)
                    {
                        if (isSnapped[j] || isMerged[j])
                        {
                            continue;
                        }
                        SnappedWall candidate = processedWalls[j];
                        bool mergedIn = false;
                        if (current.TryBucketSnap(candidate, out mergedIn))
                        {
                            anySnapped = true;
                            isSnapped[j] = true;
                            if (mergedIn)
                            {
                                isMerged[j] = true;
                                //Print("Wall {0} absorbed wall {1}", current.SourceIndices[0], candidate.SourceIndices[0]);
                                //Print("Slave's segments:");
                                //foreach (Line line in candidate.SourceSegments) {
                                //    Print(line.EndString());
                                //}
                                //Print("Updated master's segments:");
                                //foreach (Line line in current.SourceSegments) {
                                //    Print(line.EndString());
                                //}
                            }
                        }
                    }
                }
                List<SnappedWall> allButMerged = new List<SnappedWall>();
                for (int i = 0; i < processedWalls.Count; i++)
                {
                    if (!isMerged[i])
                    {
                        allButMerged.Add(processedWalls[i]);
                    }
                }
                processedWalls = allButMerged;

                safetyCounter++;
            } while (anySnapped == true && safetyCounter < MAX_SNAP_ADJUST_ITERATIONS);

            if (safetyCounter >= MAX_SNAP_ADJUST_ITERATIONS && anySnapped)
            {
                // The loop is bounded so a non-converging model can no longer hang the host;
                // surface the event instead of stopping silently.
                SolverWarnings.Add("SnapAndAdjustWalls reached the iteration cap (" + MAX_SNAP_ADJUST_ITERATIONS + "); the snapped model may be incomplete.");
            }

            MarkNakedNodes(processedWalls);

            return processedWalls;
        }
        private static void TrimAndExtendWalls(List<SnappedWall> walls)
        {
            var wallsPerFloor = SortWallsByElevation(walls);
            foreach (var floor in wallsPerFloor)
            {
                var axes = floor.Value.Select(wall => wall.ProjectedAxis).ToList();
                var extensions = floor.Value.Select(wall => wall.MaxExtension).ToList();
                ExtensionSolver solver = new ExtensionSolver(axes, extensions, SAMTolerance);
                var newAxes = solver.Solve();
                for (int i = 0; i < floor.Value.Count; i++)
                {
                    floor.Value[i].UpdateWithTrim(newAxes[i]);
                }
            }

            MarkNakedNodes(walls);
        }
        private static List<SnappedWall> RegisterWalls(List<Face3D> panelsBrep, List<double> bucketSizes, List<double> weights, List<double> maxExtensions, List<Core.Range<double>> levels, double levelOffset)
        {
            var walls = new List<SnappedWall>();

            for (int j = 0; j < levels.Count; j++)
            {
                double sectionHeight = levels[j].Min + levelOffset;
                Core.Range<double> currentHeight = levels[j];
                Plane currentPlane = new Plane(Plane.WorldXY, new Point3D(0,0, sectionHeight));

                for (int i = 0; i < panelsBrep.Count; i++)
                {
                    Face3D thiswall = panelsBrep[i];
                    List<Segment3D> intc = null;

                    //if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, currentPlane, SAM_SnapSolver.ModelTolerance, out intc, out intp))
                    if(thiswall.Intersecting(currentPlane, out intc))
                    {
                        foreach (Segment3D segment in intc)
                        {
                            Segment3D section3D = new Segment3D(segment.GetStart(), segment.GetEnd());
                            SnappedWall wall = new SnappedWall(i, section3D, weights[i], bucketSizes[i], maxExtensions[i], currentHeight);
                            walls.Add(wall);
                        }
                    }
                }
            }
            return walls;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetCount"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private static List<double> AdjustListLength(List<double> values, int targetCount, double defaultValue)
        {
            List<double> newValues = new List<double>();
            if (values == null || values.Count == 0)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    newValues.Add(defaultValue);
                }
            }
            else if (values.Count >= targetCount)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    newValues.Add(values[i]);
                }
            }
            else
            {
                for (int i = 0; i < values.Count; i++)
                {
                    newValues.Add(values[i]);
                }
                double lastValue = values[values.Count - 1];
                int surplus = targetCount - values.Count;
                for (int i = 0; i < surplus; i++)
                {
                    //newValues.Add(lastValue);
                    newValues.Add(defaultValue);
                }
            }

            return newValues;
        }
    }
}
