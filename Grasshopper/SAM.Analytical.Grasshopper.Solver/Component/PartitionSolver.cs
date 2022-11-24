using Grasshopper.Kernel;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using SAM.Core;
using Grasshopper;
using Grasshopper.Kernel.Data;
using SAM.Analytical.Solver;
using System.Linq;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    [Obsolete("Obsolete since 2021.11.24")]
    public class PartitionSolver : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("28af0153-19c3-4fa2-afe2-9eeffa8f6c2e");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        public PartitionSolver()
            : base("PartitionSolver", "PartitionSolver", "SAM Solver", "SAM WIP", "Solver")
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
                result.Add(new GH_SAMParam(new GooPartitionParam() { Name = "_partitions", NickName = "_partitions", Description = "SAM Analytical Partitions", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Bucket size per panel.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weighs or the panels", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "maxExtensions_", NickName = "maxExtensions_", Description = "Maximum extensions in a snapping process", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Interval() { Name = "levels_", NickName = "levels_", Description = "Information on each floor's elevation", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Number paramNumber;

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_levelOffset_", NickName = "_levelOffset_", Description = "Level Section Offset", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_nakedNodeSnapDistance_", NickName = "_nakedNodeSnapDistance_", Description = "Snap distance for a naked node", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_minWallSegmentLength_", NickName = "_minWallSegmentLength_", Description = "The smallest wall segment that won't be merged into an other wall", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceDistance_", NickName = "_toleranceDistance_", Description = "Distance tolerance", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Distance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceAngle_", NickName = "_toleranceAngle_", Description = "Distance angle", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Angle);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceArc_", NickName = "_toleranceArc_", Description = "Arc angle tolerance in radians", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

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
                result.Add(new GH_SAMParam(new GooPartitionParam() { Name = "partitions", NickName = "partitions", Description = "SAM Analytical Partitions", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Point() { Name = "nakedEnds", NickName = "nakedEnds", Description = "Naked Points", Access = GH_ParamAccess.list }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<IPartition> partitions = new List<IPartition>();
            List<double> bucketSizes = new List<double>();
            List<double> weights = new List<double>();
            List<double> maxExtensions = new List<double>();
            List<Interval> levels = new List<Interval>();

            double levelSectionOffset = Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset;
            double nakedNodeSnapDistance = Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance;
            double minWallSegmentLength = Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength;
            double toleranceDistance = Tolerance.Distance;
            double toleranceAngleRad = Tolerance.Angle;
            double arcToleranceAngleRad = Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad;

            int index = -1;

            index = Params.IndexOfInputParam("_partitions");
            if (index == -1 || !DA.GetDataList(index, partitions))
            {
                return;
            }

            index = Params.IndexOfInputParam("bucketSizes_");
            if(index != -1)
            {
                DA.GetDataList(index, bucketSizes);
            }

            index = Params.IndexOfInputParam("weights_");
            if (index != -1)
            {
                DA.GetDataList(index, weights);
            }

            index = Params.IndexOfInputParam("maxExtensions_");
            if (index != -1)
            {
                DA.GetDataList(index, maxExtensions);
            }

            index = Params.IndexOfInputParam("levels_");
            if (index != -1)
            {
                DA.GetDataList(index, levels);
            }

            index = Params.IndexOfInputParam("_levelOffset_");
            if (index != -1)
            {
                DA.GetData(index, ref levelSectionOffset);
            }

            index = Params.IndexOfInputParam("_nakedNodeSnapDistance_");
            if (index != -1)
            {
                DA.GetData(index, ref nakedNodeSnapDistance);
            }

            index = Params.IndexOfInputParam("_minWallSegmentLength_");
            if (index != -1)
            {
                DA.GetData(index, ref minWallSegmentLength);
            }

            index = Params.IndexOfInputParam("_toleranceDistance_");
            if (index != -1)
            {
                DA.GetData(index, ref toleranceDistance);
            }

            index = Params.IndexOfInputParam("_toleranceAngle_");
            if (index != -1)
            {
                DA.GetData(index, ref toleranceAngleRad);
            }

            index = Params.IndexOfInputParam("_toleranceArc_");
            if (index != -1)
            {
                DA.GetData(index, ref arcToleranceAngleRad);
            }

            for (int i = 0; i < partitions.Count; i++)
            {
                IPartition partition_Temp = partitions[i]?.Clone();
                if (partition_Temp == null)
                {
                    continue;
                }

                if (bucketSizes != null && bucketSizes.Count != 0)
                {
                    double bucketSize = bucketSizes.Count <= i ? bucketSizes.Last() : bucketSizes[i];
                    if (!double.IsNaN(bucketSize))
                    {
                        partition_Temp.SetValue(SolverParameter.BucketSize, bucketSize);
                    }
                }

                if (weights != null && weights.Count != 0)
                {
                    double weight = weights.Count <= i ? weights.Last() : weights[i];
                    if (!double.IsNaN(weight))
                    {
                        partition_Temp.SetValue(SolverParameter.Weight, weight);
                    }
                }

                if (maxExtensions != null && maxExtensions.Count != 0)
                {
                    double maxExtension = maxExtensions.Count <= i ? maxExtensions.Last() : maxExtensions[i];
                    if (!double.IsNaN(maxExtension))
                    {
                        partition_Temp.SetValue(SolverParameter.MaxExtend, maxExtension);
                    }
                }

                partitions[i] = partition_Temp;

            }

            List<Range<double>> ranges = new List<Range<double>>();

            if (levels != null)
            {
                foreach (Interval level in levels)
                    ranges.Add(new Range<double>(level.Min, level.Max));
            }

            if (ranges == null || ranges.Count == 0)
            {
                ranges = null;
            }

            Solver<IPartition> partitionSolver = new Solver<IPartition>(partitions, ranges)
            {
                Tolerance_Angle = toleranceAngleRad,
                Tolerance_Distance = toleranceDistance,
                Tolerance_Arc = arcToleranceAngleRad,
                MinLength = minWallSegmentLength,
                SnapDistance = nakedNodeSnapDistance,
                Offset = levelSectionOffset
            };

            partitions = partitionSolver.Execute(out List<Geometry.Spatial.Point3D> nakedPoint3Ds, 0.21);

            index = Params.IndexOfOutputParam("partitions");
            if(index != -1)
            {
                if(ranges == null)
                {
                    ranges = Geometry.Spatial.Query.ElevationRanges(partitions);
                }
                
                DataTree<GooPartition> dataTree = new DataTree<GooPartition>();
                foreach(IPartition partition in partitions)
                {
                    int count = -1;

                    Geometry.Spatial.Point3D centroid = partition?.GetBoundingBox()?.GetCentroid();
                    if(centroid != null)
                    {
                        count = ranges.FindIndex(x => x.In(centroid.Z));
                    }

                    if(count == -1)
                    {
                        count = ranges.Count;
                    }

                    dataTree.Add(new GooPartition(partition), new GH_Path(count));
                }

                DA.SetDataTree(index, dataTree);
            }

            index = Params.IndexOfOutputParam("nakedEnds");
            if (index != -1)
            {
                DA.SetDataList(index, nakedPoint3Ds?.ConvertAll(x => Geometry.Grasshopper.Convert.ToGrasshopper(x)));
            }
        }
    }
}
