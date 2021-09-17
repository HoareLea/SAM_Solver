using Grasshopper.Kernel;
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
            : base("SnapSolver", "SnapSolver", "Snap Solver Version 1.4.2", "SAM", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //TODO: write better descriptions and verify information
            pManager.AddBrepParameter("_panelsBrep", "PB",
                "panels represented as a list of surface Brep geometry", GH_ParamAccess.list);
            pManager.AddNumberParameter("_bucketSizes", "BS", "a list of bucket sizes", GH_ParamAccess.list);
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
            pManager.AddBrepParameter("SnappedWallsSurfaces", "SWSrfs", "a tree of surface representations of snapped walls", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("SnappedWallsSources", "SWSrc", "the indexes of SnappedWallsSurfaces parent surfaces from _panelsBrep",
                GH_ParamAccess.tree);
            pManager.AddPointParameter("NakedEnds", "NE", "naked verticies of linear representation left after snapping", GH_ParamAccess.tree);
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

           
        }
    }
}
