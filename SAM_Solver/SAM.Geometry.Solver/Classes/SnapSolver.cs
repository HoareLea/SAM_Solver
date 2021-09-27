using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Geometry.Solver
{
    //eliminate dataTrees, eleiminate gh references

    public class SnapSolver
    {
        public static Plane ProjectionPlane { get; } = Plane.WorldXY;
        //TODO: USE SAM tolerances where is meaningfull SAM.Core.Tolerance....
        private double _minTolerance = System.Math.Pow(10, -9);
        private double _extensionLimiter = 0.49;
        private static double _modelTolerance = System.Math.Pow(10, -3);
        /// <summary>
        /// for Rhino operations
        /// </summary>
        public static double ModelTolerance { get => _modelTolerance; private set { _modelTolerance = value; }}
        private static double _sAMTolerance = System.Math.Pow(10, -6);
        /// <summary>
        /// necessary for SAM output
        /// </summary>
        public static double SAMTolerance { get => _sAMTolerance; private set { _sAMTolerance = value; } }
        private static double _toleranceAngleRad = 5 * (System.Math.PI / 180);
        /// <summary>
        /// 5 degrees
        /// </summary>
        public static double ToleranceAngleRad { get => _toleranceAngleRad; private set { _toleranceAngleRad = value; } }
        private static double _arcToleranceAngleRad = 0.3 * (System.Math.PI / 180);
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
        public List<List<Face3D>> SnappedWalls { get; private set; } = new List<List<Face3D>>();
        /// <summary>
        /// output
        /// </summary>
        public List<List<Point3D>> NakedEnds { get; private set; } = new List<List<Point3D>>();
        /// <summary>
        /// output
        /// </summary>
        public List<List<int>> SnappedSources { get; private set; } = new List<List<int>>();

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
            List<Core.Range<double>> LevelsInput, double LevelSectionOffsetInput, double NakedNodeSnapDistanceInput, double MinWallSegmentLengthInput,
            double ToleranceDistanceInput, double ToleranceAngleRadInput, double ArcToleranceAngleRadInput)
        {
            ModelTolerance = 0.001;
            SAMTolerance = ToleranceDistanceInput >= _minTolerance ? ToleranceDistanceInput : _minTolerance;
            ToleranceAngleRad = ToleranceAngleRadInput;
            ArcToleranceAngleRad = ArcToleranceAngleRadInput;
            MinWallSegmentLength = MinWallSegmentLengthInput;

            PanelsFaces3D = PanelsBrepInput;
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
            BucketSizes = AdjustListLength(BucketSizes, PanelsFaces3D.Count, defaultValue: 0.3);
            Weights = AdjustListLength(Weights, PanelsFaces3D.Count, defaultValue: 1.0);
            MaxExtensions = AdjustListLength(MaxExtensions, PanelsFaces3D.Count, defaultValue: 0.5);

            List<SnappedWall> walls = RegisterWalls(PanelsFaces3D, BucketSizes, Weights, MaxExtensions, Levels, LevelSectionOffset, _extensionLimiter);
            List<SnappedWall> snapped = SnapAndAdjustWalls(walls);
            TrimAndExtendWalls(snapped);
            snapped = ExplodeWallsAtIntersections(snapped);
            SnapOpenNodes(snapped, NakedNodeSnapDistance);
            snapped = CreateGraph(snapped, MinWallSegmentLength); // graph processing
            snapped = MergeColinearWalls(snapped);
            MarkNakedNodes(snapped);

            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = SortWallsByElevation(snapped);
            //GH_Path levelPath = new GH_Path(0);
            for (int i = 0; i < snappedWallsPerFloor.Keys.Count; i++)
            {
                NakedEnds.Add(new List<Point3D>());
                List<SnappedWall> currentFloor = snappedWallsPerFloor[snappedWallsPerFloor.Keys[i]];
                for (int j = 0; j < currentFloor.Count; j++)
                {
                    List<List<int>> source = new List<List<int>>();
                    List<Face3D> wallSegments = currentFloor[j].GetFaces3D(out source);
                    SnappedWalls.Add(wallSegments);
                    SnappedSources.AddRange(source);

                    if (currentFloor[j].NakedStart)
                        NakedEnds[i].Add(ProjectionPlane.Convert(currentFloor[j].ProjectedAxis.Start));
                    if (currentFloor[j].NakedEnd)
                        NakedEnds[i].Add(ProjectionPlane.Convert(currentFloor[j].ProjectedAxis.End));
                }
            }
        }
        private static SortedList<double, List<SnappedWall>> SortWallsByElevation(List<SnappedWall> walls)
        {
            SortedList<double, List<SnappedWall>> snappedWallsPerFloor = new SortedList<double, List<SnappedWall>>();
            for (int i = 0; i < walls.Count; i++)
            {
                double currentLevel = System.Math.Round(walls[i].Elevation, SnapSolver.ElevationToleranceDigits);
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
                    Segment3D axis = ProjectionPlane.Convert(currentAxis);
                    axis.GetMoved(new Vector3D(0, 0, elevation));
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
                            if (thisLevelWalls[j].SourceIndices.All(id => masterSources.Contains(id)))
                                colinearIndices.Add(j);
                        }
                    }

                    // recursively check which colinear segments touch the master wall and update the masterAxis
                    List<Point2D> anchors = new List<Point2D>();
                    anchors.Add(masterAxis.Start);
                    anchors.Add(masterAxis.End);
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
                            }
                        }
                        safetyCounter++;
                    } while (anyMatch && safetyCounter < 100);

                    double elevation = floor.Key;

                    List<double> parameters = anchors.Select(a => masterAxis.ClosestParameter(a)).ToList();
                    double minParam = parameters.Min();
                    double maxParam = parameters.Max();

                    masterAxis = new Segment2D(anchors[parameters.IndexOf(minParam)], anchors[parameters.IndexOf(maxParam)]);
                    var masterAxis3D = ProjectionPlane.Convert(masterAxis);
                    masterAxis3D.GetMoved(new Vector3D(0, 0, elevation));                    

                    SnappedWall mergedWall = new SnappedWall(masterWall.SourceIndices[0], masterAxis3D, 
                        masterWall.Weight, masterWall.BucketSize, masterWall.MaxExtension, masterWall.OriginalHeight);
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
        private static double OrthoDistance2d(Point2D ptA, Point2D ptB)
        {
            double distance = System.Math.Abs(ptA.X - ptB.X);
            distance += System.Math.Abs(ptA.Y - ptB.Y);
            return distance;
        }

        private static List<SnappedWall> ExplodeWallsAtIntersections(List<SnappedWall> walls)
        {
            List<SnappedWall> split = new List<SnappedWall>();

            for (int i = 0; i < walls.Count; i++)
            {
                // find all intersections between this wall and walls on the same level
                List<Point2D> intersections = new List<Point2D>();
                for (int j = 0; j < walls.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    if (!Core.Query.AlmostEqual(walls[i].Elevation, walls[j].Elevation, SnapSolver.ModelTolerance))
                    {// are not on the same level, don't intersect
                     //continue;
                    }
                    double myParam = 0;
                    double theirParam = 0;
                    Segment2D currentExtended = walls[i].ProjectedAxis;
                    Segment2D otherExtended = walls[j].ProjectedAxis;
                    currentExtended.Extend(SnapSolver.ModelTolerance, true, true);
                    otherExtended.Extend(SnapSolver.ModelTolerance, true, true);
                    //if (Rhino.Geometry.Intersect.Intersection.LineLine(currentExtended, otherExtended, 
                    //    out myParam, out theirParam, SAM_SnapSolver.SAMTolerance, finiteSegments: true))
                    if(currentExtended.Intersect(otherExtended, SnapSolver.SAMTolerance))
                    {
                        //StartExtensionParam = System.Math.Max(StartExtensionParam, myParam);
                        intersections.Add(currentExtended.Intersection(otherExtended, true, SnapSolver.SAMTolerance));
                    }
                }
                //Print("Intersections:{0}", walls[i].Elevation);
                //foreach (Point3d point3D in intersections) {
                //    Print(point3D.EndString());
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
                List<Point2D> anchorCandidates = new List<Point2D>();
                for (int j = 0; j < wallsByLength.Count; j++)
                {
                    if (!Core.Query.AlmostEqual(currentWall.Elevation, wallsByLength[j].Elevation, SnapSolver.ModelTolerance)) // same level only
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

            } while (anySnapped == true);

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
                ExtensionSolver solver = new ExtensionSolver(axes, extensions, SnapSolver.SAMTolerance);
                var newAxes = solver.Solve();
                for (int i = 0; i < floor.Value.Count; i++)
                {
                    floor.Value[i].UpdateWithTrim(newAxes[i]);
                }
            }

            MarkNakedNodes(walls);
        }
        private static List<SnappedWall> RegisterWalls(List<Face3D> panelsBrep, List<double> bucketSizes, 
            List<double> weights, List<double> maxExtensions, List<Core.Range<double>> levels, double levelOffset, double extensionLimiter)
        {
            var walls = new List<SnappedWall>();

            for (int j = 0; j < levels.Count; j++)
            {
                double sectionHeight = levels[j].Min + levelOffset;
                Core.Range<double> currentHeight = levels[j];
                Plane currentPlane = Plane.WorldXY;
                currentPlane.GetMoved(new Vector3D(0, 0, sectionHeight));

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
                            SnappedWall wall = new SnappedWall(i, section3D, weights[i], bucketSizes[i], 
                                System.Math.Min(maxExtensions[i], section3D.GetLength() * extensionLimiter), currentHeight);
                            walls.Add(wall);
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
