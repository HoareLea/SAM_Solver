//using Grasshopper.Kernel;
//using Grasshopper.Kernel.Data;
//using Grasshopper.Kernel.Types;
//using Rhino.Geometry;
//using SAM.Core.Grasshopper;
//using System;
//using System.Collections.Generic;

//namespace SAM.Geometry.Grasshopper.Solver.Obsolete_20200709
//{
//    [Obsolete("Obsolete since 2020-07-09 Version 1")]
//    public class SnapSolver: GH_SAMComponent
//    {
//        public override Guid ComponentGuid => new Guid("{9A8FCA14-FC72-42C8-B540-BACE49096E42}");

//        /// <summary>
//        /// The latest version of this component
//        /// </summary>
//        public override string LatestComponentVersion => "1.0.0";

//        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.hidden;
       
//        public SnapSolver() 
//            : base("SnapSolver Version 1", "SnapS", "Snap solver Version 1", "SAM", "Solver")
//        {
//        }

//        protected override void RegisterInputParams(GH_InputParamManager pManager)
//        {
//            pManager.AddBrepParameter("Breps", "B", "Breps", GH_ParamAccess.list);
//            pManager.AddIntervalParameter("Levels", "L", "Levels", GH_ParamAccess.list);
//            pManager.AddNumberParameter("BucketSize", "B", "Bucket size", GH_ParamAccess.item, 0.5);
//            pManager.AddNumberParameter("GridSize", "G", "Grid size", GH_ParamAccess.item, 0.1);
//            pManager.AddNumberParameter("MaxGap", "M", "Max gap", GH_ParamAccess.item, 0.5);
//            pManager.AddNumberParameter("Angle", "A", "Angle", GH_ParamAccess.item, 5);
//            pManager.AddNumberParameter("Tolerance", "±", "Tolerance", GH_ParamAccess.item, 0.01);
//            pManager.AddPointParameter("Origin", "O", "Grid origin", GH_ParamAccess.item, new Point3d(0, 0, 0));
//        }

//        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
//        {
//            pManager.AddBrepParameter("Walls", "W", "Walls", GH_ParamAccess.tree);
//            pManager.AddCurveParameter("Slabs", "S", "Slabs", GH_ParamAccess.tree);
//        }

//        protected override void SolveInstance(IGH_DataAccess DA)
//        {
//            List<Brep> breps = new List<Brep>();
//            List<Interval> Levels = new List<Interval>();
//            double bucket = 0.5, grid = 0.1, gap = 0.5, angle = 5, toler = 0.001;
//            Point3d Origin = new Point3d();
//            SortedList<Interval, List<Brep>> OutputWalls = null;
//            List<List<Curve>> SlabOutlines = null;

//            if (!DA.GetDataList(0, breps)) return;
//            if (!DA.GetDataList(1, Levels)) return;
//            if (!DA.GetData(2, ref bucket)) return;
//            if (!DA.GetData(3, ref grid)) return;
//            if (!DA.GetData(4, ref gap)) return;
//            if (!DA.GetData(5, ref angle)) return;
//            if (!DA.GetData(6, ref toler)) return;
//            if (!DA.GetData(7, ref Origin)) return;

//            Query.Snap(breps, Levels, bucket, grid, gap, angle, toler, Origin, out OutputWalls, out SlabOutlines);

//            GH_Structure<GH_Brep> walls = new GH_Structure<GH_Brep>();
//            GH_Structure<GH_Curve> slabs = new GH_Structure<GH_Curve>();

//            GH_Path tar = DA.ParameterTargetPath(0);

//            for (int i = 0; i < OutputWalls.Keys.Count; i++)
//            {
//                GH_Path thispath = tar.AppendElement(i);

//                List<Brep> thiswalls = OutputWalls[OutputWalls.Keys[i]];
//                foreach (Brep item in thiswalls)
//                    walls.Append(new GH_Brep(item), thispath);
//            }

//            for (int i = 0; i < SlabOutlines.Count; i++)
//            {
//                List<Curve> thisout = SlabOutlines[i];
//                GH_Path thispath = tar.AppendElement(i);

//                foreach (Curve item in thisout)
//                    slabs.Append(new GH_Curve(item), thispath);
//            }

//            DA.SetDataTree(0, walls);
//            DA.SetDataTree(1, slabs);
//        }
//    }
//}

//namespace SAM.Geometry.Grasshopper.Solver.Obsolete_20200710
//{
//    [Obsolete("Obsolete since 2020-07-10 Version 2")]
//    public class SnapSolver : GH_SAMComponent
//    {
//        public override Guid ComponentGuid => new Guid("{AD7D3DBB-0967-4E48-BBE7-1063FAD65DA1}");

//        /// <summary>
//        /// The latest version of this component
//        /// </summary>
//        public override string LatestComponentVersion => "1.0.0";

//        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.hidden;

//        public SnapSolver()
//            : base("SnapSolver Version 2", "SnapS 2", "Snap Solver Version 2", "SAM", "Solver")
//        {
//        }

