# SAM_Solver — Capability Improvement Assessment

This document records an assessment of the highest-value improvements to the
SAM_Solver geometric/topological solver, and the changes implemented in response.

## Background

`SAM_Solver` prepares analytical BIM geometry for downstream simulation: it snaps,
extends/trims, merges, and resolves wall geometry per elevation level so that the
resulting model is closed and analysis-ready. The core lives in
`SAM.Geometry.Solver` (`SnapSolver`, `SnappedWall`, `ExtensionSolver`, `GraphSolver`)
with a high-level entry point in `SAM.Analytical.Solver` (`Solver<T>.Execute`).

## Top 3 improvements

### 1. Automated correctness & regression testing (foundation)

The repository previously had no automated tests; CI only compiled the solution and
correctness was validated manually via user-submitted Rhino/Revit files. For a
geometry solver where small tolerance or ordering changes can silently corrupt output,
this left every refactor unguarded.

**Implemented:** a `SAM.Solver.Tests` xUnit project covering the self-contained solver
surfaces (`GraphSolver`, `ExtensionSolver`), wired into CI via a `dotnet test` step so
regressions surface on every PR.

### 2. Spatial indexing to remove O(n²) scans (scalability)

The hot paths (`MarkNakedNodes`, `ExplodeWallsAtIntersections`, `SnapOpenNodes`, the
inner pass of `SnapAndAdjustWalls`, and `GraphSolver` node lookup) were all-pairs scans
with no spatial acceleration, so cost grew quadratically with wall count.

**Implemented:** candidate pre-filtering via bounding-box spatial indexes
(`NetTopologySuite.STRtree` for the static per-pass scans, a tolerance cell-grid for the
incremental `GraphSolver` node merge). The bounding-box filter is a strict superset of
the pairs the original loops examined, so results are unchanged while the average cost
drops toward O(n log n).

### 3. Convergence & numerical-robustness hardening (reliability)

Several hazards could hang or silently corrupt a solve: `SnapAndAdjustWalls` looped
without any iteration bound (risking a host hang on non-converging input), null/NaN
geometry was silently masked with no diagnostic, and tolerances/extension limits were
hardcoded magic numbers duplicated across layers.

**Implemented:** an iteration bound + diagnostic on the previously unbounded loop,
diagnostic capture on the null/NaN fallback path, and extraction of the magic extension
constants into named values aligned with `SAM.Core.Tolerance`.
