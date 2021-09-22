using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Grasshopper.Solver.Classes
{
    //eliminate dataTrees, eleiminate gh references

    public class SnapSolver_GH
    {
        private double _minTolerance = Math.Pow(10, -9);
        private double _extensionLimiter = 0.49;
        private static double _modelTolerance = Math.Pow(10, -3);
        /// <summary>
        /// for Rhino operations
        /// </summary>
        public static double ModelTolerance { get => _modelTolerance; private set { _modelTolerance = value; }}
        private static double _sAMTolerance = Math.Pow(10, -6);
        /// <summary>
        /// necessary for SAM output
        /// </summary>
        public static double SAMTolerance { get => _sAMTolerance; private set { _sAMTolerance = value; } }
        private static double _toleranceAngleRad = 5 * (Math.PI / 180);
        /// <summary>
        /// 5 degrees
        /// </summary>
        public static double ToleranceAngleRad { get => _toleranceAngleRad; private set { _toleranceAngleRad = value; } }
        private static double _arcToleranceAngleRad = 0.3 * (Math.PI / 180);
        /// <summary>
        /// 0.3 degrees
        /// </summary>
        public static double ArcToleranceAngleRad { get => _arcToleranceAngleRad; private set { _arcToleranceAngleRad = value; } }
        private static double _minWallSegmentLength = 0.1;
        /// <summary>
        /// 10 centimeters
        /// </summary>
        public static double MinWallSegmentLength { get => _minWallSegmentLength; private set { _minWallSegmentLength = value; } }
        private static int _elevationToleranceDigits = 3;
        /// <summary>
        /// elevations will be rounded to 3 decimal places
        /// </summary>
        public static int ElevationToleranceDigits { get => _elevationToleranceDigits; private set { _elevationToleranceDigits = value; } }
        /// <summary>
        /// 
        /// </summary>
        public List<Brep> PanelsBrep { get; private set; } = new List<Brep>();
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
        public List<Interval> Levels { get; private set; } = new List<Interval>();
        /// <summary>
        /// 
        /// </summary>
        public double LevelSectionOffset { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public double NakedNodeSnapDistance { get; private set; }

        /// <summary>
        /// output
        /// </summary>
        public DataTree<Brep> SnappedWalls { get; private set; } = new DataTree<Brep>();
        /// <summary>
        /// output
        /// </summary>
        public DataTree<Point3d> NakedEnds { get; private set; } = new DataTree<Point3d>();
        /// <summary>
        /// output
        /// </summary>
        public DataTree<int> SnappedSources { get; private set; } = new DataTree<int>();

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
        public SnapSolver_GH(List<Brep> PanelsBrepInput, List<double> BucketSizesInput, List<double> WeightsInput, List<double> MaxExtensionsInput,
            List<Interval> LevelsInput, double LevelSectionOffsetInput, double NakedNodeSnapDistanceInput, double MinWallSegmentLengthInput,
            double ToleranceDistanceInput, double ToleranceAngleRadInput, double ArcToleranceAngleRadInput)
        {
            ModelTolerance = 0.001;
            SAMTolerance = ToleranceDistanceInput >= _minTolerance ? ToleranceDistanceInput : _minTolerance;
            ToleranceAngleRad = ToleranceAngleRadInput;
            ArcToleranceAngleRad = ArcToleranceAngleRadInput;
            MinWallSegmentLength = MinWallSegmentLengthInput;

            PanelsBrep = PanelsBrepInput;
            BucketSizes = BucketSizesInput;
            Weights = WeightsInput;
            MaxExtensions = MaxExtensionsInput;
            Levels = LevelsInput;
            LevelSectionOffset = LevelSectionOffsetInput;
            NakedNodeSnapDistance = NakedNodeSnapDistanceInput;
        }
        /// <summary>
        /// 
        /// </summary>
        public void Execute()
        {
            BucketSizes = AdjustListLength(BucketSizes, PanelsBrep.Count, defaultValue: 0.3);
            Weights = AdjustListLength(Weights, PanelsBrep.Count, defaultValue: 1.0);
            MaxExtensions = AdjustListLength(MaxExtensions, PanelsBrep.Count, defaultValue: 0.5);

            List<SnappedWall> walls = RegisterWalls(PanelsBrep, BucketSizes, Weights, MaxExtensions, Levels, LevelSectionOffset, _extensionLimiter);
            List<SnappedWall> snapped = SnapAndAdjustWalls(walls);
            TrimAndExtendWalls(snapped);
            snapped = ExplodeWallsAtIntersections(snapped);
            SnapOpenNodes(snapped, NakedNodeSnapDistance);
            snapped = CreateGraph(snapped, MinWallSegmentLength); // graph processing
            snapped = MergeColinearWalls(snapped);
            MarkNakedNodes(snapped);

            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = SortWallsByElevation(snapped);
            GH_Path levelPath = new GH_Path(0);
            foreach (double level in snappedWallsPerFloor.Keys)
            {
                SnappedWalls.EnsurePath(levelPath);
                NakedEnds.EnsurePath(levelPath);
                List<SnappedWall> currentFloor = snappedWallsPerFloor[level];
                int segmentCount = 0;
                for (int i = 0; i < currentFloor.Count; i++)
                {
                    List<List<int>> source = new List<List<int>>();
                    List<Brep> wallSegments = currentFloor[i].GetBreps(out source);
                    for (int j = 0; j < wallSegments.Count; j++)
                    {
                        GH_Path wallPath = levelPath.AppendElement(segmentCount);
                        SnappedSources.EnsurePath(wallPath);
                        SnappedWalls.Add(wallSegments[j], levelPath);
                        SnappedSources.AddRange(source[j], wallPath);
                        segmentCount++;
                    }

                    //GH_Path wallPath = levelPath.AppendElement(i);
                    //snappedSources.EnsurePath(wallPath);
                    //snappedWalls.Add(currentFloor[i].GetBrep(), levelPath);
                    //snappedSources.AddRange(currentFloor[i].SourceIndices, wallPath);

                    if (currentFloor[i].NakedStart)
                        NakedEnds.Add(currentFloor[i].ProjectedAxis.From, levelPath);
                    if (currentFloor[i].NakedEnd)
                        NakedEnds.Add(currentFloor[i].ProjectedAxis.To, levelPath);
                }
                levelPath = levelPath.Increment(0);
            }
        }
        private static SortedList<double, List<SnappedWall>> SortWallsByElevation(List<SnappedWall> walls)
        {
            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = new SortedList<double, List<SnappedWall>>();
            for (int i = 0; i < walls.Count; i++)
            {
                double currentLevel = Math.Round(walls[i].Elevation, SnapSolver_GH.ElevationToleranceDigits);
                if (!snappedWallsPerFloor.ContainsKey(currentLevel))
                {
                    snappedWallsPerFloor[currentLevel] = new List<SnappedWall>();
                }
                snappedWallsPerFloor[currentLevel].Add(walls[i]);
            }
            return snappedWallsPerFloor;
        }
        private static void MarkNakedNodes(List<SnappedWall> walls)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                walls[i].ResetNakedStatus();
                for (int j = 0; j < walls.Count; j++)
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
            GraphSolver solver = new GraphSolver(walls.Select(w => w.ProjectedAxis).ToList(), walls.Select(w => w.Weight).ToList(), snappingDistance);
            List<List<int>> sourceIndices = new List<List<int>>();
            List<Line> newAxes = solver.Solve(out sourceIndices);

            List<SnappedWall> graph = new List<SnappedWall>();
            // different levels are taken into consideration
            for (int i = 0; i < newAxes.Count; i++)
            {
                Line currentAxis = newAxes[i];
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
                    Line axis = currentAxis;
                    axis.Transform(Transform.Translation(new Vector3d(0, 0, elevation)));
                    // TODO: why those values are assigned to all new walls?
                    List<int> sources = floor.Value.SelectMany(wall => wall.SourceIndices).ToList();
                    double weight = floor.Value.Select(wall => wall.Weight).Max();
                    double bucketSize = floor.Value.Select(wall => wall.BucketSize).Max();
                    double maxExtension = floor.Value.Select(wall => wall.MaxExtension).Max();
                    Interval height = floor.Value.Select(wall => wall.OriginalHeight).First();

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
        private static List<SnappedWall> MergeColinearWalls(List<SnappedWall> walls)
        {
            double angleRadTol = 0.01;
            double distTol = 0.001;

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
                    Line masterAxis = masterWall.ProjectedAxis;
                    HashSet<int> masterSources = new HashSet<int>(masterWall.SourceIndices);
                    double weight = masterWall.Weight;
                    double bucketSize = masterWall.BucketSize;
                    double maxExtension = masterWall.MaxExtension;
                    Interval height = masterWall.OriginalHeight;

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
                            if (thisLevelWalls[j].SourceIndices.All(id => masterSources.Contains(id)))
                                colinearIndices.Add(j);
                        }
                    }

                    // recursively check which colinear segments touch the master wall and update the masterAxis
                    List<Point3d> anchors = new List<Point3d>();
                    anchors.Add(masterAxis.From);
                    anchors.Add(masterAxis.To);
                    do
                    {
                        foreach (int colinearId in colinearIndices)
                        {
                            if (processed[colinearId])
                            {
                                continue;
                            }
                            if (anchors.Any(a => OrthoDistance2d(thisLevelWalls[colinearId].ProjectedAxis.From, a) < distTol) || anchors.Any(b => OrthoDistance2d(thisLevelWalls[colinearId].ProjectedAxis.To, b) < distTol))
                            {
                                processed[colinearId] = true;
                                anyMatch = true;
                                anchors.Add(thisLevelWalls[colinearId].ProjectedAxis.From);
                                anchors.Add(thisLevelWalls[colinearId].ProjectedAxis.To);
                            }
                        }
                        safetyCounter++;
                    } while (anyMatch && safetyCounter < 100);

                    double elevation = floor.Key;

                    List<double> parameters = anchors.Select(a => masterAxis.ClosestParameter(a)).ToList();
                    double minParam = parameters.Min();
                    double maxParam = parameters.Max();

                    masterAxis = new Line(anchors[parameters.IndexOf(minParam)], anchors[parameters.IndexOf(maxParam)]);
                    masterAxis.Transform(Transform.Translation(new Vector3d(0, 0, elevation)));

                    SnappedWall mergedWall = new SnappedWall(masterWall.SourceIndices[0], masterAxis, masterWall.Weight, masterWall.BucketSize, masterWall.MaxExtension, masterWall.OriginalHeight);
                    for (int m = 1; m < masterWall.SourceIndices.Count; m++)
                    {
                        mergedWall.SourceIndices.Add(masterWall.SourceIndices[m]);
                        mergedWall.SourceSegments.Add(masterAxis); // again... source segments have to be projected to the proper level
                    }
                    merged.Add(mergedWall);
                }
            }
            return merged;
        }
        private static double OrthoDistance2d(Point3d ptA, Point3d ptB)
        {
            double distance = Math.Abs(ptA.X - ptB.X);
            distance += Math.Abs(ptA.Y - ptB.Y);
            return distance;
        }

        private static List<SnappedWall> ExplodeWallsAtIntersections(List<SnappedWall> walls)
        {
            List<SnappedWall> split = new List<SnappedWall>();

            for (int i = 0; i < walls.Count; i++)
            {
                // find all intersections between this wall and walls on the same level
                List<Point3d> intersections = new List<Point3d>();
                for (int j = 0; j < walls.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    if (!RhinoMath.EpsilonEquals(walls[i].Elevation, walls[j].Elevation, SnapSolver_GH.ModelTolerance))
                    {// are not on the same level, don't intersect
                     //continue;
                    }
                    double myParam = 0;
                    double theirParam = 0;
                    Line currentExtended = walls[i].ProjectedAxis;
                    Line otherExtended = walls[j].ProjectedAxis;
                    currentExtended.Extend(SnapSolver_GH.ModelTolerance, SnapSolver_GH.ModelTolerance);
                    otherExtended.Extend(SnapSolver_GH.ModelTolerance, SnapSolver_GH.ModelTolerance);
                    if (Rhino.Geometry.Intersect.Intersection.LineLine(currentExtended, otherExtended, out myParam, out theirParam, SnapSolver_GH.SAMTolerance, finiteSegments: true))
                    {
                        //StartExtensionParam = Math.Max(StartExtensionParam, myParam);
                        intersections.Add(currentExtended.PointAt(myParam));
                    }
                }
                //Print("Intersections:{0}", walls[i].Elevation);
                //foreach (Point3d point3D in intersections) {
                //    Print(point3D.ToString());
                //}
                var currentSplit = walls[i].SnapSegmentsSplitAndExplode(intersections, walls[i].MaxExtension, false);
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
            for (int i = 0; i < wallsByLength.Count; i++)
            {
                SnappedWall currentWall = wallsByLength[i];
                //Print("Current wall [{0}] , its first source: {1}", i, currentWall.SourceIndices[0]);
                List<Point3d> anchorCandidates = new List<Point3d>();
                for (int j = 0; j < wallsByLength.Count; j++)
                {
                    if (!RhinoMath.EpsilonEquals(currentWall.Elevation, wallsByLength[j].Elevation, SnapSolver_GH.ModelTolerance)) // same level only
                    {
                        continue;
                    }
                    if (i == j)
                    {
                        continue;
                    }
                    anchorCandidates.Add(wallsByLength[j].ProjectedAxis.From);
                    anchorCandidates.Add(wallsByLength[j].ProjectedAxis.To);
                }
                //Print("Current wall [{0}] anchor count: {1}, its first source: {2}", i, anchorCandidates.Count, currentWall.SourceIndices[0]);
                string report = "";
                bool snapped = currentWall.TrySnapIfNaked(anchorCandidates, snappingDistance, out report);
                //Print("Wall [{0}] / first source {1}: {2}", i, currentWall.SourceIndices[0], report);
            }
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
            do
            {
                anySnapped = false;
                bool[] isSnapped = new bool[processedWalls.Count];
                bool[] isMerged = new bool[processedWalls.Count];

                for (int i = 0; i < processedWalls.Count - 1; i++)
                {
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
                                //    Print(line.ToString());
                                //}
                                //Print("Updated master's segments:");
                                //foreach (Line line in current.SourceSegments) {
                                //    Print(line.ToString());
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

            } while (anySnapped == true);

            MarkNakedNodes(processedWalls);

            return processedWalls;
        }
        private static void TrimAndExtendWalls(List<SnappedWall> walls)
        {
            foreach (var wall in walls)
            {
                //Debug.Print("---Current wall elevation: {0:N12}", wall.Elevation);
            }

            var wallsPerFloor = SortWallsByElevation(walls);
            //Debug.Print("Number of walls: {0}", walls.Count);
            foreach (var floor in wallsPerFloor)
            {
                //Debug.Print("Floor: {0}, NumWalls: {1}", floor.Key, floor.Value.Count);
                var axes = floor.Value.Select(wall => wall.ProjectedAxis).ToList();
                var extensions = floor.Value.Select(wall => wall.MaxExtension).ToList();
                ExtensionSolver solver = new ExtensionSolver(axes, extensions, SnapSolver_GH.SAMTolerance);
                var newAxes = solver.Solve();
                for (int i = 0; i < floor.Value.Count; i++)
                {
                    floor.Value[i].UpdateWithTrim(newAxes[i]);
                }
            }

            MarkNakedNodes(walls);
        }
        private static List<SnappedWall> RegisterWalls(List<Brep> panelsBrep, List<double> bucketSizes, List<double> weights, List<double> maxExtensions, List<Interval> levels, double levelOffset, double extensionLimiter)
        {
            var walls = new List<SnappedWall>();

            for (int j = 0; j < levels.Count; j++)
            {
                double sectionHeight = levels[j].Min + levelOffset;
                Interval currentHeight = levels[j];
                Plane currentPlane = Plane.WorldXY;
                currentPlane.Translate(new Vector3d(0, 0, sectionHeight));

                for (int i = 0; i < panelsBrep.Count; i++)
                {
                    Brep thiswall = panelsBrep[i];

                    Curve[] intc = null;
                    Point3d[] intp = null;

                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(thiswall, currentPlane, SnapSolver_GH.ModelTolerance, out intc, out intp))
                    {
                        foreach (Curve curve in intc)
                        {
                            if (curve.IsLinear(SnapSolver_GH.ModelTolerance))
                            {
                                Line section = new Line(curve.PointAtStart, curve.PointAtEnd);
                                SnappedWall wall = new SnappedWall(i, section, weights[i], bucketSizes[i], Math.Min(maxExtensions[i], section.Length * extensionLimiter), currentHeight);
                                walls.Add(wall);
                            }
                        }
                    }
                }
            }

            return walls;
        }
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
