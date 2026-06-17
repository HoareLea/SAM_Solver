using Grasshopper.Kernel;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using SAM.Core;
using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;
using SAM.Analytical.Solver;
using SAM.Geometry.Grasshopper;
using SAM.Geometry.Spatial;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    /// <summary>
    /// Variant of the SAM Solver that runs on wall axis curves, polylines and SAM geometry
    /// (Segment3D, Face3D, ...) instead of panel surfaces. Each input is reduced to a set of wall
    /// centreline segments (faces contribute their external edges), extruded vertically into a
    /// panel, snapped/cleaned with the same <see cref="Solver{T}"/> engine and returned with the
    /// same panels / outlines / shells / nakedEnds outputs as the original Solver component.
    /// </summary>
    public class SolverCurves : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("12102e43-312a-4f03-ac66-70e070754028");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SolverCurves()
            : base("SolverCurves", "SolverCurves", "SAM Solver running on wall axis curves, polylines and SAM geometry instead of panels", "SAM", "Solver")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Curve() { Name = "_curves", NickName = "_curves", Description = "Wall axes represented as a list of curves or polylines.\nEach segment is treated as a wall centreline.", Access = GH_ParamAccess.list, Optional = true, DataMapping = GH_DataMapping.Flatten }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "geometry_", NickName = "geometry_", Description = "Wall axes represented as SAM geometry (Segment3D, Face3D, Polyline3D, ...).\nExternal edges are extracted from faces.", Access = GH_ParamAccess.list, Optional = true, DataMapping = GH_DataMapping.Flatten }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Number paramNumber_Height = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_wallHeight_", NickName = "_wallHeight_", Description = "Height used to extrude each axis into a vertical wall panel before snapping.\ndefault: 3.0", Access = GH_ParamAccess.item };
                paramNumber_Height.SetPersistentData(3.0);
                result.Add(new GH_SAMParam(paramNumber_Height, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Bucket size per input axis.\ndefault: 0.12", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weights for the axes\ndefault: between 0.2 - 1 and air: 0", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "maxExtensions_", NickName = "maxExtensions_", Description = "Maximum extensions in a snapping process\ndefault: depends on wall thickness", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Interval() { Name = "levels_", NickName = "levels_", Description = "Information on each floor's elevation", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Number paramNumber;

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_levelOffset_", NickName = "_levelOffset_", Description = "Level Section Offset", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketBetweenLevels_", NickName = "bucketBetweenLevels_", Description = "A global parameter that aligns walls between levels within a defined bucket distance, correcting offsets to ensure consistency.", Access = GH_ParamAccess.item, Optional = true };
                paramNumber.SetPersistentData(0.21);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_nakedNodeSnapDistance_", NickName = "_nakedNodeSnapDistance_", Description = "Snap distance for a naked node", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_minWallSegmentLength_", NickName = "_minWallSegmentLength_", Description = "The smallest wall segment that won't be merged into an other wall", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceDistance_", NickName = "_toleranceDistance_", Description = "Distance tolerance \ndefault 1E-06", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Distance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceAngle_", NickName = "_toleranceAngle_", Description = "Distance angle in radians \nUse it to align walls between levels\ndefault 0.034907 RAD = 2 deg", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Angle);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceArc_", NickName = "_toleranceArc_", Description = "Arc angle tolerance in radians \ndefault 0.005236 RAD = 0.3 deg", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                return result.ToArray();
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels", NickName = "panels", Description = "SAM Analytical Panels", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "outlines", NickName = "outlines iSAM Geometry", Description = "iSAM Geometry Segment3D\nTop-level section outlines on xy plane of walls at each level \n Input for CreateAdjacencyCluster", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "shells", NickName = "shells", Description = "SAM Geometry Shells", Access = GH_ParamAccess.tree }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Point() { Name = "nakedEnds", NickName = "nakedEnds", Description = "Naked Points", Access = GH_ParamAccess.list }, ParamVisibility.Voluntary));

                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<Curve> curves = new List<Curve>();
            List<ISAMGeometry3D> geometries = new List<ISAMGeometry3D>();
            List<double> bucketSizes = new List<double>();
            List<double> weights = new List<double>();
            List<double> maxExtensions = new List<double>();
            List<Interval> levels = new List<Interval>();

            double wallHeight = 3.0;
            double levelSectionOffset = Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset;
            double nakedNodeSnapDistance = Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance;
            double minWallSegmentLength = Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength;
            double toleranceDistance = Tolerance.Distance;
            double toleranceAngleRad = Tolerance.Angle;
            double arcToleranceAngleRad = Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad;

            int index = -1;

            bool hasCurves = false;
            index = Params.IndexOfInputParam("_curves");
            if (index != -1)
            {
                hasCurves = dataAccess.GetDataList(index, curves);
            }

            bool hasGeometry = false;
            index = Params.IndexOfInputParam("geometry_");
            if (index != -1)
            {
                hasGeometry = dataAccess.GetDataList(index, geometries);
            }

            if (!hasCurves && !hasGeometry)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide wall axes through _curves and/or geometry_.");
                return;
            }

            index = Params.IndexOfInputParam("_wallHeight_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref wallHeight);
            }

            if (double.IsNaN(wallHeight) || wallHeight <= 0)
            {
                wallHeight = 3.0;
            }

            index = Params.IndexOfInputParam("bucketSizes_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, bucketSizes);
            }

            index = Params.IndexOfInputParam("weights_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, weights);
            }

            index = Params.IndexOfInputParam("maxExtensions_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, maxExtensions);
            }

            index = Params.IndexOfInputParam("levels_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, levels);
            }

            index = Params.IndexOfInputParam("_levelOffset_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref levelSectionOffset);
            }

            double bucketBetweenLevels = 0.21;
            index = Params.IndexOfInputParam("bucketBetweenLevels_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref bucketBetweenLevels);
            }

            index = Params.IndexOfInputParam("_nakedNodeSnapDistance_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref nakedNodeSnapDistance);
            }

            index = Params.IndexOfInputParam("_minWallSegmentLength_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref minWallSegmentLength);
            }

            index = Params.IndexOfInputParam("_toleranceDistance_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref toleranceDistance);
            }

            index = Params.IndexOfInputParam("_toleranceAngle_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref toleranceAngleRad);
            }

            index = Params.IndexOfInputParam("_toleranceArc_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref arcToleranceAngleRad);
            }

            // Convert each input axis (curve, polyline or SAM geometry) into vertical wall panels.
            // Each axis becomes a wall centreline that is extruded vertically by wallHeight so that
            // the existing panel based Solver engine can snap and clean the wall network. The bucket
            // size / weight / max extension lists are applied per input axis.
            List<Panel> panels = new List<Panel>();
            int sourceIndex = 0;

            foreach (Curve curve in curves)
            {
                AddPanels(panels, ToSegment3Ds(curve, toleranceDistance), wallHeight, sourceIndex, bucketSizes, weights, maxExtensions, toleranceDistance);
                sourceIndex++;
            }

            foreach (ISAMGeometry3D geometry in geometries)
            {
                AddPanels(panels, ToSegment3Ds(geometry, toleranceDistance), wallHeight, sourceIndex, bucketSizes, weights, maxExtensions, toleranceDistance);
                sourceIndex++;
            }

            if (panels.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least two wall axis segments are required to run the solver.");
                return;
            }

            List<Range<double>> ranges = new List<Range<double>>();

            if (levels != null)
            {
                foreach (Interval level in levels)
                    ranges.Add(new Range<double>(level.Min, level.Max));
            }

            if (ranges == null || ranges.Count == 0)
            {
                ranges = null;
            }

            Solver<Panel> solver = new Solver<Panel>(panels, ranges)
            {
                Tolerance_Angle = toleranceAngleRad,
                Tolerance_Distance = toleranceDistance,
                Tolerance_Arc = arcToleranceAngleRad,
                MinLength = minWallSegmentLength,
                SnapDistance = nakedNodeSnapDistance,
                Offset = levelSectionOffset
            };

            panels = solver.Execute(out List<Point3D> nakedPoint3Ds, bucketBetweenLevels);

            if (panels != null)
            {
                //Make sure each panel has unique Guid!
                for (int i = 0; i < panels.Count; i++)
                {
                    Guid guid = panels[i].Guid;

                    List<Panel> panels_Guid = panels.FindAll(x => x.Guid == guid);
                    while (panels_Guid != null && panels_Guid.Count > 1)
                    {
                        guid = Guid.NewGuid();
                        panels[i] = Create.Panel(guid, panels[i]);
                        panels_Guid = panels.FindAll(x => x.Guid == guid);
                    }
                }
            }

            index = Params.IndexOfOutputParam("panels");
            if (index != -1 && panels != null)
            {
                if (ranges == null)
                {
                    ranges = Geometry.Object.Spatial.Query.ElevationRanges(panels);
                }

                Dictionary<int, List<Panel>> dictionary = new Dictionary<int, List<Panel>>();
                foreach (Panel panel in panels)
                {
                    int count = -1;

                    Geometry.Spatial.Point3D centroid = panel?.GetBoundingBox()?.GetCentroid();
                    if (centroid != null)
                    {
                        count = ranges.FindIndex(x => x.In(centroid.Z));
                    }

                    if (count == -1)
                    {
                        count = ranges.Count;
                    }

                    if (!dictionary.TryGetValue(count, out List<Panel> panels_Temp) || panels_Temp == null)
                    {
                        panels_Temp = new List<Panel>();
                        dictionary[count] = panels_Temp;
                    }

                    panels_Temp.Add(panel);
                }

                int index_Shells = Params.IndexOfOutputParam("shells");
                int index_Outlines = Params.IndexOfOutputParam("outlines");

                DataTree<GooSAMGeometry> dataTree_Shell = index_Shells == -1 ? null : new DataTree<GooSAMGeometry>();
                DataTree<GooSAMGeometry> dataTree_Outlines = index_Outlines == -1 ? null : new DataTree<GooSAMGeometry>();
                DataTree<GooPanel> dataTree_Panel = new DataTree<GooPanel>();
                foreach (KeyValuePair<int, List<Panel>> keyValuePair in dictionary)
                {
                    GH_Path path = new GH_Path(keyValuePair.Key);
                    List<Panel> panels_Temp = keyValuePair.Value;

                    foreach (Panel panel in panels_Temp)
                    {
                        dataTree_Panel.Add(new GooPanel(panel), path);
                    }

                    if (dataTree_Shell is not null)
                    {
                        List<Shell> shells = getShells(panels_Temp, toleranceDistance);
                        if (shells != null)
                        {
                            foreach (Shell shell in shells)
                            {
                                dataTree_Shell.Add(new GooSAMGeometry(shell), path);
                            }
                        }
                    }

                    if (dataTree_Outlines is not null && keyValuePair.Key < ranges.Count)
                    {
                        double elevation = ranges[keyValuePair.Key].Max;

                        Dictionary<Panel, List<ISAMGeometry3D>> dictionary_Section = Analytical.Query.SectionDictionary<ISAMGeometry3D>(panels_Temp, Geometry.Spatial.Plane.WorldXY.GetMoved(new Vector3D(0, 0, elevation)) as Geometry.Spatial.Plane, toleranceDistance);

                        List<ISAMGeometry3D> sAMGeometry3Ds = new List<ISAMGeometry3D>();
                        foreach (List<ISAMGeometry3D> sAMGeometry3Ds_Temp in dictionary_Section.Values)
                        {
                            sAMGeometry3Ds_Temp.ForEach(x => dataTree_Outlines.Add(new GooSAMGeometry(x), path));
                        }
                    }
                }

                dataAccess.SetDataTree(index, dataTree_Panel);

                if (index_Shells != -1)
                {
                    dataAccess.SetDataTree(index_Shells, dataTree_Shell);
                }

                if (index_Outlines != -1)
                {
                    dataAccess.SetDataTree(index_Outlines, dataTree_Outlines);
                }
            }

            index = Params.IndexOfOutputParam("nakedEnds");
            if (index != -1)
            {
                dataAccess.SetDataList(index, nakedPoint3Ds?.ConvertAll(x => Geometry.Grasshopper.Convert.ToGrasshopper(x)));
            }
        }

        /// <summary>
        /// Builds vertical wall panels from a set of axis segments and assigns the per-axis solver
        /// properties, appending them to <paramref name="panels"/>.
        /// </summary>
        private static void AddPanels(List<Panel> panels, List<Segment3D> segment3Ds, double wallHeight, int sourceIndex, List<double> bucketSizes, List<double> weights, List<double> maxExtensions, double tolerance)
        {
            if (panels == null || segment3Ds == null || segment3Ds.Count == 0)
            {
                return;
            }

            double bucketSize = GetValue(bucketSizes, sourceIndex);
            double weight = GetValue(weights, sourceIndex);
            double maxExtension = GetValue(maxExtensions, sourceIndex);

            foreach (Segment3D segment3D in segment3Ds)
            {
                if (segment3D == null || segment3D.GetLength() <= tolerance)
                {
                    continue;
                }

                Face3D face3D = Geometry.Spatial.Create.Face3D(segment3D, wallHeight);
                if (face3D == null)
                {
                    continue;
                }

                Vector3D normal = face3D.GetPlane()?.Normal;
                if (normal == null)
                {
                    continue;
                }

                PanelType panelType = Analytical.Query.PanelType(normal);
                Construction construction = Analytical.Query.DefaultConstruction(panelType);

                Panel panel = Analytical.Create.Panel(construction, panelType, face3D);
                if (panel == null)
                {
                    continue;
                }

                if (!double.IsNaN(bucketSize))
                {
                    panel.SetValue(SolverParameter.BucketSize, bucketSize);
                }

                if (!double.IsNaN(weight))
                {
                    panel.SetValue(SolverParameter.Weight, weight);
                }

                if (!double.IsNaN(maxExtension))
                {
                    panel.SetValue(SolverParameter.MaxExtend, maxExtension);
                }

                panels.Add(panel);
            }
        }

        /// <summary>
        /// Converts a Rhino curve (line, polyline or general curve) into a list of SAM
        /// <see cref="Segment3D"/> wall axes. General curves are approximated by a polyline.
        /// </summary>
        private static List<Segment3D> ToSegment3Ds(Curve curve, double tolerance)
        {
            if (curve == null)
            {
                return null;
            }

            Polyline polyline = null;
            if (!curve.TryGetPolyline(out polyline) || polyline == null)
            {
                PolylineCurve polylineCurve = curve.ToPolyline(0, 0, 0.5, 0, 0, tolerance, 0, 0, true);
                polylineCurve?.TryGetPolyline(out polyline);
            }

            if (polyline == null || polyline.Count < 2)
            {
                return null;
            }

            List<Segment3D> result = new List<Segment3D>();
            foreach (Line line in polyline.GetSegments())
            {
                Geometry.Spatial.Point3D start = new Geometry.Spatial.Point3D(line.From.X, line.From.Y, line.From.Z);
                Geometry.Spatial.Point3D end = new Geometry.Spatial.Point3D(line.To.X, line.To.Y, line.To.Z);

                Segment3D segment3D = new Segment3D(start, end);
                if (segment3D.GetLength() > tolerance)
                {
                    result.Add(segment3D);
                }
            }

            return result;
        }

        /// <summary>
        /// Reduces SAM geometry to a list of wall axis <see cref="Segment3D"/>. Segments are used
        /// directly, faces contribute their external edges and any other segmentable geometry is
        /// exploded into its segments.
        /// </summary>
        private static List<Segment3D> ToSegment3Ds(ISAMGeometry3D geometry, double tolerance)
        {
            if (geometry == null)
            {
                return null;
            }

            List<Segment3D> result = new List<Segment3D>();

            switch (geometry)
            {
                case Segment3D segment3D:
                    result.Add(segment3D);
                    break;

                case Face3D face3D:
                    ISegmentable3D externalEdge = face3D.GetExternalEdge3D() as ISegmentable3D;
                    if (externalEdge != null)
                    {
                        result.AddRange(externalEdge.GetSegments());
                    }
                    break;

                case ISegmentable3D segmentable3D:
                    result.AddRange(segmentable3D.GetSegments());
                    break;
            }

            result.RemoveAll(x => x == null || x.GetLength() <= tolerance);

            return result;
        }

        private static double GetValue(List<double> values, int index)
        {
            if (values == null || values.Count == 0)
            {
                return double.NaN;
            }

            return values.Count <= index ? values.Last() : values[index];
        }

        private List<Shell> getShells(IEnumerable<Panel> panels, double tolerance)
        {
            if (panels == null)
            {
                return null;
            }

            BoundingBox3D boundingBox3D = panels.BoundingBox3D();
            if (boundingBox3D == null)
            {
                return null;
            }

            double elevation_Min = boundingBox3D.Min.Z;
            double elevation_Max = boundingBox3D.Max.Z;

            Geometry.Spatial.Point3D point3D = boundingBox3D.GetCentroid();

            Geometry.Spatial.Plane plane_Min = new Geometry.Spatial.Plane(new Geometry.Spatial.Point3D(point3D.X, point3D.Y, elevation_Min), Vector3D.WorldZ.GetNegated());
            Geometry.Spatial.Plane plane = new Geometry.Spatial.Plane(point3D, Vector3D.WorldZ);
            Geometry.Spatial.Plane plane_Max = new Geometry.Spatial.Plane(new Geometry.Spatial.Point3D(point3D.X, point3D.Y, elevation_Max), Vector3D.WorldZ);


            Dictionary<Panel, List<ISegmentable2D>> dictionary = Analytical.Query.SectionDictionary<ISegmentable2D>(panels, plane);
            if (dictionary == null)
            {
                return null;
            }

            List<Tuple<Panel, Face3D, BoundingBox3D, List<Geometry.Spatial.Point3D>>> tuples = new List<Tuple<Panel, Face3D, BoundingBox3D, List<Geometry.Spatial.Point3D>>>();

            List<Segment2D> segment2Ds = new List<Segment2D>();
            foreach (KeyValuePair<Panel, List<ISegmentable2D>> keyValuePair in dictionary)
            {
                foreach (ISegmentable2D segmentable2D in keyValuePair.Value)
                {
                    segment2Ds.AddRange(segmentable2D.GetSegments());
                }

                Face3D face3D = keyValuePair.Key.Face3D;

                BoundingBox3D boundingBox3D_Face3D = face3D.GetBoundingBox();

                List<Geometry.Spatial.Point3D> point3Ds = (face3D.GetExternalEdge3D() as ISegmentable3D)?.GetPoints();

                tuples.Add(new Tuple<Panel, Face3D, BoundingBox3D, List<Geometry.Spatial.Point3D>>(keyValuePair.Key, face3D, boundingBox3D_Face3D, point3Ds));
            }

            List<Face2D> face2Ds = segment2Ds.Face2Ds();
            face2Ds.Holes()?.ForEach(x => face2Ds.Add(new Face2D(x)));

            Func<Geometry.Spatial.Point3D, Geometry.Spatial.Point3D> findExistingPoint3D = new Func<Geometry.Spatial.Point3D, Geometry.Spatial.Point3D>((Geometry.Spatial.Point3D x) =>
            {
                foreach (Tuple<Panel, Face3D, BoundingBox3D, List<Geometry.Spatial.Point3D>> tuple in tuples)
                {
                    foreach (Geometry.Spatial.Point3D point3D_Temp in tuple.Item4)
                    {
                        if (x.Distance(point3D_Temp) < Tolerance.MacroDistance)
                        {
                            return point3D_Temp;
                        }
                    }
                }
                return x;
            });

            Func<IClosedPlanar3D, Geometry.Spatial.Plane, Polygon2D> createPolygon2D = new Func<IClosedPlanar3D, Geometry.Spatial.Plane, Polygon2D>((IClosedPlanar3D x, Geometry.Spatial.Plane y) =>
            {
                ISegmentable3D segmentable3D = x as ISegmentable3D;
                if (segmentable3D == null)
                {
                    return null;
                }

                List<Geometry.Spatial.Point3D> point3Ds = new List<Geometry.Spatial.Point3D>();
                foreach (Geometry.Spatial.Point3D point3D_Segmentable3D in segmentable3D.GetPoints())
                {
                    Geometry.Spatial.Point3D point3D_Temp = y.Project(point3D_Segmentable3D);
                    point3D_Temp = findExistingPoint3D.Invoke(point3D_Temp);
                    if (point3D_Temp == null)
                    {
                        continue;
                    }

                    Geometry.Spatial.Point3D point3D_Project = y.Project(point3D_Temp);
                    if (point3D_Project.Distance(point3D_Temp) > tolerance)
                    {
                        point3D_Temp = point3D_Temp.Mid(point3D_Project);
                    }

                    point3Ds.Add(point3D_Temp);

                }

                if (point3Ds.Count < 3)
                {
                    return null;
                }

                return new Polygon2D(point3Ds.ConvertAll(a => y.Convert(a)));
            });

            List<Shell> result = new List<Shell>();
            foreach (Face2D face2D in face2Ds)
            {
                List<Segment2D> segment2Ds_Face2D = (face2D?.ExternalEdge2D as ISegmentable2D)?.GetSegments();
                if (segment2Ds_Face2D == null)
                {
                    continue;
                }

                face2D.InternalEdge2Ds?.FindAll(x => x is ISegmentable2D).ForEach(x => segment2Ds_Face2D.AddRange(((ISegmentable2D)x).GetSegments()));

                if (segment2Ds_Face2D == null || segment2Ds_Face2D.Count == 0)
                {
                    continue;
                }

                List<Face3D> face3Ds = new List<Face3D>();
                foreach (Segment2D segment2D in segment2Ds_Face2D)
                {
                    Geometry.Spatial.Point3D point3D_Segment2D = plane.Convert(segment2D.Mid());

                    Tuple<Panel, Face3D, BoundingBox3D, List<Geometry.Spatial.Point3D>> tuple = tuples.Find(x => x.Item3.InRange(point3D_Segment2D) && x.Item2.On(point3D_Segment2D));
                    face3Ds.Add(tuple.Item2);
                }

                Polygon2D externalEdge;
                List<Polygon2D> internalEdges;
                Face3D face3D_Temp = null;

                Face3D face3D = plane.Convert(face2D);

                externalEdge = createPolygon2D(face3D.GetExternalEdge3D(), plane_Min);
                internalEdges = face3D.GetInternalEdge3Ds()?.ConvertAll(x => createPolygon2D(x, plane_Min));

                face3D_Temp = Geometry.Spatial.Create.Face3Ds(externalEdge, internalEdges, plane_Min)?.FirstOrDefault();
                if (face3D_Temp != null)
                {
                    face3Ds.Add(face3D_Temp);
                }

                externalEdge = createPolygon2D(face3D.GetExternalEdge3D(), plane_Max);
                internalEdges = face3D.GetInternalEdge3Ds()?.ConvertAll(x => createPolygon2D(x, plane_Max));

                face3D_Temp = Geometry.Spatial.Create.Face3Ds(externalEdge, internalEdges, plane_Max)?.FirstOrDefault();
                if (face3D_Temp != null)
                {
                    face3Ds.Add(face3D_Temp);
                }

                result.Add(new Shell(face3Ds));
            }

            return result;
        }
    }
}
