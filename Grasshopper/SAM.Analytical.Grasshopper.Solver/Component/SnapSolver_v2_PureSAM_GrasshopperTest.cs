using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Classes;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SAM.Geometry.Grasshopper;
using Grasshopper.Kernel.Types;
using SAM.Core;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    public class SnapSolver_v2_PureSAM_GrasshopperTest : GH_SAMComponent
    {
        public override Guid ComponentGuid => new Guid("{83C6F5D9-F7CC-491B-94BA-AD0F87E1993F}");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.4.2";


        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.obscure;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SnapSolver_v2_PureSAM_GrasshopperTest()
            : base("SnapSolver_v142_TestPureSAM", "SnapSolver_v142_TestPureSAM", "Snap Solver Version 1.4.2_TestPureSAM", "SAM_TestPureSAM", "Solver_TestPureSAM")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //TODO: write better descriptions and verify information, full Sentencies with "."
            //defaults: _name_
            //no default value : _name
            //optional - no default, no obligation for the input : name_
            pManager.AddBrepParameter("_panelsBrep", "PB",
                "Panels represented as a list of surface brep geometry.", GH_ParamAccess.list);
            pManager.AddNumberParameter("_bucketSizes", "BS", "Bucket size per panel.", GH_ParamAccess.list);
            pManager.AddNumberParameter("_weights", "W", "a list of weighs or the panels", GH_ParamAccess.list);
            pManager.AddNumberParameter("_maxExtensions", "ME", "maximum extensions in a snapping process", GH_ParamAccess.list);
            pManager.AddIntervalParameter("_levels", "L", "information on each floor's elevation", GH_ParamAccess.list);
            pManager.AddNumberParameter("_levelSectionOffset", "LO", "floor height", GH_ParamAccess.item);
            pManager.AddNumberParameter("_nakedNodeSnapDistance", "NNSD", "snap distance for a naked node", GH_ParamAccess.item);
            pManager.AddNumberParameter("_minWallSegmentLength", "MWSL",
                "the smallest wall segment that won't be merged into an other wall", GH_ParamAccess.item);
            pManager.AddNumberParameter("_toleranceDistance", "±Dist", "tolerance distance", GH_ParamAccess.item);
            pManager.AddNumberParameter("_toleranceAngleRad", "±AngleRad", "angle tolerance in radians", GH_ParamAccess.item);
            pManager.AddNumberParameter("_arcToleranceAngleRad", "±ArcAngleRad", "arc angle tolerance in radians", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("snappedWallsSurfaces", "SWSrfs", "a tree of surface representations of snapped walls", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("snappedWallsSources", "SWSrc", "the indexes of SnappedWallsSurfaces parent surfaces from _panelsBrep",
                GH_ParamAccess.tree);
            pManager.AddPointParameter("nakedEnds", "NE", "naked verticies of linear representation left after snapping", GH_ParamAccess.tree);
            pManager.AddTextParameter("Debug", "Debug", "Debug", GH_ParamAccess.list);
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var panelsBrep = new List<GH_ObjectWrapper>();
            var bucketSizes = new List<double>();
            var weights = new List<double>();
            var maxExtensions = new List<double>();
            var levels = new List<Interval>();

            var levelSectionOffset = new double();
            var nakedNodeSnapDistance = new double();
            var minWallSegmentLength = new double();
            var toleranceDistance = new double();
            var toleranceAngleRad = new double();
            var arcToleranceAngleRad = new double();

            if (!DA.GetDataList(0, panelsBrep)) return;
            if (!DA.GetDataList(1, bucketSizes)) return;
            if (!DA.GetDataList(2, weights)) return;
            if (!DA.GetDataList(3, maxExtensions)) return;
            if (!DA.GetDataList(4, levels)) return;

            if (!DA.GetData(5, ref levelSectionOffset)) return;
            if (!DA.GetData(6, ref nakedNodeSnapDistance)) return;
            if (!DA.GetData(7, ref minWallSegmentLength)) return;
            if (!DA.GetData(8, ref toleranceDistance)) return;
            if (!DA.GetData(9, ref toleranceAngleRad)) return;
            if (!DA.GetData(10, ref arcToleranceAngleRad)) return;

            //List<GH_ObjectWrapper> panelsBrepForSAM = panelsBrep
            //    ?.Select(pb => new GH_ObjectWrapper(pb))
            //    .ToList();

            List<Face3D> panelFace3Ds = new List<Face3D>();
            foreach (GH_ObjectWrapper objectWrapper in panelsBrep)
                if (objectWrapper.TryGetSAMGeometries(out List<Face3D> face3Ds_Temp) && face3Ds_Temp != null)
                    panelFace3Ds.AddRange(face3Ds_Temp);

            List<Range<double>> levelsForSAM = new List<Range<double>>();
            foreach (Interval level in levels)
                levelsForSAM.Add(new Range<double>(level.Min, level.Max));

            SAM.Geometry.Solver.Query.Snap_v2(
                panelFace3Ds, bucketSizes, weights, maxExtensions, levelsForSAM,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength,
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad,
                out List<List<Face3D>> SnappedWalls,
                out List<List<int>> SnappedSources,
                out List<List<Point3D>> NakedEnds,
                out List<string> Debug);


            var snappedWallsBrep = new List<List<Brep>>();
            foreach (var walls in SnappedWalls)
            {
                var brepWalls = walls?.ConvertAll(f => f.ToRhino_Brep()).ToList();
                snappedWallsBrep.Add(brepWalls);
            }
            var snappedWallsSurfaces = ListOfListsToTree<Brep>(snappedWallsBrep);

            var nakedEndsPoint3d = new List<List<Point3d>>();
            foreach (var points in NakedEnds)
            {
                var points3d = points?.ConvertAll(p => new Point3d(p.X, p.Y, p.Z)).ToList();
                nakedEndsPoint3d.Add(points3d);
            }
            var nakedEnds = ListOfListsToTree<Point3d>(nakedEndsPoint3d);

            var snappedWallSources = ListOfListsToTree<int>(SnappedSources);

            DA.SetDataTree(0, snappedWallsSurfaces);
            DA.SetDataTree(1, snappedWallSources);
            DA.SetDataTree(2, nakedEnds);
            DA.SetDataList(3, Debug);

            //Geometry.Solver.Query.Snap_v2(face3Ds, out List<Face3D> result);
            //List<Brep> breps = result?.ConvertAll(x => x.ToRhino_Brep());
            ////Geometry.Grasshopper.Query.TryGetSAMGeometries()
        }
        private DataTree<T> ListOfListsToTree<T>(List<List<T>> list)
        {
            DataTree<T> tree = new DataTree<T>();
            int i = 0;
            foreach (List<T> innerList in list)
            {
                tree.AddRange(innerList, new GH_Path(new int[] { 0, i }));
                i++;
            }
            return tree;
        }
    }
}
