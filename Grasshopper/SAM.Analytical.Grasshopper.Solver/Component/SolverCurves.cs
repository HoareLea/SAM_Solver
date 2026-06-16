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

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    /// <summary>
    /// Variant of the SAM Solver that runs on wall axis curves and polylines instead of panel
    /// surfaces. Each input curve is treated as a wall centreline, extruded vertically into a
    /// panel, snapped/cleaned with the same <see cref="Solver{T}"/> engine and returned as the
    /// resulting wall outlines (curves) per level.
    /// </summary>
    public class SolverCurves : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("12102e43-312a-4f03-ac66-70e070754028");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SolverCurves()
            : base("SolverCurves", "SolverCurves", "SAM Solver running on wall axis curves and polylines instead of panels", "SAM", "Solver")
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Curve() { Name = "_curves", NickName = "_curves", Description = "Wall axes represented as a list of curves or polylines.\nEach segment is treated as a wall centreline.", Access = GH_ParamAccess.list, DataMapping = GH_DataMapping.Flatten }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Number paramNumber_Height = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_wallHeight_", NickName = "_wallHeight_", Description = "Height used to extrude each axis curve into a vertical wall panel before snapping.\ndefault: 3.0", Access = GH_ParamAccess.item };
                paramNumber_Height.SetPersistentData(3.0);
                result.Add(new GH_SAMParam(paramNumber_Height, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Bucket size per curve.\ndefault: 0.12", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weights for the curves\ndefault: between 0.2 - 1 and air: 0", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
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
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "curves", NickName = "curves", Description = "Snapped wall axis curves / polylines on the xy plane at each level", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels", NickName = "panels", Description = "Snapped wall panels extruded from the input axes", Access = GH_ParamAccess.tree }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Point() { Name = "nakedEnds", NickName = "nakedEnds", Description = "Naked Points", Access = GH_ParamAccess.list }, ParamVisibility.Voluntary));

                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<Curve> curves = new List<Curve>();
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

            index = Params.IndexOfInputParam("_curves");
            if (index == -1 || !dataAccess.GetDataList(index, curves))
            {
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

            // Convert each input curve into a vertical wall panel. Each curve segment becomes a
            // wall centreline that is extruded vertically by wallHeight so that the existing
            // panel based Solver engine can snap and clean the wall network.
            List<Panel> panels = new List<Panel>();
            for (int i = 0; i < curves.Count; i++)
            {
                List<Segment3D> segment3Ds = ToSegment3Ds(curves[i], toleranceDistance);
                if (segment3Ds == null || segment3Ds.Count == 0)
                {
                    continue;
                }

                double bucketSize = GetValue(bucketSizes, i);
                double weight = GetValue(weights, i);
                double maxExtension = GetValue(maxExtensions, i);

                foreach (Segment3D segment3D in segment3Ds)
                {
                    if (segment3D == null || segment3D.GetLength() <= toleranceDistance)
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

            if (panels == null)
            {
                return;
            }

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

            if (ranges == null)
            {
                ranges = Geometry.Object.Spatial.Query.ElevationRanges(panels);
            }

            // Group the resulting panels per level so the snapped axes can be sectioned and
            // returned level by level, mirroring the panel based Solver component.
            Dictionary<int, List<Panel>> dictionary = new Dictionary<int, List<Panel>>();
            foreach (Panel panel in panels)
            {
                int count = -1;

                Point3D centroid = panel?.GetBoundingBox()?.GetCentroid();
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

            int index_Curves = Params.IndexOfOutputParam("curves");
            int index_Panels = Params.IndexOfOutputParam("panels");

            DataTree<GooSAMGeometry> dataTree_Curves = index_Curves == -1 ? null : new DataTree<GooSAMGeometry>();
            DataTree<GooPanel> dataTree_Panel = index_Panels == -1 ? null : new DataTree<GooPanel>();

            foreach (KeyValuePair<int, List<Panel>> keyValuePair in dictionary)
            {
                GH_Path path = new GH_Path(keyValuePair.Key);
                List<Panel> panels_Temp = keyValuePair.Value;

                if (dataTree_Panel is not null)
                {
                    foreach (Panel panel in panels_Temp)
                    {
                        dataTree_Panel.Add(new GooPanel(panel), path);
                    }
                }

                if (dataTree_Curves is not null && keyValuePair.Key < ranges.Count)
                {
                    double elevation = ranges[keyValuePair.Key].Max;

                    Dictionary<Panel, List<ISAMGeometry3D>> dictionary_Section = Analytical.Query.SectionDictionary<ISAMGeometry3D>(panels_Temp, Geometry.Spatial.Plane.WorldXY.GetMoved(new Vector3D(0, 0, elevation)) as Geometry.Spatial.Plane, toleranceDistance);
                    if (dictionary_Section != null)
                    {
                        foreach (List<ISAMGeometry3D> sAMGeometry3Ds_Temp in dictionary_Section.Values)
                        {
                            sAMGeometry3Ds_Temp?.ForEach(x => dataTree_Curves.Add(new GooSAMGeometry(x), path));
                        }
                    }
                }
            }

            if (index_Curves != -1)
            {
                dataAccess.SetDataTree(index_Curves, dataTree_Curves);
            }

            if (index_Panels != -1)
            {
                dataAccess.SetDataTree(index_Panels, dataTree_Panel);
            }

            index = Params.IndexOfOutputParam("nakedEnds");
            if (index != -1)
            {
                dataAccess.SetDataList(index, nakedPoint3Ds?.ConvertAll(x => Geometry.Grasshopper.Convert.ToGrasshopper(x)));
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
                Point3D start = new Point3D(line.From.X, line.From.Y, line.From.Z);
                Point3D end = new Point3D(line.To.X, line.To.Y, line.To.Z);

                Segment3D segment3D = new Segment3D(start, end);
                if (segment3D.GetLength() > tolerance)
                {
                    result.Add(segment3D);
                }
            }

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
    }
}
