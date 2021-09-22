using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Classes;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    public class SnapSolver_v2 : GH_SAMComponent
    {
        public override Guid ComponentGuid => new Guid("{83C6F5D9-F7CC-491B-94BA-AD0F87E1993D}");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.4.2";


        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.obscure;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SnapSolver_v2()
            : base("SnapSolver_v142", "SnapSolver_v142", "Snap Solver Version 1.4.2", "SAM", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //TODO: write better descriptions and verify information, full Sentencies with "."
            pManager.AddBrepParameter("_panelsBrep", "Panels Brep",
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
            pManager.AddBrepParameter("Snapped walls surfaces", "SWSrfs", "a tree of surface representations of snapped walls", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Snapped walls sources", "SWSrc", "the indexes of SnappedWallsSurfaces parent surfaces from _panelsBrep",
                GH_ParamAccess.tree);
            pManager.AddPointParameter("Naked ends", "NE", "naked verticies of linear representation left after snapping", GH_ParamAccess.tree);
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var panelsBrep = new List<Brep>();
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


            var SnapSolver = new SnapSolver_GH(panelsBrep, bucketSizes, weights, maxExtensions, levels,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength, toleranceDistance,
                toleranceAngleRad, arcToleranceAngleRad);

            SnapSolver.Execute();

            var snappedWallsSurfaces = SnapSolver.SnappedWalls;
            var snappedWallSources = SnapSolver.SnappedSources;
            var nakedEnds = SnapSolver.NakedEnds;

            DA.SetDataTree(0, snappedWallsSurfaces);
            DA.SetDataTree(1, snappedWallSources);
            DA.SetDataTree(2, nakedEnds);
        }
    }
}
