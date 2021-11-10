using Grasshopper.Kernel;
using Rhino.Geometry;
using SAM.Geometry.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Geometry.Grasshopper.Solver.Obsolete
{
    [Obsolete("Obsolete since 2021.10.04")]
    public class GrasshopperSnapSolver : GH_SAMComponent
    {
        public override Guid ComponentGuid => new Guid("{83C6F5D9-F7CC-491B-94BA-AD0F87E1993D}");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.4.2";


        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.hidden;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public GrasshopperSnapSolver()
            : base("GrasshopperSnapSolver", "GrasshopperSnapSolver", "Grasshopper Snap Solver Version", "SAM", "Solver")
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
            pManager.AddNumberParameter("_weights", "W", "A list of weighs of the panels", GH_ParamAccess.list);
            pManager.AddNumberParameter("_maxExtensions", "ME", "Maximum extensions for snapping process", GH_ParamAccess.list);
            pManager.AddIntervalParameter("_levels", "L", "Information on each floor's elevation", GH_ParamAccess.list);
            pManager.AddNumberParameter("_levelSectionOffset", "LO", "Floor height", GH_ParamAccess.item);
            pManager.AddNumberParameter("_nakedNodeSnapDistance", "NNSD", "Snap distance for a naked node", GH_ParamAccess.item);
            pManager.AddNumberParameter("_minWallSegmentLength", "MWSL",
                "The smallest wall segment that won't be merged into an other wall", GH_ParamAccess.item);
            pManager.AddNumberParameter("_toleranceDistance_", "±Dist", "Distance tolerance", 
                GH_ParamAccess.item, Core.Tolerance.Distance);
            pManager.AddNumberParameter("_toleranceAngleRad_", "±AngleRad", "Angle tolerance in radians", 
                GH_ParamAccess.item, Core.Tolerance.Angle);
            pManager.AddNumberParameter("_arcToleranceAngleRad", "±ArcAngleRad", "Arc angle tolerance in radians", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("SnappedWallsSurfaces", "SWSrfs", "A tree of surface representations of snapped walls", GH_ParamAccess.tree);
           pManager.AddIntegerParameter("SnappedWallsSources", "SWSrc", "The indexes of SnappedWallsSurfaces parent surfaces from _panelsBrep",
                GH_ParamAccess.tree);
            pManager.AddPointParameter("NakedEnds", "NE", "Naked verticies of linear representation left after snapping", GH_ParamAccess.tree);
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

            var SnapSolver = new Solver.GrasshopperSnapSolver(panelsBrep, bucketSizes, weights, maxExtensions, levels,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength, toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

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
