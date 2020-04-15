using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Solver.Grasshopper
{
    public class SnapSolverComponent_V3 : GH_Component
    {
        public SnapSolverComponent_V3() : base("SnapSolver V3", "SnapS 3", "Snap solver", "SAM", "Solver") { }

        public override Guid ComponentGuid => new Guid("{83C6F5D9-F7CC-491B-94BA-AD0F87E1993E}");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GooPanelParam(), "_panels", "P", "Panels", GH_ParamAccess.list);
            pManager.AddParameter(new GooPanelParam(), "fixed_", "F", "Fixed panels", GH_ParamAccess.list);

            Params.Input[1].Optional = true;

            pManager.AddIntervalParameter("_levels", "L", "Levels", GH_ParamAccess.list);
            pManager.AddNumberParameter("_bucketSize_", "B", "Bucket default: ", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("_gridSize_", "G", "Grid size default: ", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("_maxGap_", "M", "Max gap default: ", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("_angle_", "A", "Angle default: ", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("_tolerance_", "±", "Tolerance default: ", GH_ParamAccess.item, 0.01);
            pManager.AddPointParameter("_origin_", "O", "Grid origin default: ", GH_ParamAccess.item, new Rhino.Geometry.Point3d(0, 0, 0));
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Surfaces Walls", "W", "Walls per level", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Sources", "I", "Source panels indices per level and brep", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Curves Slabs ", "S", "Slabs", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GooPanel> Panels = new List<GooPanel>();
            List<GooPanel> FixedPanels = new List<GooPanel>();

            List<Interval> Levels = new List<Interval>();
            double bucket = 0.5, grid = 0.1, gap = 0.5, angle = 5, toler = 0.001;
            Point3d Origin = new Point3d();
            SortedList<Interval, List<Brep>> OutputWalls = null;
            SortedList<Interval, List<List<int>>> OutputIds = null;
            List<List<Curve>> SlabOutlines = null;

            if (!DA.GetDataList(0, Panels)) return;
            DA.GetDataList(1, FixedPanels);
            if (!DA.GetDataList(2, Levels)) return;
            if (!DA.GetData(3, ref bucket)) return;
            if (!DA.GetData(4, ref grid)) return;
            if (!DA.GetData(5, ref gap)) return;
            if (!DA.GetData(6, ref angle)) return;
            if (!DA.GetData(7, ref toler)) return;
            if (!DA.GetData(8, ref Origin)) return;

            List<Brep> fixedBreps = new List<Brep>();
            List<Brep> panelBreps = new List<Brep>();

            foreach (GooPanel item in Panels) 
                panelBreps.Add(item.Value.ToRhino());

            if (FixedPanels.Count > 0) {
                foreach (GooPanel item in FixedPanels) {
                    fixedBreps.Add(item.Value.ToRhino());
                }
            }

            List<Line> fixedlines = new List<Line>();

            foreach (Interval interval in Levels) {
                foreach (Brep brep in fixedBreps) {
                    Plane plane1 = new Plane(new Point3d(0, 0, (interval.T0 + interval.T1) / 2), Vector3d.ZAxis);
                    Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, plane1, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Curve[] curves1, out Point3d[] points1);

                    List<Curve> allcurves = new List<Curve>(curves1);
                    foreach (Curve item in allcurves) {
                        if (item.TryGetPolyline(out Polyline aspoly)) {
                            aspoly.Transform(Transform.PlanarProjection(Plane.WorldXY));
                            fixedlines.AddRange(aspoly.GetSegments());
                        }
                    }
                }
            }

            SnapSolver.SnapPanels(panelBreps, Levels, bucket, grid, gap, angle, toler, Origin, fixedlines, out OutputWalls, out OutputIds, out SlabOutlines);

            GH_Structure<GH_Brep> walls = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Integer> wallids = new GH_Structure<GH_Integer>(); 
            GH_Structure<GH_Curve> slabs = new GH_Structure<GH_Curve>();

            GH_Path tar = DA.ParameterTargetPath(0);

            for (int i = 0; i < OutputWalls.Keys.Count; i++) {
                GH_Path thispath = tar.AppendElement(i);

                List<Brep> thiswalls = OutputWalls[OutputWalls.Keys[i]];
                List<List<int>> thisids = OutputIds[OutputIds.Keys[i]];

                for (int j = 0; j < thiswalls.Count; j++) {
                    Brep item = thiswalls[j];
                    List<int> sources = thisids[j];
                    GH_Path thisbreppath = thispath.AppendElement(j);

                    item.Flip();

                    walls.Append(new GH_Brep(item), thisbreppath);
                    foreach (int index in sources) 
                        wallids.Append(new GH_Integer(index), thisbreppath);
                }
            }

            for (int i = 0; i < SlabOutlines.Count; i++) {
                List<Curve> thisout = SlabOutlines[i];
                GH_Path thispath = tar.AppendElement(i);

                foreach (Curve item in thisout)
                    slabs.Append(new GH_Curve(item), thispath);
            }

            DA.SetDataTree(0, walls);
            DA.SetDataTree(1, wallids);
            DA.SetDataTree(2, slabs);
        }
    }
}
