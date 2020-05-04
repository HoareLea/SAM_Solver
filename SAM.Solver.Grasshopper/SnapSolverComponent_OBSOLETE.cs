using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace SAM.Solver.Grasshopper
{
    public class SnapSolverComponent_OBSOLETE : GH_Component
    {
        public SnapSolverComponent_OBSOLETE() : base("SnapSolver", "SnapS", "Snap solver", "SAM", "Solver")
        {
        }

        public override Guid ComponentGuid => new Guid("{9A8FCA14-FC72-42C8-B540-BACE49096E42}");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Breps", "B", "Breps", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Levels", "L", "Levels", GH_ParamAccess.list);
            pManager.AddNumberParameter("BucketSize", "B", "Bucket size", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("GridSize", "G", "Grid size", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("MaxGap", "M", "Max gap", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Angle", "A", "Angle", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("Tolerance", "±", "Tolerance", GH_ParamAccess.item, 0.01);
            pManager.AddPointParameter("Origin", "O", "Grid origin", GH_ParamAccess.item, new Rhino.Geometry.Point3d(0, 0, 0));
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Walls", "W", "Walls", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Slabs", "S", "Slabs", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> Panels = new List<Brep>();
            List<Interval> Levels = new List<Interval>();
            double bucket = 0.5, grid = 0.1, gap = 0.5, angle = 5, toler = 0.001;
            Point3d Origin = new Point3d();
            SortedList<Interval, List<Brep>> OutputWalls = null;
            List<List<Curve>> SlabOutlines = null;

            if (!DA.GetDataList(0, Panels)) return;
            if (!DA.GetDataList(1, Levels)) return;
            if (!DA.GetData(2, ref bucket)) return;
            if (!DA.GetData(3, ref grid)) return;
            if (!DA.GetData(4, ref gap)) return;
            if (!DA.GetData(5, ref angle)) return;
            if (!DA.GetData(6, ref toler)) return;
            if (!DA.GetData(7, ref Origin)) return;

            SnapSolver.SnapPanels(Panels, Levels, bucket, grid, gap, angle, toler, Origin, out OutputWalls, out SlabOutlines);

            GH_Structure<GH_Brep> walls = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Curve> slabs = new GH_Structure<GH_Curve>();

            GH_Path tar = DA.ParameterTargetPath(0);

            for (int i = 0; i < OutputWalls.Keys.Count; i++)
            {
                GH_Path thispath = tar.AppendElement(i);

                List<Brep> thiswalls = OutputWalls[OutputWalls.Keys[i]];
                foreach (Brep item in thiswalls)
                    walls.Append(new GH_Brep(item), thispath);
            }

            for (int i = 0; i < SlabOutlines.Count; i++)
            {
                List<Curve> thisout = SlabOutlines[i];
                GH_Path thispath = tar.AppendElement(i);

                foreach (Curve item in thisout)
                    slabs.Append(new GH_Curve(item), thispath);
            }

            DA.SetDataTree(0, walls);
            DA.SetDataTree(1, slabs);
        }
    }
}