using Grasshopper.Kernel;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using SAM.Core;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    public class SnapSolver : GH_SAMComponent
    {
        public override Guid ComponentGuid => new Guid("{91F869F6-C75F-4F67-A459-F56AEA6DBC2F}");


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
            : base("SnapSolver", "SnapSolver", "Snap Solver", "SAM", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager manager)
        {
            //TODO: write better descriptions and verify information, full Sentencies with "."
            //defaults: _name_
            //no default value : _name
            //optional - no default, no obligation for the input : name_
            manager.AddParameter(new GooPanelParam(), "_panels", "PB", "Panels represented as a list of surface brep geometry.", GH_ParamAccess.list);
            manager.AddNumberParameter("_bucketSizes", "BS", "Bucket size per panel.", GH_ParamAccess.list);
            manager.AddNumberParameter("_weights", "W", "A list of weighs or the panels", GH_ParamAccess.list);
            manager.AddNumberParameter("_maxExtensions", "ME", "Maximum extensions in a snapping process", GH_ParamAccess.list);
            manager.AddIntervalParameter("_levels", "L", "Information on each floor's elevation", GH_ParamAccess.list);
            manager.AddNumberParameter("_levelSectionOffset_", "LO", "Floor height", GH_ParamAccess.item, Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset);
            manager.AddNumberParameter("_nakedNodeSnapDistance_", "NNSD", "Snap distance for a naked node", GH_ParamAccess.item, Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance);
            manager.AddNumberParameter("_minWallSegmentLength_", "MWSL", "The smallest wall segment that won't be merged into an other wall", GH_ParamAccess.item, Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength);
            manager.AddNumberParameter("_toleranceDistance_", "±Dist", "Distance tolerance", GH_ParamAccess.item, Tolerance.Distance);
            manager.AddNumberParameter("_toleranceAngleRad_", "±AngleRad", "Angle tolerance in radians", GH_ParamAccess.item, Tolerance.Angle);
            manager.AddNumberParameter("_arcToleranceAngleRad_", "±ArcAngleRad", "Arc angle tolerance in radians", GH_ParamAccess.item, Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GooPanelParam(), "panels", "panels", "Snapped SAM Analytical Panels", GH_ParamAccess.list);
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var panels = new List<Panel>();
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

            if (!DA.GetDataList(0, panels)) return;
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

            List<Range<double>> levelsForSAM = new List<Range<double>>();
            foreach (Interval level in levels)
                levelsForSAM.Add(new Range<double>(level.Min, level.Max));

            panels = panels?.ConvertAll(x => Create.Panel(x));

            Analytical.Solver.Modify.Snap(
                panels, bucketSizes, weights, maxExtensions, levelsForSAM,
                levelSectionOffset, nakedNodeSnapDistance, minWallSegmentLength,
                toleranceDistance, toleranceAngleRad, arcToleranceAngleRad);

            DA.SetDataList(0, panels?.ConvertAll(x => new GooPanel(x)));
        }
    }
}
