using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Linq;
using SAM.Analytical.Solver;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    /// <summary>
    /// Assigns individual snap properties (bucket size, weight and max extension) to panels so
    /// that each panel can be tuned before being fed into the Solver / SolverCurves components.
    /// </summary>
    public class SolverProperties : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("1282644d-1b95-4cc4-b5d5-b4851a0c7bcc");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SolverProperties()
            : base("SolverProperties", "SolverProperties", "Assign individual snap properties (bucket size, weight, max extension) to SAM Analytical Panels", "SAM", "Solver")
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
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "_panels", NickName = "_panels", Description = "SAM Analytical Panels", Access = GH_ParamAccess.list, DataMapping = GH_DataMapping.Flatten }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Bucket size per panel.\nIf fewer values than panels are supplied the last value is reused.\nNaN values are skipped.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weights for the panels (typically between 0.2 - 1, air: 0).\nIf fewer values than panels are supplied the last value is reused.\nNaN values are skipped.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "maxExtensions_", NickName = "maxExtensions_", Description = "Maximum extensions in a snapping process per panel.\nIf fewer values than panels are supplied the last value is reused.\nNaN values are skipped.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels", NickName = "panels", Description = "SAM Analytical Panels with assigned solver properties", Access = GH_ParamAccess.list }, ParamVisibility.Binding));

                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<Panel> panels = new List<Panel>();
            List<double> bucketSizes = new List<double>();
            List<double> weights = new List<double>();
            List<double> maxExtensions = new List<double>();

            int index = -1;

            index = Params.IndexOfInputParam("_panels");
            if (index == -1 || !dataAccess.GetDataList(index, panels))
            {
                return;
            }

            index = Params.IndexOfInputParam("bucketSizes_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, bucketSizes);
            }

            index = Params.IndexOfInputParam("weights_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, weights);
            }

            index = Params.IndexOfInputParam("maxExtensions_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, maxExtensions);
            }

            List<Panel> result = new List<Panel>();
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] == null)
                {
                    result.Add(null);
                    continue;
                }

                Panel panel = Create.Panel(panels[i]);
                if (panel == null)
                {
                    result.Add(panels[i]);
                    continue;
                }

                double bucketSize = GetValue(bucketSizes, i);
                if (!double.IsNaN(bucketSize))
                {
                    panel.SetValue(SolverParameter.BucketSize, bucketSize);
                }

                double weight = GetValue(weights, i);
                if (!double.IsNaN(weight))
                {
                    panel.SetValue(SolverParameter.Weight, weight);
                }

                double maxExtension = GetValue(maxExtensions, i);
                if (!double.IsNaN(maxExtension))
                {
                    panel.SetValue(SolverParameter.MaxExtend, maxExtension);
                }

                result.Add(panel);
            }

            index = Params.IndexOfOutputParam("panels");
            if (index != -1)
            {
                dataAccess.SetDataList(index, result.ConvertAll(x => x == null ? null : new GooPanel(x)));
            }
        }

        /// <summary>
        /// Returns the value for the panel at the given index. If fewer values than panels are
        /// supplied the last value is reused, matching the behaviour of the Solver component.
        /// </summary>
        private static double GetValue(List<double> values, int index)
        {
            if (values == null || values.Count == 0)
            {
                return double.NaN;
            }

            return values.Count <= index ? values.Last() : values[index];
        }
    }
}
