using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper;
using SAM.Core.Grasshopper;
using SAM.Analytical.Grasshopper.Solver.Properties;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Solver.Grasshopper
{
    public class SnapSolver : GH_SAMComponent
    {
        public override Guid ComponentGuid => new Guid("{83C6F5D9-F7CC-491B-94BA-AD0F87E1993E}");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";


        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.obscure;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SnapSolver() 
            : base("SnapSolver", "SnapSolver", "Snap Solver Version 3", "SAM", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GooPanelParam(), "_panels", "P", "SAM Analytical Panels", GH_ParamAccess.list);
            pManager.AddParameter(new GooPanelParam(), "fixed_", "F", "SAM Analytical Fixed panels, will be not modified unless included aslo in _panels ", GH_ParamAccess.list);

            Params.Input[1].Optional = true;

            pManager.AddIntervalParameter("_levels", "L", "Levels as Numberic Domain", GH_ParamAccess.list);
            pManager.AddNumberParameter("_bucketSize_", "B", "Bucket default: 0.19m, distance to squash location line of walls", GH_ParamAccess.item, 0.19); //0.5
            pManager.AddNumberParameter("_gridSize_", "G", "Grid size default: 0.2m", GH_ParamAccess.item, 0.2); //0.1 
            pManager.AddNumberParameter("_maxGap_", "M", "Max gap default: 0.2m ", GH_ParamAccess.item, 0.2); //0.5
            pManager.AddNumberParameter("_angle_", "A", "Angle default: 5 deg ", GH_ParamAccess.item, 5); //5
            pManager.AddNumberParameter("_tolerance_", "±", "Tolerance default: 0.01m ", GH_ParamAccess.item, 0.01); //0.01
            pManager.AddPointParameter("_origin_", "O", "Grid origin default: (0,0,0) ", GH_ParamAccess.item, new Point3d(0, 0, 0)); //new Rhino.Geometry.Point3d(0, 0, 0)
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Surfaces Walls", "W", "Walls per level as Rhino Surfaces", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Sources", "I", "Source panels indices per level and brep from _panels list ", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Curves Slabs ", "S", "Slabs", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BucketAxes ", "BS", "Bucket Axes as Rhino Curves to be used for further snapping", GH_ParamAccess.list);
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ////TODO: Find better Way to change tolerance
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance = Core.Tolerance.MacroDistance; 1e-3
            //Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem = Rhino.UnitSystem.Meters;

            if (!CheckTolerance())
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Set tolerance");
                return;
            }

            List<GooPanel> Panels = new List<GooPanel>();
            List<GooPanel> FixedPanels = new List<GooPanel>();

            List<Interval> Levels = new List<Interval>();
            double bucket = 0.5, grid = 0.1, gap = 0.5, angle = 5, toler = 0.001;
            Point3d Origin = new Point3d();
            SortedList<Interval, List<Brep>> OutputWalls = null;
            SortedList<Interval, List<List<int>>> OutputIds = null;
            List<List<Curve>> SlabOutlines = null;
            List<Line> Axes = null;

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

            if (FixedPanels.Count > 0)
            {
                foreach (GooPanel item in FixedPanels)
                {
                    fixedBreps.Add(item.Value.ToRhino());
                }
            }

            List<Line> fixedlines = new List<Line>();

            foreach (Interval interval in Levels)
            {
                foreach (Brep brep in fixedBreps)
                {
                    Plane plane1 = new Plane(new Point3d(0, 0, (interval.T0 + interval.T1) / 2), Vector3d.ZAxis);
                    Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, plane1, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Curve[] curves1, out Point3d[] points1);

                    List<Curve> allcurves = new List<Curve>(curves1);
                    foreach (Curve item in allcurves)
                    {
                        if (item.TryGetPolyline(out Polyline aspoly))
                        {
                            aspoly.Transform(Transform.PlanarProjection(Plane.WorldXY));
                            fixedlines.AddRange(aspoly.GetSegments());
                        }
                    }
                }
            }

            Core.Grasshopper.Solver.Query.Snap(panelBreps, Levels, bucket, grid, gap, angle, toler, Origin, fixedlines, out OutputWalls, out OutputIds, out SlabOutlines, out Axes);

            GH_Structure<GH_Brep> walls = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Integer> wallids = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Curve> slabs = new GH_Structure<GH_Curve>();

            GH_Path tar = DA.ParameterTargetPath(0);

            for (int i = 0; i < OutputWalls.Keys.Count; i++)
            {
                GH_Path thispath = tar.AppendElement(i);

                List<Brep> thiswalls = OutputWalls[OutputWalls.Keys[i]];
                List<List<int>> thisids = OutputIds[OutputIds.Keys[i]];

                for (int j = 0; j < thiswalls.Count; j++)
                {
                    Brep item = thiswalls[j];
                    List<int> sources = thisids[j];
                    GH_Path thisbreppath = thispath.AppendElement(j);

                    item.Flip();

                    walls.Append(new GH_Brep(item), thisbreppath);
                    foreach (int index in sources)
                        wallids.Append(new GH_Integer(index), thisbreppath);
                }
            }

            for (int i = 0; i < SlabOutlines.Count; i++)
            {
                List<Curve> thisout = SlabOutlines[i];
                GH_Path thispath = tar.AppendElement(i);

                foreach (Curve item in thisout)
                    slabs.Append(new GH_Curve(item), thispath);
            }

            DA.SetDataTree(0, walls);
            DA.SetDataTree(1, wallids);
            DA.SetDataTree(2, slabs);
            DA.SetDataList(3, Axes);
        }
    }
}