// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Analytical.Solver;
using SAM.Core;
using SAM.Core.Grasshopper;
using SAM.Geometry.Grasshopper;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    /// <summary>
    /// Opt-in auto-tuning solver: a separate, additive component that wraps the standard Solver and
    /// escalates bucket/extension only on panels adjacent to remaining naked ends, accepting an
    /// escalation only when it improves the closure objective. The standard Solver is untouched.
    /// </summary>
    public class AutoTuneSolver : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("c0a7b9d2-4e51-4a3c-8b6f-2d9e7c1a5f04");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.10";

        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AutoTuneSolver()
            : base("AutoTuneSolver", "AutoTuneSolver", "SAM Solver with gap-driven auto-tuning of max extension and optional slit-merge width selection.\nEscalates only panels next to remaining naked ends, accepting changes only when closure improves.\nOpt-in and additive - the standard Solver is unaffected.", "SAM", "Solver")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "_panels", NickName = "_panels", Description = "Panels represented as a list of surface brep geometry.", Access = GH_ParamAccess.list, DataMapping = GH_DataMapping.Flatten }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketSizes_", NickName = "bucketSizes_", Description = "Starting bucket size per panel (the tuner escalates from these).", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "weights_", NickName = "weights_", Description = "A list of weights for the panels.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "maxExtensions_", NickName = "maxExtensions_", Description = "Starting max extension per panel (the tuner escalates from these).", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Interval() { Name = "levels_", NickName = "levels_", Description = "Information on each floor's elevation", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Number paramNumber;

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_levelOffset_", NickName = "_levelOffset_", Description = "Level Section Offset", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bucketBetweenLevels_", NickName = "bucketBetweenLevels_", Description = "A global parameter that aligns walls between levels within a defined bucket distance.", Access = GH_ParamAccess.item, Optional = true };
                paramNumber.SetPersistentData(0.21);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_nakedNodeSnapDistance_", NickName = "_nakedNodeSnapDistance_", Description = "Snap distance for a naked node (also the radius used to find gap-adjacent panels).", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_minWallSegmentLength_", NickName = "_minWallSegmentLength_", Description = "The smallest wall segment that won't be merged into another wall", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceDistance_", NickName = "_toleranceDistance_", Description = "Distance tolerance \ndefault 1E-06", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Distance);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceAngle_", NickName = "_toleranceAngle_", Description = "Distance angle in radians \ndefault 0.034907 RAD = 2 deg", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Tolerance.Angle);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                paramNumber = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_toleranceArc_", NickName = "_toleranceArc_", Description = "Arc angle tolerance in radians \ndefault 0.005236 RAD = 0.3 deg", Access = GH_ParamAccess.item };
                paramNumber.SetPersistentData(Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad);
                result.Add(new GH_SAMParam(paramNumber, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Boolean paramEscalate = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_escalateMaxExtend_", NickName = "_escalateMaxExtend_", Description = "Allow AutoTune to raise MaxExtend beyond the default/caller values.\nFalse keeps normal solver reach and leaves remaining naked ends.", Access = GH_ParamAccess.item };
                paramEscalate.SetPersistentData(false);
                result.Add(new GH_SAMParam(paramEscalate, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Integer paramInteger = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_maxRounds_", NickName = "_maxRounds_", Description = "Maximum number of escalation rounds (each round is a full solve). Only used when _escalateMaxExtend_ is true.", Access = GH_ParamAccess.item };
                paramInteger.SetPersistentData(6);
                result.Add(new GH_SAMParam(paramInteger, ParamVisibility.Voluntary));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "escalationLadder_", NickName = "escalationLadder_", Description = "Absolute MaxExtend targets (metres) tried on gap-adjacent panels, one step per round, until naked ends close without reducing closed loops.\ndefault: 0.5, 0.75, 1.0, 1.5", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Number paramMerge = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_mergeSlitWidth_", NickName = "_mergeSlitWidth_", Description = "Manual merge width for parallel 'double-wall' slits whose perpendicular gap is at most this (m).\n0 = off. Closure-guarded: rooms wider than this width are never erased. Keep it below your narrowest real room/corridor - a space narrower than the width can't be told from a slit and may be collapsed.\nIgnored when mergeSlitWidths_ is supplied.", Access = GH_ParamAccess.item };
                paramMerge.SetPersistentData(0.0);
                result.Add(new GH_SAMParam(paramMerge, ParamVisibility.Voluntary));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "mergeSlitWidths_", NickName = "mergeSlitWidths_", Description = "Optional candidate merge widths (m) for automatic slit-merge selection.\nWhen supplied, AutoTune first closes gaps with slit merging off, then tests each width and selects the best non-regressing result. 0 is always included as fallback.\nRecommended starter list: 0, 0.05, 0.10, 0.20, 0.30", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Number paramSlitMinGap = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_slitMinGap_", NickName = "_slitMinGap_", Description = "Minimum perpendicular gap width (m) to show in slits diagnostics.", Access = GH_ParamAccess.item };
                paramSlitMinGap.SetPersistentData(0.02);
                result.Add(new GH_SAMParam(paramSlitMinGap, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Number paramSlitMaxGap = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_slitMaxGap_", NickName = "_slitMaxGap_", Description = "Maximum perpendicular gap width (m) to show in slits diagnostics.\nFor typical actionable gaps try 0.4.", Access = GH_ParamAccess.item };
                paramSlitMaxGap.SetPersistentData(0.5);
                result.Add(new GH_SAMParam(paramSlitMaxGap, ParamVisibility.Voluntary));

                global::Grasshopper.Kernel.Parameters.Param_Number paramSlitMaxOverlap = new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_slitMaxOverlap_", NickName = "_slitMaxOverlap_", Description = "Maximum parallel overlap length (m) to show in slits diagnostics.\nShort overlaps are actionable gaps; longer side-by-side walls are ignored.\n0 = no overlap limit.", Access = GH_ParamAccess.item };
                paramSlitMaxOverlap.SetPersistentData(2.0);
                result.Add(new GH_SAMParam(paramSlitMaxOverlap, ParamVisibility.Voluntary));

                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels", NickName = "panels", Description = "SAM Analytical Panels", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Point() { Name = "nakedEnds", NickName = "nakedEnds", Description = "Remaining naked points after tuning", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "report", NickName = "report", Description = "Auto-tune trace: rounds run, selected merge width, naked/closed-loop counts before and after, panels escalated.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "slits", NickName = "slits", Description = "Remaining internal double-wall/slit diagnostics as short Segment3D connectors between near-parallel wall axes, grouped by level.", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "slitPanels", NickName = "slitPanels", Description = "Panels whose section axes touch the remaining slit diagnostics, grouped by level. Use these to target bucket-size overrides.", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "rooms", NickName = "rooms", Description = "Closed room polygons (Face3D) recognised in the tuned result, grouped by level to match the panels output. Sliver loops are excluded.", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));

                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<Panel> panels = new List<Panel>();
            List<double> bucketSizes = new List<double>();
            List<double> weights = new List<double>();
            List<double> maxExtensions = new List<double>();
            List<global::Rhino.Geometry.Interval> levels = new List<global::Rhino.Geometry.Interval>();

            double levelSectionOffset = Geometry.Solver.SnapSolver.DEFAULT_LevelSectionOffset;
            double nakedNodeSnapDistance = Geometry.Solver.SnapSolver.DEFAULT_NakedNodeSnapDistance;
            double minWallSegmentLength = Geometry.Solver.SnapSolver.DEFAULT_MinWallSegmentLength;
            double toleranceDistance = Tolerance.Distance;
            double toleranceAngleRad = Tolerance.Angle;
            double arcToleranceAngleRad = Geometry.Solver.SnapSolver.DEFAULT_ArcToleranceAngleRad;
            double bucketBetweenLevels = 0.21;
            int maxRounds = 6;
            bool escalateMaxExtend = false;
            double mergeSlitWidth = 0;
            double slitMinGap = 0.02;
            double slitMaxGap = 0.5;
            double slitMaxOverlap = 2.0;
            List<double> mergeSlitWidths = new List<double>();
            List<double> escalationLadder = new List<double>();

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

            index = Params.IndexOfInputParam("levels_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, levels);
            }

            index = Params.IndexOfInputParam("_levelOffset_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref levelSectionOffset);
            }

            index = Params.IndexOfInputParam("bucketBetweenLevels_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref bucketBetweenLevels);
            }

            index = Params.IndexOfInputParam("_nakedNodeSnapDistance_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref nakedNodeSnapDistance);
            }

            index = Params.IndexOfInputParam("_minWallSegmentLength_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref minWallSegmentLength);
            }

            index = Params.IndexOfInputParam("_toleranceDistance_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref toleranceDistance);
            }

            index = Params.IndexOfInputParam("_toleranceAngle_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref toleranceAngleRad);
            }

            index = Params.IndexOfInputParam("_toleranceArc_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref arcToleranceAngleRad);
            }

            index = Params.IndexOfInputParam("_escalateMaxExtend_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref escalateMaxExtend);
            }

            index = Params.IndexOfInputParam("_maxRounds_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref maxRounds);
            }

            index = Params.IndexOfInputParam("escalationLadder_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, escalationLadder);
            }

            index = Params.IndexOfInputParam("_mergeSlitWidth_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref mergeSlitWidth);
            }

            index = Params.IndexOfInputParam("mergeSlitWidths_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, mergeSlitWidths);
            }

            index = Params.IndexOfInputParam("_slitMinGap_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref slitMinGap);
            }

            index = Params.IndexOfInputParam("_slitMaxGap_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref slitMaxGap);
            }

            index = Params.IndexOfInputParam("_slitMaxOverlap_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref slitMaxOverlap);
            }

            for (int i = 0; i < panels.Count; i++)
            {
                Panel panel_Temp = Create.Panel(panels[i]);
                if (panel_Temp == null)
                {
                    continue;
                }

                if (bucketSizes != null && bucketSizes.Count != 0)
                {
                    double bucketSize = bucketSizes.Count <= i ? bucketSizes[bucketSizes.Count - 1] : bucketSizes[i];
                    if (!double.IsNaN(bucketSize))
                    {
                        panel_Temp.SetValue(SolverParameter.BucketSize, bucketSize);
                    }
                }

                if (weights != null && weights.Count != 0)
                {
                    double weight = weights.Count <= i ? weights[weights.Count - 1] : weights[i];
                    if (!double.IsNaN(weight))
                    {
                        panel_Temp.SetValue(SolverParameter.Weight, weight);
                    }
                }

                if (maxExtensions != null && maxExtensions.Count != 0)
                {
                    double maxExtension = maxExtensions.Count <= i ? maxExtensions[maxExtensions.Count - 1] : maxExtensions[i];
                    if (!double.IsNaN(maxExtension))
                    {
                        panel_Temp.SetValue(SolverParameter.MaxExtend, maxExtension);
                    }
                }

                panels[i] = panel_Temp;
            }

            List<Range<double>> ranges = new List<Range<double>>();
            if (levels != null)
            {
                foreach (global::Rhino.Geometry.Interval level in levels)
                {
                    ranges.Add(new Range<double>(level.Min, level.Max));
                }
            }

            if (ranges == null || ranges.Count == 0)
            {
                ranges = null;
            }

            AutoTuneSolver<Panel> autoTuneSolver = new AutoTuneSolver<Panel>(panels, ranges)
            {
                Tolerance_Angle = toleranceAngleRad,
                Tolerance_Distance = toleranceDistance,
                Tolerance_Arc = arcToleranceAngleRad,
                MinLength = minWallSegmentLength,
                SnapDistance = nakedNodeSnapDistance,
                Offset = levelSectionOffset,
                BucketBetweenLevels = bucketBetweenLevels,
                MaxRounds = maxRounds,
                EscalateMaxExtend = escalateMaxExtend,
                MergeSlitWidth = mergeSlitWidth,
                SlitDiagnosticMinGap = slitMinGap,
                SlitDiagnosticMaxGap = slitMaxGap,
                SlitDiagnosticMaxOverlap = slitMaxOverlap
            };

            if (mergeSlitWidths != null && mergeSlitWidths.Count != 0)
            {
                autoTuneSolver.MergeSlitWidths = mergeSlitWidths;
            }

            if (escalationLadder != null && escalationLadder.Count != 0)
            {
                autoTuneSolver.EscalationLadder = escalationLadder;
            }

            List<Panel> solvedPanels = autoTuneSolver.Execute(out List<Point3D> nakedPoint3Ds, out AutoTuneReport report);

            if (solvedPanels != null)
            {
                // Make sure each panel has a unique Guid.
                for (int i = 0; i < solvedPanels.Count; i++)
                {
                    Guid guid = solvedPanels[i].Guid;

                    List<Panel> panels_Guid = solvedPanels.FindAll(x => x.Guid == guid);
                    while (panels_Guid != null && panels_Guid.Count > 1)
                    {
                        guid = Guid.NewGuid();
                        solvedPanels[i] = Create.Panel(guid, solvedPanels[i]);
                        panels_Guid = solvedPanels.FindAll(x => x.Guid == guid);
                    }
                }
            }

            // Level buckets shared by the panels and rooms outputs so both are on matching branches.
            List<Range<double>> ranges_Output = ranges;
            if (ranges_Output == null && solvedPanels != null)
            {
                ranges_Output = Geometry.Object.Spatial.Query.ElevationRanges(solvedPanels);
            }

            index = Params.IndexOfOutputParam("panels");
            if (index != -1)
            {
                DataTree<GooPanel> dataTree_Panel = new DataTree<GooPanel>();

                if (solvedPanels != null)
                {
                    Dictionary<int, List<Panel>> dictionary = new Dictionary<int, List<Panel>>();
                    foreach (Panel panel in solvedPanels)
                    {
                        int count = -1;

                        Point3D centroid = panel?.GetBoundingBox()?.GetCentroid();
                        if (centroid != null && ranges_Output != null)
                        {
                            count = ranges_Output.FindIndex(x => x.In(centroid.Z));
                        }

                        if (count == -1)
                        {
                            count = ranges_Output == null ? 0 : ranges_Output.Count;
                        }

                        if (!dictionary.TryGetValue(count, out List<Panel> panels_Temp) || panels_Temp == null)
                        {
                            panels_Temp = new List<Panel>();
                            dictionary[count] = panels_Temp;
                        }

                        panels_Temp.Add(panel);
                    }

                    foreach (KeyValuePair<int, List<Panel>> keyValuePair in dictionary)
                    {
                        GH_Path path = new GH_Path(keyValuePair.Key);
                        foreach (Panel panel in keyValuePair.Value)
                        {
                            dataTree_Panel.Add(new GooPanel(panel), path);
                        }
                    }
                }

                dataAccess.SetDataTree(index, dataTree_Panel);
            }

            index = Params.IndexOfOutputParam("nakedEnds");
            if (index != -1)
            {
                dataAccess.SetDataList(index, nakedPoint3Ds?.ConvertAll(x => Geometry.Grasshopper.Convert.ToGrasshopper(x)));
            }

            index = Params.IndexOfOutputParam("report");
            if (index != -1)
            {
                dataAccess.SetData(index, report?.ToString());
            }

            index = Params.IndexOfOutputParam("slits");
            if (index != -1)
            {
                DataTree<GooSAMGeometry> dataTree_Slit = new DataTree<GooSAMGeometry>();

                List<Segment3D> slits = autoTuneSolver.Slits;
                if (slits != null)
                {
                    foreach (Segment3D slit in slits)
                    {
                        if (slit == null)
                        {
                            continue;
                        }

                        int count = -1;
                        Point3D start = slit.GetStart();
                        Point3D end = slit.GetEnd();
                        if (start != null && end != null && ranges_Output != null)
                        {
                            double z = (start.Z + end.Z) / 2;
                            count = ranges_Output.FindIndex(x => x.In(z));
                        }

                        if (count == -1)
                        {
                            count = ranges_Output == null ? 0 : ranges_Output.Count;
                        }

                        dataTree_Slit.Add(new GooSAMGeometry(slit), new GH_Path(count));
                    }
                }

                dataAccess.SetDataTree(index, dataTree_Slit);
            }

            index = Params.IndexOfOutputParam("slitPanels");
            if (index != -1)
            {
                DataTree<GooPanel> dataTree_SlitPanel = new DataTree<GooPanel>();

                List<Panel> slitPanels = autoTuneSolver.SlitPanels;
                if (slitPanels != null)
                {
                    foreach (Panel panel in slitPanels)
                    {
                        if (panel == null)
                        {
                            continue;
                        }

                        int count = -1;
                        Point3D centroid = panel.GetBoundingBox()?.GetCentroid();
                        if (centroid != null && ranges_Output != null)
                        {
                            count = ranges_Output.FindIndex(x => x.In(centroid.Z));
                        }

                        if (count == -1)
                        {
                            count = ranges_Output == null ? 0 : ranges_Output.Count;
                        }

                        dataTree_SlitPanel.Add(new GooPanel(panel), new GH_Path(count));
                    }
                }

                dataAccess.SetDataTree(index, dataTree_SlitPanel);
            }

            index = Params.IndexOfOutputParam("rooms");
            if (index != -1)
            {
                DataTree<GooSAMGeometry> dataTree_Room = new DataTree<GooSAMGeometry>();

                List<Face3D> rooms = autoTuneSolver.Rooms;
                if (rooms != null)
                {
                    foreach (Face3D room in rooms)
                    {
                        if (room == null)
                        {
                            continue;
                        }

                        // Bucket each room onto the same level branch as its panels, by section elevation.
                        int count = -1;
                        Point3D centroid = room.GetBoundingBox()?.GetCentroid();
                        if (centroid != null && ranges_Output != null)
                        {
                            count = ranges_Output.FindIndex(x => x.In(centroid.Z));
                        }

                        if (count == -1)
                        {
                            count = ranges_Output == null ? 0 : ranges_Output.Count;
                        }

                        dataTree_Room.Add(new GooSAMGeometry(room), new GH_Path(count));
                    }
                }

                dataAccess.SetDataTree(index, dataTree_Room);
            }
        }
    }
}
