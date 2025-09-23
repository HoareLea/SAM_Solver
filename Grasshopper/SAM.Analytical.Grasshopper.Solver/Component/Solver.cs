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
    public class Solver : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("2e2be06e-55f1-4c94-92b3-71df0808eaf8");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.4";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public Solver()
            : base("Solver", "Solver", "SAM Solver", "SAM", "Solver")
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
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "_panels", NickName = "_panels", Description = "Panels represented as a list of surface brep geometry.", Access = GH_ParamAccess.list, DataMapping = GH_DataMapping.Flatten}, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Bucket size per panel.\ndefault: 0.12", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weighs or the panels\ndefault: between 0.2 - 1 nad air: 0", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "maxExtensions_", NickName = "maxExtensions_", Description = "Maximum extensions in a snapping process\ndefault: depends on panel thickness", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
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

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceAngle_", NickName = "_toleranceAngle_", Description = "Distance angle in radians \nUse it to align panels between levels\ndefault 0.034907 RAD = 2 deg", Access = GH_ParamAccess.item };
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

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<Panel> panels = new List<Panel>();
            List<double> bucketSizes = new List<double>();
            List<double> weights = new List<double>();
            List<double> maxExtensions = new List<double>();
            List<Interval> levels = new List<Interval>();

            double levelSectionOffset = Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset;
            double nakedNodeSnapDistance = Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance;
            double minWallSegmentLength = Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength;
            double toleranceDistance = Tolerance.Distance;
            double toleranceAngleRad = Tolerance.Angle;
            double arcToleranceAngleRad = Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad;

            int index = -1;

            index = Params.IndexOfInputParam("_panels");
            if (index == -1 || !dataAccess.GetDataList(index, panels))
            {
                return;
            }

            index = Params.IndexOfInputParam("bucketSizes_");
            if(index != -1)
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

            for(int i = 0; i < panels.Count; i++)
            {
                Panel panel_Temp = Create.Panel(panels[i]);
                if(panel_Temp == null)
                {
                    continue;
                }

                if(bucketSizes != null && bucketSizes.Count != 0)
                {
                    double bucketSize = bucketSizes.Count <= i ? bucketSizes.Last() : bucketSizes[i];
                    if(!double.IsNaN(bucketSize))
                    {
                        panel_Temp.SetValue(SolverParameter.BucketSize, bucketSize);
                    }
                }

                if (weights != null && weights.Count != 0)
                {
                    double weight = weights.Count <= i ? weights.Last() : weights[i];
                    if (!double.IsNaN(weight))
                    {
                        panel_Temp.SetValue(SolverParameter.Weight, weight);
                    }
                }

                if (maxExtensions != null && maxExtensions.Count != 0)
                {
                    double maxExtension = maxExtensions.Count <= i ? maxExtensions.Last() : maxExtensions[i];
                    if (!double.IsNaN(maxExtension))
                    {
                        panel_Temp.SetValue(SolverParameter.MaxExtend, maxExtension);
                    }
                }

                panels[i] = panel_Temp;

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

            if(panels != null)
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

            //Analytical.Solver.Modify.Snap(
            //    panels, bucketSizes, weights, maxExtensions, ranges,
            //    levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength,
            //    toleranceDistance, toleranceAngleRad, arcToleranceAngleRad, out List<Geometry.Spatial.Point3D> nakedPoint3Ds);

            index = Params.IndexOfOutputParam("panels");
            if(index != -1)
            {
                if(ranges == null)
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

                    if(!dictionary.TryGetValue(count, out List<Panel> panels_Temp) || panels_Temp == null)
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
                foreach(KeyValuePair<int, List<Panel>> keyValuePair in dictionary)
                {
                    GH_Path path = new GH_Path(keyValuePair.Key);
                    List<Panel> panels_Temp = keyValuePair.Value;

                    foreach(Panel panel in panels_Temp)
                    {
                        dataTree_Panel.Add(new GooPanel(panel), path);
                    }

                    if(dataTree_Shell is not null)
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

                    if (dataTree_Outlines is not null)
                    {
                        double elevation = ranges[keyValuePair.Key].Max;

                        Dictionary<Panel, List<ISAMGeometry3D>> dictionary_Section = Analytical.Query.SectionDictionary<ISAMGeometry3D>(panels_Temp, Geometry.Spatial.Plane.WorldXY.GetMoved(new Vector3D(0, 0, elevation)) as Geometry.Spatial.Plane, toleranceDistance);

                        List<ISAMGeometry3D> sAMGeometry3Ds = new List<ISAMGeometry3D>();
                        foreach(List<ISAMGeometry3D> sAMGeometry3Ds_Temp in dictionary_Section.Values )
                        {
                            sAMGeometry3Ds_Temp.ForEach(x => dataTree_Outlines.Add(new GooSAMGeometry(x), path));
                        }


                    }
                }

                dataAccess.SetDataTree(index, dataTree_Panel);

                if(index_Shells != -1)
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

        private List<Shell> getShells(IEnumerable<Panel> panels, double tolerance)
        {
            if(panels == null)
            {
                return null;
            }

            BoundingBox3D boundingBox3D = panels.BoundingBox3D();
            if(boundingBox3D == null)
            {
                return null;
            }

            double elevation_Min = boundingBox3D.Min.Z;
            double elevation_Max = boundingBox3D.Max.Z;

            Point3D point3D = boundingBox3D.GetCentroid();

            Geometry.Spatial.Plane plane_Min = new Geometry.Spatial.Plane(new Point3D(point3D.X, point3D.Y, elevation_Min), Vector3D.WorldZ.GetNegated());
            Geometry.Spatial.Plane plane = new Geometry.Spatial.Plane(point3D, Vector3D.WorldZ);
            Geometry.Spatial.Plane plane_Max = new Geometry.Spatial.Plane(new Point3D(point3D.X, point3D.Y, elevation_Max), Vector3D.WorldZ);


            Dictionary<Panel, List<ISegmentable2D>> dictionary = Analytical.Query.SectionDictionary<ISegmentable2D>(panels, plane);
            if(dictionary == null)
            {
                return null;
            }

            List<Tuple<Panel, Face3D, BoundingBox3D, List<Point3D>>> tuples = new List<Tuple<Panel, Face3D, BoundingBox3D, List<Point3D>>>();

            List<Segment2D> segment2Ds = new List<Segment2D>();
            foreach(KeyValuePair<Panel, List<ISegmentable2D>> keyValuePair in dictionary)
            {
                foreach(ISegmentable2D segmentable2D in keyValuePair.Value)
                {
                    segment2Ds.AddRange(segmentable2D.GetSegments());
                }

                Face3D face3D = keyValuePair.Key.Face3D;

                BoundingBox3D boundingBox3D_Face3D = face3D.GetBoundingBox();

                List<Point3D> point3Ds = (face3D.GetExternalEdge3D() as ISegmentable3D)?.GetPoints();

                tuples.Add(new Tuple<Panel, Face3D, BoundingBox3D, List<Point3D>>(keyValuePair.Key, face3D, boundingBox3D_Face3D, point3Ds));
            }

            List<Face2D> face2Ds = segment2Ds.Face2Ds();
            face2Ds.Holes()?.ForEach(x => face2Ds.Add(new Face2D(x)));

            Func<Point3D, Point3D> findExistingPoint3D = new Func<Point3D, Point3D>((Point3D x) =>
            {
                foreach (Tuple<Panel, Face3D, BoundingBox3D, List<Point3D>> tuple in tuples)
                {
                    foreach (Point3D point3D_Temp in tuple.Item4)
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

                List<Point3D> point3Ds = new List<Point3D>();
                foreach (Point3D point3D_Segmentable3D in segmentable3D.GetPoints())
                {
                    Point3D point3D_Temp = y.Project(point3D_Segmentable3D);
                    point3D_Temp = findExistingPoint3D.Invoke(point3D_Temp);
                    if (point3D_Temp == null)
                    {
                        continue;
                    }

                    Point3D point3D_Project = y.Project(point3D_Temp);
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
                if(segment2Ds_Face2D == null)
                {
                    continue;
                }

                face2D.InternalEdge2Ds?.FindAll(x => x is ISegmentable2D).ForEach(x => segment2Ds_Face2D.AddRange(((ISegmentable2D)x).GetSegments()));
            
                if(segment2Ds_Face2D == null || segment2Ds_Face2D.Count == 0)
                {
                    continue;
                }

                List<Face3D> face3Ds = new List<Face3D>();
                foreach (Segment2D segment2D in segment2Ds_Face2D)
                {
                    Point3D point3D_Segment2D = plane.Convert(segment2D.Mid());

                    Tuple<Panel, Face3D, BoundingBox3D, List<Point3D>> tuple = tuples.Find(x => x.Item3.InRange(point3D_Segment2D) && x.Item2.On(point3D_Segment2D));
                    face3Ds.Add(tuple.Item2);
                }

                Polygon2D externalEdge;
                List<Polygon2D> internalEdges;
                Face3D face3D_Temp = null;

                Face3D face3D = plane.Convert(face2D);

                externalEdge = createPolygon2D(face3D.GetExternalEdge3D(), plane_Min);
                internalEdges = face3D.GetInternalEdge3Ds()?.ConvertAll(x => createPolygon2D(x, plane_Min));

                face3D_Temp = Geometry.Spatial.Create.Face3Ds(externalEdge, internalEdges, plane_Min)?.FirstOrDefault();
                if(face3D_Temp != null)
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
