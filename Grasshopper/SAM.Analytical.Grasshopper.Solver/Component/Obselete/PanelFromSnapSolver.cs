using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper;
using SAM.Core.Grasshopper;
using SAM.Geometry.Spatial;
using SAM.Analytical.Grasshopper.Solver.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Solver.Grasshopper.Obsolete
{
    [Obsolete("Obsolete since 2021.11.09")]
    public class PanelFromSnapSolver : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("5857acd8-c038-4a1e-9bde-33dedfed4fde");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.2";

        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.hidden;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver1;

        public PanelFromSnapSolver() : base("PanelFromSnapSolver", "PanelFromSnapSolver", "PanelFromSnapSolver", "SAM", "Solver")
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

                GooPanelParam gooPanelParam = new GooPanelParam() { Name = "_panels", NickName = "_panels", Description = "SAM Analytical Panel or AnalyticalModel", Access = GH_ParamAccess.list };
                gooPanelParam.DataMapping = GH_DataMapping.Flatten;
                result.Add(new GH_SAMParam(gooPanelParam, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Brep() { Name = "_surfaces", NickName = "_surfaces", Description = "Surfaces", Access = GH_ParamAccess.tree}, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_sources", NickName = "_sources", Description = "Sources Indexes", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_bottom_", NickName = "_bottom_", Description = "Bottom Elevation", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_top_", NickName = "_top_", Description = "Top Elevation", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
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
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels", NickName = "panels", Description = "SAM Analytical Panels", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "unusedPanels", NickName = "unusedPanels", Description = "Unused SAM Analytical Panels", Access = GH_ParamAccess.list }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int index = -1;

            index = Params.IndexOfInputParam("_panels");
            List<Panel> panels = new List<Panel>();
            if (index == -1 || !DA.GetDataList(index, panels))
                return;

            index = Params.IndexOfInputParam("_surfaces");
            GH_Structure<GH_Brep> breps = new GH_Structure<GH_Brep>();
            if (index == -1 || !DA.GetDataTree(index, out breps))
                return;

            index = Params.IndexOfInputParam("_sources");
            GH_Structure<GH_Integer> sources;
            if (index == -1 || !DA.GetDataTree(index, out sources))
                return;

            double elevation_Bottom = double.NaN;
            index = Params.IndexOfInputParam("_bottom_");
            if (index != -1)
            {
                double elevation_Bottom_Temp = double.NaN;
                if (DA.GetData(index, ref elevation_Bottom_Temp))
                    elevation_Bottom = elevation_Bottom_Temp;
            }

            double elevation_Top = double.NaN;
            index = Params.IndexOfInputParam("_top_");
            if (index != -1)
            {
                double elevation_Top_Temp = double.NaN;
                if (DA.GetData(index, ref elevation_Top_Temp))
                    elevation_Top = elevation_Top_Temp;
            }

            List<Tuple<int, List<int>>> tuples = new List<Tuple<int, List<int>>>();
            HashSet<int> indexes_Unique = new HashSet<int>();
            for (int i = 0; i < sources.PathCount; i++)
            {
                List<GH_Integer> goos = sources[i];
                List<int> indexes_Temp = new List<int>();
                foreach (GH_Integer goo in goos)
                {
                    int index_Temp = goo.Value;
                    indexes_Temp.Add(index_Temp);
                    indexes_Unique.Add(index_Temp);
                }
                tuples.Add(new Tuple<int, List<int>>(i, indexes_Temp));
            }

            double tolerance = Core.Tolerance.Distance;
            double maxDistance = 0.55;
            double minArea = Core.Tolerance.MacroDistance;

            List<Aperture> apertures = new List<Aperture>();
            for(int i=0; i < panels.Count; i++)
            {
                Panel panel = panels[i];
                if(panel == null)
                {
                    continue;
                }

                panel = Analytical.Create.Panel(panel);
                List<Aperture> apertures_Panel = panel.Apertures;
                if(apertures_Panel != null && apertures_Panel.Count != 0)
                {
                    apertures_Panel.RemoveAll(x => x == null);
                    apertures.AddRange(apertures_Panel);
                    panel.RemoveApertures();
                }

                panels[i] = panel;
            }

            List<Panel> result = new List<Panel>();
            foreach (Tuple<int, List<int>> tuple in tuples)
            {
                int index_Brep = tuple.Item1;
                if (index_Brep == -1)
                    continue;

                List<int> indexes_Panel = tuple.Item2;

                GH_Brep brep = breps[index_Brep][0];
                if (brep == null)
                    continue;

                List<Face3D> face3Ds = Geometry.Spatial.Query.Face3Ds(Geometry.Grasshopper.Convert.ToSAM(brep, true));
                if (face3Ds == null || face3Ds.Count == 0)
                    continue;

                Face3D face3D = face3Ds.First();
                if (!double.IsNaN(elevation_Bottom) || !double.IsNaN(elevation_Top))
                {
                    BoundingBox3D boundingBox3D = face3D.GetBoundingBox();
                    Plane plane_Mid = Plane.WorldXY.GetMoved(new Vector3D(0, 0, boundingBox3D.GetCenter().Z)) as Plane;
                    

                    PlanarIntersectionResult planarIntersectionResult = Geometry.Spatial.Create.PlanarIntersectionResult(plane_Mid, face3D);
                    if(planarIntersectionResult != null && planarIntersectionResult.Intersecting)
                    {
                        List<Geometry.Planar.ISegmentable2D> segmentable2Ds = planarIntersectionResult.GetGeometry2Ds<Geometry.Planar.ISegmentable2D>();
                        if(segmentable2Ds != null && segmentable2Ds.Count != 0)
                        {
                            if (segmentable2Ds.Count > 1)
                                segmentable2Ds.Sort((x, y) => y.GetLength().CompareTo(x.GetLength()));

                            double elevation_Bottom_Temp = elevation_Bottom;
                            if (double.IsNaN(elevation_Bottom_Temp))
                                elevation_Bottom_Temp = boundingBox3D.Min.Z;

                            Plane plane_Bottom = Plane.WorldXY.GetMoved(new Vector3D(0, 0, elevation_Bottom_Temp)) as Plane;

                            Segment3D segment3D = plane_Bottom.Convert(segmentable2Ds[0]) as Segment3D;
                            if (segment3D != null)
                            {
                                double elevation_Top_Temp = elevation_Top;
                                if (double.IsNaN(elevation_Top_Temp))
                                    elevation_Top_Temp = boundingBox3D.Max.Z;

                                Face3D face3D_Temp = Geometry.Spatial.Create.Face3D(segment3D, elevation_Top_Temp - elevation_Bottom_Temp);
                                if (face3D_Temp != null)
                                    face3D = face3D_Temp;
                            }
                        }
                    }
                }

                List<Panel> panels_Old = null;
                if (indexes_Panel == null || indexes_Panel.Count == 0)
                {
                    //TODO: Temporary solution to find panels has not been assigned to Brep
                    Point3D point3D_Internal = face3D.InternalPoint3D(tolerance);
                    List<Tuple<Panel, double>> tuples_Distance = panels.FindAll(x => x != null).ConvertAll(x => new Tuple<Panel, double>(x, Math.Min(x.Distance(point3D_Internal), face3D.Distance(x.GetFace3D().InternalPoint3D(tolerance)))));
                    tuples_Distance.RemoveAll(x => x.Item2 > maxDistance);
                    tuples_Distance.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                    panels_Old = tuples_Distance.ConvertAll(x => x.Item1);
                }
                else
                {
                    panels_Old = indexes_Panel.ConvertAll(x => panels[x]);
                }

                if(panels_Old == null)
                {
                    continue;
                }

                panels_Old.RemoveAll(x => x == null);
                if (panels_Old.Count == 0)
                    continue;

                Panel panel_Old = panels_Old.Find(x => x.PanelType != PanelType.Air && x.Construction != null);
                if (panel_Old == null)
                    panels_Old.Find(x => x.Construction != null);

                 if (panel_Old == null)
                    panel_Old = panels_Old.FirstOrDefault();

                if (panel_Old == null)
                    continue;

                Guid guid = panel_Old.Guid;
                if (result.Find(x => x.Guid == guid) != null)
                    guid = Guid.NewGuid();

                Panel panel_New = Analytical.Create.Panel(guid, panel_Old, face3D);

                result.Add(panel_New);
            }

            apertures = apertures.Fit(result, 0.5, maxDistance, tolerance);
            foreach(Panel panel in result)
            {
                foreach(Aperture aperture in apertures)
                {
                    Analytical.Modify.AddApertures(panel, aperture.ApertureConstruction, aperture.GetFace3D(), true, minArea, maxDistance, tolerance);
                }
            }

            List<Panel> result_Unused = new List<Panel>();
            foreach (Panel panel in panels)
            {
                Panel panel_Temp = result.Find(x => x.Guid.Equals(panel.Guid));
                if (panel_Temp == null)
                    result_Unused.Add(panel);
            }

            index = Params.IndexOfOutputParam("panels");
            if (index != -1)
                DA.SetDataList(index, result.ConvertAll(x => new GooPanel(x)));

            index = Params.IndexOfOutputParam("unusedPanels");
            if (index != -1)
                DA.SetDataList(1, result_Unused.ConvertAll(x => new GooPanel(x)));
        }
    }
}