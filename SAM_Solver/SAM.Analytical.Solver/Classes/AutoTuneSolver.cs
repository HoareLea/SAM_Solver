// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Solver
{
    /// <summary>
    /// Opt-in, gap-driven auto-tuner that wraps <see cref="Solver{T}"/>. It runs a baseline solve, scores
    /// closure via <see cref="SAM.Geometry.Solver.ClosureSolver"/> (NTS snap-round + polygonise: rooms =
    /// closed loops, dangles = naked ends), then ramps <c>MaxExtend</c> on the panels next to the
    /// remaining dangles until the gaps close - accepting a step only while it does not reduce the room
    /// count, so clean regions are never over-merged. See docs/parameter-tuning-design.md.
    ///
    /// This is additive: it does not change <see cref="Solver{T}"/> or the default solve path.
    /// </summary>
    public class AutoTuneSolver<T> where T : IParameterizedSAMObject, IFace3DObject
    {
        /// <summary>
        /// Closed room polygons recognised in the most recent <see cref="Execute"/> result, one
        /// <see cref="Face3D"/> per room at its level's section elevation. Promotes the NTS polygonisation
        /// that already scores closure into actual output geometry (the "analysis-ready closed spaces").
        /// </summary>
        public List<Face3D> Rooms { get; private set; } = new List<Face3D>();

        /// <summary>
        /// Remaining internal double-wall/slit diagnostics from the most recent <see cref="Execute"/>.
        /// Each segment connects a pair of near-parallel wall axes that still sit apart after tuning.
        /// </summary>
        public List<Segment3D> Slits { get; private set; } = new List<Segment3D>();

        /// <summary>
        /// Panels whose section segments form the remaining <see cref="Slits"/> diagnostics.
        /// </summary>
        public List<T> SlitPanels { get; private set; } = new List<T>();

        // Mirrors the Solver<T> settings so the wrapped solves behave identically to a manual solve.
        public double Tolerance_Distance { get; set; } = Tolerance.Distance;
        public double Tolerance_Angle { get; set; } = Tolerance.Angle;
        public double Tolerance_Arc { get; set; } = 0.3 * (System.Math.PI / 180);
        public double Offset { get; set; } = 0.2;
        public double SnapDistance { get; set; } = 0.5;
        public double MinLength { get; set; } = 0.1;
        public double BucketBetweenLevels { get; set; } = 0.20;

        /// <summary>
        /// Absolute MaxExtend targets (metres), tried one step per round on the gap-adjacent panels.
        /// Absolute (not a multiplier) because the per-wall baseline MaxExtend is capped at ~0.49 x the
        /// wall length, so a short stub next to a gap could never reach across it with a multiplier.
        /// A step only ever raises a wall's reach, never lowers it.
        /// </summary>
        public List<double> EscalationLadder { get; set; } = new List<double>() { 0.5, 0.75, 1.0, 1.5 };

        /// <summary>
        /// Hard cap on the number of escalation rounds (each round is a full solve).
        /// </summary>
        public int MaxRounds { get; set; } = 6;

        /// <summary>
        /// When true, AutoTune may raise panel MaxExtend beyond the caller/default values. When false,
        /// only the normal solver's default extension is used and remaining naked ends are kept.
        /// </summary>
        public bool EscalateMaxExtend { get; set; } = true;

        /// <summary>
        /// Maximum perpendicular offset (m) of "double-wall" slits to merge into a single wall via
        /// <see cref="SnapSolver.PerpendicularMergeTolerance"/>. 0 (default) leaves slits untouched.
        /// The merge is closure-guarded, so a room wider than the width is never erased. Keep the width
        /// below your narrowest real room/corridor: a genuine space narrower than the width is bounded by
        /// the same parallel wall pair as a slit and may be collapsed.
        /// </summary>
        public double MergeSlitWidth { get; set; } = 0;

        /// <summary>
        /// Minimum perpendicular gap width reported as a slit diagnostic.
        /// </summary>
        public double SlitDiagnosticMinGap { get; set; } = 0.02;

        /// <summary>
        /// Maximum perpendicular gap width reported as a slit diagnostic.
        /// </summary>
        public double SlitDiagnosticMaxGap { get; set; } = 0.5;

        /// <summary>
        /// Maximum parallel overlap length reported as a slit diagnostic. Longer side-by-side walls are
        /// usually intentional wall runs rather than short actionable gaps.
        /// </summary>
        public double SlitDiagnosticMaxOverlap { get; set; } = 2.0;

        /// <summary>
        /// Optional candidate slit-merge widths (m). When supplied, AutoTune first performs the normal
        /// gap-closing tune with slit merging off, then tries each candidate on the tuned parameters and
        /// keeps the best non-regressing result. 0 is always included as a safe fallback.
        /// </summary>
        public List<double> MergeSlitWidths { get; set; } = new List<double>();

        // Closure scoring/extraction uses ClosureSolver's default snap-rounding grid and sliver threshold
        // (see ClosureSolver.DefaultGridSize / DefaultSliverMinSide).

        private readonly List<T> face3DObjects;
        private readonly List<Range<double>> ranges;

        public AutoTuneSolver(IEnumerable<T> face3DObjects, IEnumerable<Range<double>> ranges)
        {
            if (face3DObjects != null)
            {
                this.face3DObjects = new List<T>(face3DObjects);
            }

            if (ranges != null)
            {
                this.ranges = new List<Range<double>>(ranges);
            }
        }

        public List<T> Execute(out List<Point3D> nakedPoint3Ds, out AutoTuneReport report)
        {
            nakedPoint3Ds = null;
            report = new AutoTuneReport();

            if (face3DObjects == null || face3DObjects.Count < 2)
            {
                return null;
            }

            List<Range<double>> ranges_Temp = ranges;
            if (ranges_Temp == null || ranges_Temp.Count == 0)
            {
                ranges_Temp = face3DObjects.ElevationRanges(Tolerance_Distance);
            }

            if (ranges_Temp == null || ranges_Temp.Count == 0)
            {
                return null;
            }

            // Seed every panel with a baseline BucketSize / MaxExtend (without overriding any value the
            // caller supplied), then record those baselines so escalation is a clean multiple of them.
            face3DObjects.SetBucketSizes(false);
            face3DObjects.SetMaxExtends(false);

            Dictionary<T, double> baselineBucket = new Dictionary<T, double>();
            Dictionary<T, double> baselineExtend = new Dictionary<T, double>();
            Dictionary<T, int> escalationStep = new Dictionary<T, int>();
            foreach (T face3DObject in face3DObjects)
            {
                baselineBucket[face3DObject] = ReadValue(face3DObject, SolverParameter.BucketSize);
                baselineExtend[face3DObject] = ReadValue(face3DObject, SolverParameter.MaxExtend);
                escalationStep[face3DObject] = -1; // -1 == baseline (no escalation)
            }

            List<double> mergeSlitWidthCandidates = MergeSlitWidthCandidates();
            bool useAutoMergeSlitWidth = mergeSlitWidthCandidates.Count > 0;

            double previousMergeTolerance = SnapSolver.PerpendicularMergeTolerance;
            int warningStartIndex = SnapSolver.SolverWarnings.Count;
            SnapSolver.PerpendicularMergeTolerance = useAutoMergeSlitWidth ? 0 : MergeSlitWidth;
            try
            {
                List<T> bestSolved = Solve(ranges_Temp, out List<Point3D> bestNaked);
                Closure bestClosure = Evaluate(bestSolved, ranges_Temp);

                // The per-panel escalation that produced bestSolved. Neutral rounds keep climbing
                // escalationStep past this without updating bestSolved, so we snapshot the accepted state
                // and restore the panels to it before testing merge widths (which re-solve from the panel
                // parameters) - otherwise auto-merge could return geometry from an unaccepted escalation.
                Dictionary<T, int> bestEscalationStep = new Dictionary<T, int>(escalationStep);

                report.BaselineNakedCount = bestClosure.Dangles;
                report.BaselineClosedLoopCount = bestClosure.Rooms;
                report.Baseline = ClosureReport.Create(bestSolved.Cast<IFace3DObject>(), ranges_Temp, Offset, Tolerance_Angle, Tolerance_Distance);
                report.Add("baseline: " + bestClosure);

                // Current = latest non-regressing solve; escalation climbs from here. We keep ramping
                // MaxExtend on the gap-adjacent panels one ladder step at a time - continuing even
                // through "neutral" rounds that don't close a gap yet - and stop only when a step would
                // over-merge (reduce the room count), the ladder is exhausted, or no dangles remain.
                Closure currentClosure = bestClosure;

                // A panel can only help a dangle if its wall can reach it: that's the snap distance plus
                // how far we may extend a wall (the largest ladder target). Selecting only panels within
                // SnapDistance misses the wall that actually needs to grow across the gap.
                double gapRadius = SnapDistance + (EscalationLadder.Count > 0 ? EscalationLadder.Max() : 0);

                int round = 0;
                if (!EscalateMaxExtend)
                {
                    report.Add("max-extend escalation disabled: keeping default solver reach and leaving remaining naked ends.");
                }

                while (EscalateMaxExtend && currentClosure.Dangles > 0 && round < MaxRounds)
                {
                    round++;

                    // Target the exact dangling-edge endpoints reported by the polygonizer.
                    List<T> gapPanels = GapAdjacentPanels(currentClosure.DangleEnds, gapRadius);
                    if (gapPanels.Count == 0)
                    {
                        report.Add("round " + round + ": no panels adjacent to the remaining dangles - stopping.");
                        break;
                    }

                    // Ramp each gap-adjacent panel by one ladder step, remembering its previous step so
                    // the round can be rolled back atomically if it turns out to over-merge.
                    List<KeyValuePair<T, int>> escalated = new List<KeyValuePair<T, int>>();
                    foreach (T panel in gapPanels)
                    {
                        int step = escalationStep[panel];
                        if (step + 1 >= EscalationLadder.Count)
                        {
                            continue; // already at the top of the ladder
                        }

                        escalated.Add(new KeyValuePair<T, int>(panel, step));
                        ApplyStep(panel, step + 1, baselineBucket, baselineExtend, escalationStep);
                    }

                    if (escalated.Count == 0)
                    {
                        report.Add("round " + round + ": all gap-adjacent panels already at maximum extension - stopping.");
                        break;
                    }

                    List<T> candidateSolved = Solve(ranges_Temp, out List<Point3D> candidateNaked);
                    Closure candidateClosure = Evaluate(candidateSolved, ranges_Temp);

                    if (candidateClosure.Rooms < currentClosure.Rooms)
                    {
                        // Over-merge: the extra reach destroyed a room. Roll this round back and stop.
                        foreach (KeyValuePair<T, int> entry in escalated)
                        {
                            ApplyStep(entry.Key, entry.Value, baselineBucket, baselineExtend, escalationStep);
                        }
                        report.Add("round " + round + ": escalated " + escalated.Count + " panel(s) but room count fell (" + candidateClosure + ") - reverted and stopping.");
                        break;
                    }

                    // Non-regressing: adopt as the new current so the next round climbs further.
                    currentClosure = candidateClosure;

                    if (candidateClosure.Dangles < bestClosure.Dangles || candidateClosure.Rooms > bestClosure.Rooms)
                    {
                        bestSolved = candidateSolved;
                        bestNaked = candidateNaked;
                        bestClosure = candidateClosure;
                        bestEscalationStep = new Dictionary<T, int>(escalationStep);
                        report.Add("round " + round + ": escalated " + escalated.Count + " panel(s), accepted -> " + candidateClosure);
                    }
                    else
                    {
                        report.Add("round " + round + ": escalated " + escalated.Count + " panel(s), no change yet (" + candidateClosure + ") - climbing.");
                    }
                }

                int selectedParallelMergeSkips = SnapSolver.SolverWarnings
                    .Skip(warningStartIndex)
                    .Count(w => w != null && w.StartsWith("Parallel merge skipped"));
                SlitDiagnostics selectedSlitDiagnostics = DetectSlitDiagnostics(bestSolved, ranges_Temp);
                int selectedRemainingSlits = selectedSlitDiagnostics.SlitsOrEmpty().Count;
                double selectedMergeSlitWidth = useAutoMergeSlitWidth ? 0 : MergeSlitWidth;

                if (useAutoMergeSlitWidth)
                {
                    // Restore the panels to the accepted escalation state so the width candidates re-solve
                    // from the same parameters that produced bestSolved, not from a later neutral climb.
                    foreach (KeyValuePair<T, int> entry in bestEscalationStep)
                    {
                        ApplyStep(entry.Key, entry.Value, baselineBucket, baselineExtend, escalationStep);
                    }

                    MergeSelection mergeSelection = SelectMergeSlitWidth(bestSolved, bestNaked, bestClosure, ranges_Temp, mergeSlitWidthCandidates, report);
                    bestSolved = mergeSelection.Solved;
                    bestNaked = mergeSelection.Naked;
                    bestClosure = mergeSelection.Closure;
                    selectedMergeSlitWidth = mergeSelection.Width;
                    selectedRemainingSlits = mergeSelection.RemainingSlits;
                    selectedParallelMergeSkips = mergeSelection.ParallelMergeSkips;
                    selectedSlitDiagnostics = mergeSelection.SlitDiagnostics;
                }

                // Transparency: echo the settings actually in effect (so a stale plug-in or an
                // unconnected input is obvious) and report the remaining double-wall slits.
                report.Rounds = round;
                report.FinalNakedCount = bestClosure.Dangles;
                report.FinalClosedLoopCount = bestClosure.Rooms;
                report.EscalatedPanelCount = escalationStep.Count(x => x.Value >= 0);
                report.MergeSlitWidth = selectedMergeSlitWidth;
                report.AutoMergeSlitWidth = useAutoMergeSlitWidth;
                report.MergeSlitWidthCandidates = string.Join("/", mergeSlitWidthCandidates.Select(v => v.ToString("0.###")));
                report.SlitDiagnosticMinGap = SlitDiagnosticMinGap;
                report.SlitDiagnosticMaxGap = SlitDiagnosticMaxGap;
                report.SlitDiagnosticMaxOverlap = SlitDiagnosticMaxOverlap;
                report.EscalateMaxExtend = EscalateMaxExtend;
                report.MaxRounds = MaxRounds;
                report.EscalationLadder = string.Join("/", EscalationLadder.Select(v => v.ToString("0.##")));
                report.RemainingSlits = selectedRemainingSlits;
                report.ParallelMergeSkips = selectedParallelMergeSkips;

                // Feature: promote the closure polygonisation into output geometry - extract the closed
                // rooms of the final tuned result once (scoring above only needed the counts).
                Rooms = ExtractRooms(bestSolved, ranges_Temp);
                Slits = selectedSlitDiagnostics.SlitsOrEmpty();
                SlitPanels = selectedSlitDiagnostics.Panels ?? new List<T>();
                report.Final = ClosureReport.Create(bestSolved.Cast<IFace3DObject>(), ranges_Temp, Offset, Tolerance_Angle, Tolerance_Distance);

                nakedPoint3Ds = bestNaked;
                return bestSolved;
            }
            finally
            {
                SnapSolver.PerpendicularMergeTolerance = previousMergeTolerance;
            }
        }

        private List<double> MergeSlitWidthCandidates()
        {
            if (MergeSlitWidths == null || MergeSlitWidths.Count == 0)
            {
                return new List<double>();
            }

            SortedSet<double> result = new SortedSet<double>();
            result.Add(0);
            foreach (double value in MergeSlitWidths)
            {
                if (!double.IsNaN(value) && value >= 0)
                {
                    result.Add(value);
                }
            }

            return result.ToList();
        }

        private MergeSelection SelectMergeSlitWidth(List<T> tunedSolved, List<Point3D> tunedNaked, Closure tunedClosure, List<Range<double>> ranges_Temp, List<double> candidates, AutoTuneReport report)
        {
            MergeSelection selected = new MergeSelection(
                width: 0,
                solved: tunedSolved,
                naked: tunedNaked,
                closure: tunedClosure,
                slitDiagnostics: DetectSlitDiagnostics(tunedSolved, ranges_Temp),
                parallelMergeSkips: 0);

            report.Add("merge auto-tune: testing widths [" + string.Join("/", candidates.Select(v => v.ToString("0.###"))) + "]");

            foreach (double width in candidates)
            {
                MergeSelection candidate;
                if (width <= 0)
                {
                    candidate = selected;
                }
                else
                {
                    SnapSolver.PerpendicularMergeTolerance = width;
                    int warningStartIndex = SnapSolver.SolverWarnings.Count;
                    List<T> solved = Solve(ranges_Temp, out List<Point3D> naked);
                    Closure closure = Evaluate(solved, ranges_Temp);
                    int parallelMergeSkips = SnapSolver.SolverWarnings
                        .Skip(warningStartIndex)
                        .Count(w => w != null && w.StartsWith("Parallel merge skipped"));

                    candidate = new MergeSelection(
                        width,
                        solved,
                        naked,
                        closure,
                        DetectSlitDiagnostics(solved, ranges_Temp),
                        parallelMergeSkips);
                }

                if (candidate.Closure.Rooms < tunedClosure.Rooms || candidate.Closure.Dangles > tunedClosure.Dangles)
                {
                    report.Add("merge width " + width.ToString("0.###") + ": rejected -> " + candidate.Closure);
                    continue;
                }

                report.Add("merge width " + width.ToString("0.###") + ": candidate -> " + candidate.Closure
                    + ", slits=" + candidate.RemainingSlits
                    + ", panels=" + (candidate.Solved == null ? 0 : candidate.Solved.Count)
                    + ", protectedSkips=" + candidate.ParallelMergeSkips);

                if (BetterMergeSelection(candidate, selected))
                {
                    selected = candidate;
                }
            }

            report.Add("merge auto-tune: selected width " + selected.Width.ToString("0.###")
                + " (slits=" + selected.RemainingSlits
                + ", panels=" + (selected.Solved == null ? 0 : selected.Solved.Count)
                + ", protectedSkips=" + selected.ParallelMergeSkips + ")");

            return selected;
        }

        private static bool BetterMergeSelection(MergeSelection candidate, MergeSelection selected)
        {
            if (candidate.Closure.Rooms != selected.Closure.Rooms)
            {
                return candidate.Closure.Rooms > selected.Closure.Rooms;
            }

            if (candidate.Closure.Dangles != selected.Closure.Dangles)
            {
                return candidate.Closure.Dangles < selected.Closure.Dangles;
            }

            bool candidateHasNoSkips = candidate.ParallelMergeSkips == 0;
            bool selectedHasNoSkips = selected.ParallelMergeSkips == 0;
            if (candidateHasNoSkips != selectedHasNoSkips)
            {
                return candidateHasNoSkips;
            }

            if (candidate.RemainingSlits != selected.RemainingSlits)
            {
                return candidate.RemainingSlits < selected.RemainingSlits;
            }

            int candidatePanels = candidate.Solved == null ? int.MaxValue : candidate.Solved.Count;
            int selectedPanels = selected.Solved == null ? int.MaxValue : selected.Solved.Count;
            if (candidatePanels != selectedPanels)
            {
                return candidatePanels < selectedPanels;
            }

            return candidate.Width < selected.Width;
        }

        /// <summary>
        /// One full solve from the current per-panel parameters, reusing all of <see cref="Solver{T}"/>.
        /// </summary>
        private List<T> Solve(List<Range<double>> ranges_Temp, out List<Point3D> nakedPoint3Ds)
        {
            Solver<T> solver = new Solver<T>(face3DObjects, ranges_Temp)
            {
                Tolerance_Angle = Tolerance_Angle,
                Tolerance_Distance = Tolerance_Distance,
                Tolerance_Arc = Tolerance_Arc,
                MinLength = MinLength,
                SnapDistance = SnapDistance,
                Offset = Offset
            };

            return solver.Execute(out nakedPoint3Ds, BucketBetweenLevels);
        }

        internal void ApplyStep(T panel, int step, Dictionary<T, double> baselineBucket, Dictionary<T, double> baselineExtend, Dictionary<T, int> escalationStep)
        {
            escalationStep[panel] = step;

            // Only MaxExtend is escalated. Empirically, raising it extends a wall to reach the
            // perpendicular wall across a sliver gap (closing naked ends and *increasing* closed
            // loops), whereas raising BucketSize on this solver re-buckets parallel walls and tends
            // to create more naked ends - so BucketSize is left at its baseline.
            double baseline = baselineExtend[panel];

            if (step < 0)
            {
                // Restore the wall's seeded baseline reach. Zero (and any non-positive value) is a valid
                // baseline - a caller can seed MaxExtend = 0 to stop a wall extending - so it must be
                // restored on rollback just like a positive one; only an absent (NaN) baseline is skipped.
                if (!double.IsNaN(baseline))
                {
                    panel.SetValue(SolverParameter.MaxExtend, baseline);
                }
                return;
            }

            // Absolute target, but never reduce a wall that already reaches further than the target.
            double target = EscalationLadder[step];
            double value = double.IsNaN(baseline) ? target : System.Math.Max(baseline, target);
            panel.SetValue(SolverParameter.MaxExtend, value);
        }

        private double ReadValue(T panel, SolverParameter solverParameter)
        {
            if (panel.TryGetValue(solverParameter, out double value) && !double.IsNaN(value))
            {
                return value;
            }
            return double.NaN;
        }

        /// <summary>
        /// Source panels whose footprint sits within <paramref name="radius"/> of a naked end on the same level.
        /// The proximity test is in plan (X/Y), with Z used only to keep escalation on the affected floor.
        /// </summary>
        private List<T> GapAdjacentPanels(List<Point3D> nakedPoint3Ds, double radius)
        {
            List<T> result = new List<T>();
            if (nakedPoint3Ds == null || nakedPoint3Ds.Count == 0)
            {
                return result;
            }

            HashSet<T> seen = new HashSet<T>();
            foreach (T panel in face3DObjects)
            {
                BoundingBox3D boundingBox3D = panel?.Face3D?.GetBoundingBox();
                if (boundingBox3D == null)
                {
                    continue;
                }

                foreach (Point3D nakedPoint3D in nakedPoint3Ds)
                {
                    if (nakedPoint3D == null)
                    {
                        continue;
                    }

                    double dx = AxisGap(nakedPoint3D.X, boundingBox3D.Min.X, boundingBox3D.Max.X);
                    double dy = AxisGap(nakedPoint3D.Y, boundingBox3D.Min.Y, boundingBox3D.Max.Y);
                    double dz = AxisGap(nakedPoint3D.Z, boundingBox3D.Min.Z, boundingBox3D.Max.Z);
                    if (dz > Tolerance_Distance)
                    {
                        continue;
                    }

                    if (System.Math.Sqrt((dx * dx) + (dy * dy)) <= radius)
                    {
                        if (seen.Add(panel))
                        {
                            result.Add(panel);
                        }
                        break;
                    }
                }
            }

            return result;
        }

        private static double AxisGap(double value, double min, double max)
        {
            if (value < min)
            {
                return min - value;
            }
            if (value > max)
            {
                return value - max;
            }
            return 0;
        }

        /// <summary>
        /// Closure of a solved result via <see cref="SAM.Geometry.Solver.ClosureSolver"/>: each level's
        /// wall axes are snap-rounded, noded and polygonised into rooms (sliver-filtered) and dangling
        /// edges (the naked ends, with their endpoints for targeting). Best-effort per level: a level NTS
        /// can't process contributes nothing.
        /// </summary>
        private Closure Evaluate(List<T> solved, List<Range<double>> ranges_Temp)
        {
            List<Point3D> dangleEnds = new List<Point3D>();
            int rooms = 0;
            int dangles = 0;

            if (solved == null || solved.Count == 0)
            {
                return new Closure(0, 0, dangleEnds);
            }

            foreach (Range<double> range in ranges_Temp)
            {
                try
                {
                    List<Segment2D> segment2Ds = LevelSegments(solved, range, out Plane plane);
                    double elevation = plane?.Origin?.Z ?? range.Min;

                    SAM.Geometry.Solver.ClosureSolver.Result closure = SAM.Geometry.Solver.ClosureSolver.Polygonize(segment2Ds);
                    rooms += closure.RoomCount;
                    dangles += closure.DangleCount;
                    foreach (Point2D dangleEnd in closure.DangleEnds)
                    {
                        dangleEnds.Add(new Point3D(dangleEnd.X, dangleEnd.Y, elevation));
                    }
                }
                catch
                {
                    // Best-effort: a level NTS can't process contributes nothing to the score.
                }
            }

            return new Closure(rooms, dangles, dangleEnds);
        }

        /// <summary>
        /// Closed room polygons of a solved result, one <see cref="Face3D"/> per room laid on its level's
        /// section plane. Uses the same <see cref="SAM.Geometry.Solver.ClosureSolver"/> polygonisation as
        /// the scorer, so the room count matches the reported closed-loop count. Best-effort per level.
        /// </summary>
        private List<Face3D> ExtractRooms(List<T> solved, List<Range<double>> ranges_Temp)
        {
            List<Face3D> result = new List<Face3D>();
            if (solved == null || solved.Count == 0)
            {
                return result;
            }

            foreach (Range<double> range in ranges_Temp)
            {
                try
                {
                    List<Segment2D> segment2Ds = LevelSegments(solved, range, out Plane plane);
                    if (plane == null)
                    {
                        continue;
                    }

                    SAM.Geometry.Solver.ClosureSolver.Result closure = SAM.Geometry.Solver.ClosureSolver.Polygonize(segment2Ds);
                    foreach (Polygon2D room in closure.Rooms)
                    {
                        if (room != null)
                        {
                            result.Add(new Face3D(plane, room));
                        }
                    }
                }
                catch
                {
                    // Best-effort: a level NTS can't process contributes no rooms.
                }
            }

            return result;
        }

        /// <summary>
        /// Section <paramref name="solved"/> at the given level's section plane (range.Min + Offset) and
        /// collect the resulting 2D wall-axis segments. The section <paramref name="plane"/> is returned so
        /// callers can lift the plan geometry back to 3D.
        /// </summary>
        private List<Segment2D> LevelSegments(List<T> solved, Range<double> range, out Plane plane)
        {
            plane = SAM.Geometry.Spatial.Create.Plane(range.Min + Offset);

            List<Segment2D> segment2Ds = new List<Segment2D>();
            Dictionary<T, List<ISegmentable2D>> dictionary = solved.SectionDictionary<T, ISegmentable2D>(plane, Tolerance_Angle, Tolerance_Distance);
            if (dictionary == null)
            {
                return segment2Ds;
            }

            foreach (List<ISegmentable2D> segmentable2Ds in dictionary.Values)
            {
                if (segmentable2Ds == null)
                {
                    continue;
                }

                foreach (ISegmentable2D segmentable2D in segmentable2Ds)
                {
                    List<Segment2D> segments = segmentable2D?.GetSegments();
                    if (segments != null)
                    {
                        segment2Ds.AddRange(segments);
                    }
                }
            }

            return segment2Ds;
        }

        private List<LevelSegment> LevelSegmentSources(List<T> solved, Range<double> range, out Plane plane)
        {
            plane = SAM.Geometry.Spatial.Create.Plane(range.Min + Offset);

            List<LevelSegment> result = new List<LevelSegment>();
            Dictionary<T, List<ISegmentable2D>> dictionary = solved.SectionDictionary<T, ISegmentable2D>(plane, Tolerance_Angle, Tolerance_Distance);
            if (dictionary == null)
            {
                return result;
            }

            foreach (KeyValuePair<T, List<ISegmentable2D>> keyValuePair in dictionary)
            {
                List<ISegmentable2D> segmentable2Ds = keyValuePair.Value;
                if (segmentable2Ds == null)
                {
                    continue;
                }

                foreach (ISegmentable2D segmentable2D in segmentable2Ds)
                {
                    List<Segment2D> segments = segmentable2D?.GetSegments();
                    if (segments == null)
                    {
                        continue;
                    }

                    foreach (Segment2D segment in segments)
                    {
                        result.Add(new LevelSegment(segment, keyValuePair.Key));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Count remaining parallel "double-wall" slits in a solved result: pairs of near-parallel,
        /// overlapping wall axes separated by 0.02-0.5 m. Diagnostic only (for the report).
        /// </summary>
        private int CountSlits(List<T> solved, List<Range<double>> ranges_Temp)
        {
            return DetectSlitDiagnostics(solved, ranges_Temp).SlitsOrEmpty().Count;
        }

        /// <summary>
        /// Return diagnostic connector segments for remaining parallel "double-wall" slits.
        /// </summary>
        private List<Segment3D> DetectSlits(List<T> solved, List<Range<double>> ranges_Temp)
        {
            return DetectSlitDiagnostics(solved, ranges_Temp).SlitsOrEmpty();
        }

        /// <summary>
        /// Return diagnostic connector segments and the panels whose section axes generated them.
        /// </summary>
        private SlitDiagnostics DetectSlitDiagnostics(List<T> solved, List<Range<double>> ranges_Temp)
        {
            if (solved == null || solved.Count == 0)
            {
                return new SlitDiagnostics();
            }

            // A slit pair is within MaxSlitGap perpendicular distance, so an STRtree of segment boxes
            // grown by that gap returns a strict superset of the pairs the former all-pairs scan tested -
            // the count is unchanged while the per-level cost drops from O(n^2) toward O(n log n).
            const double MinSlitOverlap = 0.05;
            const double ParallelDot = 0.99;
            double minSlitGap = System.Math.Max(0, SlitDiagnosticMinGap);
            double maxSlitGap = SlitDiagnosticMaxGap > minSlitGap ? SlitDiagnosticMaxGap : 0.5;

            SlitDiagnostics result = new SlitDiagnostics
            {
                Slits = new List<Segment3D>(),
                Panels = new List<T>()
            };
            foreach (Range<double> range in ranges_Temp)
            {
                try
                {
                    List<LevelSegment> segments = LevelSegmentSources(solved, range, out Plane plane);
                    if (segments.Count < 2)
                    {
                        continue;
                    }

                    double elevation = plane?.Origin?.Z ?? range.Min;

                    NetTopologySuite.Index.Strtree.STRtree<int> index = new NetTopologySuite.Index.Strtree.STRtree<int>();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        index.Insert(SegmentEnvelope(segments[i].Segment, 0), i);
                    }

                    for (int i = 0; i < segments.Count; i++)
                    {
                        Vector2D ui = segments[i].Segment.Direction.Unit;
                        foreach (int j in index.Query(SegmentEnvelope(segments[i].Segment, maxSlitGap)))
                        {
                            if (j <= i)
                            {
                                continue; // count each pair once
                            }

                            Vector2D uj = segments[j].Segment.Direction.Unit;
                            if (System.Math.Abs((ui.X * uj.X) + (ui.Y * uj.Y)) < ParallelDot)
                            {
                                continue;
                            }

                            if (TryCreateSlitMarker(segments[i].Segment, segments[j].Segment, elevation, minSlitGap, maxSlitGap, MinSlitOverlap, out Segment3D slit))
                            {
                                result.Slits.Add(slit);
                                result.AddPanel(segments[i].Source);
                                result.AddPanel(segments[j].Source);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            return result;
        }

        private bool TryCreateSlitMarker(Segment2D reference, Segment2D other, double elevation, double minGap, double maxGap, double minOverlap, out Segment3D slit)
        {
            slit = null;

            double startParameter = reference.ClosestParameter(other.Start);
            double endParameter = reference.ClosestParameter(other.End);
            double overlapMin = System.Math.Max(0, System.Math.Min(startParameter, endParameter));
            double overlapMax = System.Math.Min(1, System.Math.Max(startParameter, endParameter));
            if (overlapMax <= overlapMin)
            {
                return false;
            }

            double overlapLength = (overlapMax - overlapMin) * reference.GetLength();
            if (overlapLength < minOverlap)
            {
                return false;
            }

            if (SlitDiagnosticMaxOverlap > 0 && overlapLength > SlitDiagnosticMaxOverlap)
            {
                return false;
            }

            double parameter = (overlapMin + overlapMax) / 2;
            Point2D referencePoint = reference.GetPoint(parameter);
            double otherParameter = other.ClosestParameter(referencePoint).Clamp(0, 1);
            Point2D otherPoint = other.GetPoint(otherParameter);

            double gap = referencePoint.Distance(otherPoint);
            if (gap <= minGap || gap > maxGap)
            {
                return false;
            }

            slit = new Segment3D(
                new Point3D(referencePoint.X, referencePoint.Y, elevation),
                new Point3D(otherPoint.X, otherPoint.Y, elevation));
            return true;
        }

        /// <summary>
        /// Axis-aligned bounding box of a segment, optionally grown by <paramref name="expansion"/>.
        /// </summary>
        private static NetTopologySuite.Geometries.Envelope SegmentEnvelope(Segment2D segment, double expansion)
        {
            Point2D start = segment.Start;
            Point2D end = segment.End;
            NetTopologySuite.Geometries.Envelope envelope = new NetTopologySuite.Geometries.Envelope(
                System.Math.Min(start.X, end.X), System.Math.Max(start.X, end.X),
                System.Math.Min(start.Y, end.Y), System.Math.Max(start.Y, end.Y));
            if (expansion > 0)
            {
                envelope.ExpandBy(expansion);
            }
            return envelope;
        }

        /// <summary>
        /// Closure objective: maximise real rooms, then minimise dangles (naked ends). Carries the
        /// dangle endpoints so the next round can target exactly the walls that need to grow.
        /// </summary>
        private struct Closure
        {
            public int Rooms;
            public int Dangles;
            public List<Point3D> DangleEnds;

            public Closure(int rooms, int dangles, List<Point3D> dangleEnds)
            {
                Rooms = rooms;
                Dangles = dangles;
                DangleEnds = dangleEnds;
            }

            public override string ToString()
            {
                return "rooms=" + Rooms + ", naked=" + Dangles;
            }
        }

        private struct MergeSelection
        {
            public double Width;
            public List<T> Solved;
            public List<Point3D> Naked;
            public Closure Closure;
            public int RemainingSlits;
            public SlitDiagnostics SlitDiagnostics;
            public int ParallelMergeSkips;

            public MergeSelection(double width, List<T> solved, List<Point3D> naked, Closure closure, SlitDiagnostics slitDiagnostics, int parallelMergeSkips)
            {
                Width = width;
                Solved = solved;
                Naked = naked;
                Closure = closure;
                SlitDiagnostics = slitDiagnostics;
                RemainingSlits = SlitDiagnostics.Slits == null ? 0 : SlitDiagnostics.Slits.Count;
                ParallelMergeSkips = parallelMergeSkips;
            }
        }

        private struct LevelSegment
        {
            public Segment2D Segment;
            public T Source;

            public LevelSegment(Segment2D segment, T source)
            {
                Segment = segment;
                Source = source;
            }
        }

        private struct SlitDiagnostics
        {
            public List<Segment3D> Slits;
            public List<T> Panels;

            public void AddPanel(T panel)
            {
                if (panel == null)
                {
                    return;
                }

                if (Panels == null)
                {
                    Panels = new List<T>();
                }

                if (!Panels.Any(x => object.ReferenceEquals(x, panel)))
                {
                    Panels.Add(panel);
                }
            }

            public List<Segment3D> SlitsOrEmpty()
            {
                return Slits ?? new List<Segment3D>();
            }
        }
    }

    /// <summary>
    /// Human-readable trace of an <see cref="AutoTuneSolver{T}"/> run, surfaced for transparency so a
    /// user can sanity-check what the tuner did and which panels it escalated.
    /// </summary>
    public class AutoTuneReport
    {
        public int Rounds { get; set; }
        public int BaselineNakedCount { get; set; }
        public int FinalNakedCount { get; set; }
        public int BaselineClosedLoopCount { get; set; }
        public int FinalClosedLoopCount { get; set; }
        public int EscalatedPanelCount { get; set; }

        /// <summary>
        /// Per-level closure of the baseline (standard) solve and the final tuned solve, scored with the
        /// same metric. Enables a level-by-level before/after comparison in the report.
        /// </summary>
        public ClosureReport Baseline { get; set; }
        public ClosureReport Final { get; set; }

        // Settings actually in effect (echoed so a stale plug-in or unconnected input is obvious).
        public double MergeSlitWidth { get; set; }
        public bool AutoMergeSlitWidth { get; set; }
        public string MergeSlitWidthCandidates { get; set; } = "";
        public double SlitDiagnosticMinGap { get; set; }
        public double SlitDiagnosticMaxGap { get; set; }
        public double SlitDiagnosticMaxOverlap { get; set; }
        public bool EscalateMaxExtend { get; set; }
        public int MaxRounds { get; set; }
        public string EscalationLadder { get; set; } = "";

        // Slit-merge action summary.
        public int RemainingSlits { get; set; }
        public int ParallelMergeSkips { get; set; }

        public List<string> Log { get; } = new List<string>();

        public void Add(string message)
        {
            Log.Add(message);
        }

        public override string ToString()
        {
            List<string> lines = new List<string>
            {
                "AutoTune v2: " + Rounds + " round(s)   (baseline = standard solver, final = autotuned)",
                "settings: mergeSlitWidth=" + MergeSlitWidth.ToString("0.###") + (AutoMergeSlitWidth ? (" selected from [" + MergeSlitWidthCandidates + "]") : "") + ", slitGap=[" + SlitDiagnosticMinGap.ToString("0.###") + "/" + SlitDiagnosticMaxGap.ToString("0.###") + "], slitMaxOverlap=" + SlitDiagnosticMaxOverlap.ToString("0.###") + ", escalateMaxExtend=" + EscalateMaxExtend + ", maxRounds=" + MaxRounds + ", ladder=[" + EscalationLadder + "]",
                "naked ends: " + BaselineNakedCount + " -> " + FinalNakedCount,
                "closed rooms: " + BaselineClosedLoopCount + " -> " + FinalClosedLoopCount
            };

            // Enclosed area before/after, when the per-level reports were captured.
            if (Baseline != null && Final != null)
            {
                lines.Add("enclosed area (m2): " + Baseline.TotalRoomArea.ToString("0.##") + " -> " + Final.TotalRoomArea.ToString("0.##"));
            }

            lines.Add("panels extended: " + EscalatedPanelCount);
            lines.Add("double-wall slits remaining: " + RemainingSlits + (MergeSlitWidth > 0 ? ("  (merge skipped " + ParallelMergeSkips + " group(s) to protect rooms > " + MergeSlitWidth.ToString("0.###") + " m)") : "  (merge OFF - set mergeSlitWidth_ > 0 to collapse them)"));

            // Per-level before/after breakdown, aligning the baseline and final reports by level index.
            if (Baseline != null && Final != null && (Baseline.LevelCount > 0 || Final.LevelCount > 0))
            {
                lines.Add("per level (rooms / area m2 / naked ends, baseline -> final):");
                int levelCount = System.Math.Max(Baseline.LevelCount, Final.LevelCount);
                for (int i = 0; i < levelCount; i++)
                {
                    ClosureReport.Level before = i < Baseline.LevelCount ? Baseline.Levels[i] : null;
                    ClosureReport.Level after = i < Final.LevelCount ? Final.Levels[i] : null;
                    double elevation = after?.Elevation ?? before?.Elevation ?? 0;

                    string roomsText = (before?.Rooms ?? 0) + " -> " + (after?.Rooms ?? 0);
                    string areaText = (before?.RoomArea ?? 0).ToString("0.##") + " -> " + (after?.RoomArea ?? 0).ToString("0.##");
                    string nakedText = (before?.NakedEnds ?? 0) + " -> " + (after?.NakedEnds ?? 0);
                    lines.Add("  " + elevation.ToString("0.###") + " m: " + roomsText + " / " + areaText + " / " + nakedText);
                }
            }

            lines.AddRange(Log);
            return string.Join(System.Environment.NewLine, lines);
        }
    }
}