//        protected override void RegisterInputParams(GH_InputParamManager pManager)
//        {
//            pManager.AddBrepParameter("Breps", "B", "Breps", GH_ParamAccess.list);
//            pManager.AddBrepParameter("Fixed", "F", "Fixed breps", GH_ParamAccess.list);

//            Params.Input[1].Optional = true;

//            pManager.AddIntervalParameter("Levels", "L", "Levels", GH_ParamAccess.list);
//            pManager.AddNumberParameter("BucketSize", "B", "Bucket size", GH_ParamAccess.item, 0.5);
//            pManager.AddNumberParameter("GridSize", "G", "Grid size", GH_ParamAccess.item, 0.1);
//            pManager.AddNumberParameter("MaxGap", "M", "Max gap", GH_ParamAccess.item, 0.5);
//            pManager.AddNumberParameter("Angle", "A", "Angle", GH_ParamAccess.item, 5);
//            pManager.AddNumberParameter("Tolerance", "±", "Tolerance", GH_ParamAccess.item, 0.01);
//            pManager.AddPointParameter("Origin", "O", "Grid origin", GH_ParamAccess.item, new Point3d(0, 0, 0));
//        }

//        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
//        {
//            pManager.AddBrepParameter("Walls", "W", "Walls", GH_ParamAccess.tree);
//            pManager.AddCurveParameter("Slabs", "S", "Slabs", GH_ParamAccess.tree);
//        }

//        protected override void SolveInstance(IGH_DataAccess DA)
//        {
//            List<Brep> breps = new List<Brep>();
//            List<Brep> FixedPanels = new List<Brep>();
//            List<Interval> Levels = new List<Interval>();
//            double bucket = 0.5, grid = 0.1, gap = 0.5, angle = 5, toler = 0.001;
//            Point3d Origin = new Point3d();
//            SortedList<Interval, List<Brep>> OutputWalls = null;
//            List<List<Curve>> SlabOutlines = null;

//            if (!DA.GetDataList(0, breps)) return;
//            DA.GetDataList(1, FixedPanels);
//            if (!DA.GetDataList(2, Levels)) return;
//            if (!DA.GetData(3, ref bucket)) return;
//            if (!DA.GetData(4, ref grid)) return;
//            if (!DA.GetData(5, ref gap)) return;
//            if (!DA.GetData(6, ref angle)) return;
//            if (!DA.GetData(7, ref toler)) return;
//            if (!DA.GetData(8, ref Origin)) return;

//            if (FixedPanels.Count > 0)
//            {
//                List<Line> fixedlines = new List<Line>();

//                foreach (Interval interval in Levels)
//                {
//                    foreach (Brep brep in FixedPanels)
//                    {
//                        Plane plane1 = new Plane(new Point3d(0, 0, interval.T0), Vector3d.ZAxis);
//                        Plane plane2 = new Plane(new Point3d(0, 0, interval.T1), Vector3d.ZAxis);
//                        global::Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, plane1, global::Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Curve[] curves1, out Point3d[] points1);
//                        global::Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, plane2, global::Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Curve[] curves2, out Point3d[] points2);

//                        List<Curve> allcurves = new List<Curve>(curves1);
//                        allcurves.AddRange(curves2);
//                        foreach (Curve item in allcurves)
//                        {
//                            if (item.TryGetPolyline(out Polyline aspoly))
//                            {
//                                aspoly.Transform(Transform.PlanarProjection(Plane.WorldXY));
//                                fixedlines.AddRange(aspoly.GetSegments());
//                            }
//                        }
//                    }
//                }

//                Query.Snap(breps, Levels, bucket, grid, gap, angle, toler, Origin, fixedlines, out OutputWalls, out SlabOutlines);
//            }
//            else
//            {
//                Query.Snap(breps, Levels, bucket, grid, gap, angle, toler, Origin, out OutputWalls, out SlabOutlines);
//            }

//            GH_Structure<GH_Brep> walls = new GH_Structure<GH_Brep>();
//            GH_Structure<GH_Curve> slabs = new GH_Structure<GH_Curve>();

//            GH_Path tar = DA.ParameterTargetPath(0);

//            for (int i = 0; i < OutputWalls.Keys.Count; i++)
//            {
//                GH_Path thispath = tar.AppendElement(i);

//                List<Brep> thiswalls = OutputWalls[OutputWalls.Keys[i]];
//                foreach (Brep item in thiswalls)
//                    walls.Append(new GH_Brep(item), thispath);
//            }

//            for (int i = 0; i < SlabOutlines.Count; i++)
//            {
//                List<Curve> thisout = SlabOutlines[i];
//                GH_Path thispath = tar.AppendElement(i);

//                foreach (Curve item in thisout)
//                    slabs.Append(new GH_Curve(item), thispath);
//            }

//            DA.SetDataTree(0, walls);
//            DA.SetDataTree(1, slabs);
//        }
//    }
//}