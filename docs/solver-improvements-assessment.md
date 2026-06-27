# SAM_Solver Capability Improvement Assessment

This document records an assessment of high-value improvements to the SAM_Solver
geometric/topological solver, and the changes implemented in response. Claims below cite
the concrete type, method, and constant they refer to so they can be verified against the
source.

## Background

`SAM_Solver` prepares analytical BIM geometry for downstream simulation. It snaps,
extends, trims, merges, and resolves wall geometry per elevation level so the resulting
model is closed and analysis-ready. The core lives in `SAM.Geometry.Solver`
([`SnapSolver`](SAM_Solver/SAM.Geometry.Solver/Classes/SnapSolver.cs),
[`SnappedWall`](SAM_Solver/SAM.Geometry.Solver/Classes/SnappedWall.cs),
[`ExtensionSolver`](SAM_Solver/SAM.Geometry.Solver/Classes/ExtensionSolver.cs),
[`GraphSolver`](SAM_Solver/SAM.Geometry.Solver/Classes/GraphSolver.cs)) with a high-level
entry point [`Solver<T>.Execute`](SAM_Solver/SAM.Analytical.Solver/Classes/Solver.cs) in
`SAM.Analytical.Solver`.

## Implemented Improvements

### 1. Automated correctness and regression testing

The repository previously had no automated tests; CI only compiled the solution and
correctness was validated manually. A
[`SAM.Solver.Tests`](SAM_Solver/SAM.Solver.Tests/SAM.Solver.Tests.csproj) xUnit project
(`net8.0`; xunit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1)
now covers the self-contained solver surfaces — **5 test classes / 22 facts**:

- [`GraphSolverTests`](SAM_Solver/SAM.Solver.Tests/GraphSolverTests.cs) (4) — exact
  duplicates collapse to one edge keeping both sources; near-coincident endpoints within
  tolerance still collapse; far-apart segments stay separate; sub-tolerance degenerate
  edge is dropped.
- [`ExtensionSolverTests`](SAM_Solver/SAM.Solver.Tests/ExtensionSolverTests.cs) (2) — one
  valid segment returned per input, in order; a single segment returns with finite,
  non-zero length.
- [`ClosureSolverTests`](SAM_Solver/SAM.Solver.Tests/ClosureSolverTests.cs) (9) — closed
  square = 1 room / 0 dangles; protruding tail = 1 room + reported dangle; sub-0.2 m
  sliver loop is not a room; fewer than three segments cannot enclose; null input returns
  empty without throwing; crossing segments each split in two; T-junction splits only the
  through-segment; non-touching segments stay whole; empty input returns empty.
- [`ClosureTests`](SAM_Solver/SAM.Solver.Tests/ClosureTests.cs) (4) — unit square = 1 loop
  of area 1; open three-sided path is not closed; tiny sub-min square ignored; thin slot
  (one side below min) ignored.
- [`SnapSolverTests`](SAM_Solver/SAM.Solver.Tests/SnapSolverTests.cs) (1) — a clean closed
  room solves end-to-end into wall faces with no naked ends.

CI runs these on every PR. [`.github/workflows/build.yml`](.github/workflows/build.yml)
(runner `windows-2022`, .NET 8; triggers push/PR on `master`, `main`, `sow/**`, plus
`workflow_dispatch`) builds dependencies (`SAM` → `SAM_Solver`) and then runs the
**"Run solver unit tests"** step:
`dotnet test SAM_Solver\SAM.Solver.Tests\SAM.Solver.Tests.csproj -c Release --logger "trx;LogFileName=solver-tests.trx"`.

**Coverage gaps (known).** Tests target the self-contained surfaces only. Not yet
covered: the multi-level `SnapSolver.Execute` pipeline beyond the single-room smoke test
(plane sectioning, multi-level aggregation), `SnappedWall` field behaviour (elevation /
weight / bucket size / naked-end flags), and the `Query` utilities (`ClosestParameter`, `Intersecting`,
`MinimumDistanceTo`, `Snap`). All fixtures are programmatic; there is no real-model
regression corpus yet.

### 2. Spatial indexing to remove O(n^2) scans

The static per-pass scans `MarkNakedNodes`, `ExplodeWallsAtIntersections`, and
`SnapOpenNodes` previously compared every wall against every other. They now pre-filter
candidates through a bounding-box index built by `SnapSolver.BuildAxisIndex`
(`NetTopologySuite.Index.Strtree.STRtree<int>` of axis envelopes). Each pass queries an
envelope expanded by tolerance, which is a strict superset of the pairs the original loops
examined, so behaviour is preserved while average cost moves toward O(n log n). Likewise
`GraphSolver` replaced its all-pairs node lookup with a uniform spatial hash (`NodeGrid`,
cell size `max(tolerance, 1e-3 m)`, 3×3 neighbour scan); `FindCoincidentNode` returns the
lowest-index coincident node so merges stay deterministic.

**Deliberate exception.** `SnapAndAdjustWalls` is intentionally left as an all-pairs scan
(see the comment at `SnapSolver.cs`): the `current` wall absorbs candidates and grows
during the pass, so a precomputed box filter could miss walls that only come into reach
after a merge. It is made safe by a bound (`MAX_SNAP_ADJUST_ITERATIONS`, see §3) rather
than by indexing. The per-level merges `MergeColinearWalls` and `MergeParallelOneLevel`
are also still all-pairs by design and are the most natural next indexing candidates,
behind golden-output tests.

### 3. Convergence and numerical hardening

Several hazards could hang or silently corrupt a solve: unbounded loops, null/NaN
geometry fallbacks, and duplicated magic tolerances. These are now addressed concretely:

- **Iteration bounds.** `SnapAndAdjustWalls` is capped by
  `MAX_SNAP_ADJUST_ITERATIONS = 1000`; `ExtensionSolver.Solve` by `safetyCounter < 10000`;
  `MergeColinearWalls` by `safetyCounter < 1000`.
- **Diagnostics.** A static `SnapSolver.SolverWarnings` list records non-fatal events —
  a skipped parallel merge and the snap-adjust cap. The
  `SnappedWall` NaN/null path no longer fails silently: a null or `IsNaN` snapped endpoint
  falls back to the original `ProjectedAxis` and pushes a `SolverWarnings` entry tagged
  with the source panel index.
- **Named constants.** Magic numbers were extracted into named values —
  `SnappedWall.ExtensionLimitLengthRatio = 0.49`,
  `SnappedWall.OpenNodeSnapAngleRangeRad = 2π/3` (120°), the `SetMaxExtends` factors, plus
  the `SnapSolver` closure constants (`AreaTolerance = 1e-3 m²`,
  `MinLoopRectangleSide = 0.2 m`). The solver seeds its working tolerances from
  `SAM.Core.Tolerance.Distance` / `.Angle`, clamped to a `1e-9` floor, with
  `ModelTolerance` fixed at `0.001 m` for Rhino geometry operations.

### 4. Closed-space extraction

[`ClosureSolver`](SAM_Solver/SAM.Geometry.Solver/Classes/ClosureSolver.cs) consolidates the
snap-round → node → polygonize pipeline used to score closure: it nodes segments with
`NetTopologySuite`'s `SnapRoundingNoder` on a `PrecisionModel(1.0 / gridSize)`
(`DefaultGridSize = 0.01 m`), runs them through `Polygonizer`, and discards loops whose
bounding-box minimum side is below the sliver threshold (`0.2 m`).
[`AutoTuneSolver<T>`](SAM_Solver/SAM.Analytical.Solver/Classes/AutoTuneSolver.cs) uses it to
count rooms and dangles and to drive gap-driven tuning, and exposes the recognised rooms
as `Face3D` through the AutoTuneSolver Grasshopper component's `rooms` output. See
[`docs/autotune-solver-component.md`](docs/autotune-solver-component.md) and
[`docs/parameter-tuning-design.md`](docs/parameter-tuning-design.md).

## NTS Features Reviewed

### 1. Robust noding via snap-rounding — skipped

`SnapSolver.ExplodeWallsAtIntersections` still uses the default pairwise
extend-and-intersect splitter. An opt-in `SnapRoundingNoder` branch was tested and then
removed after real-model validation on the 2026-06-17 solver input: with the tested
precision grid it left naked ends that the default path closed. `ClosureSolver.NodeSegments`
remains available for closure scoring and future experiments, but robust noding is not
wired into `SnapSolver` or exposed on Grasshopper components.

### 2. Edge dissolve — future

`NetTopologySuite.Operation.Linemerge.LineMerger` could replace parts of
`SnapSolver.MergeColinearWalls`, which currently rebuilds maximal walls with a recursive
anchor-walking loop. Any adoption needs golden-output tests because the merge behaviour is
sensitive to source-panel grouping.

### 3. Prepared predicates and indexed distance — future

`PreparedGeometryFactory` and `IndexedFacetDistance` could replace repeated predicate and
closest-point tests in naked-node marking, open-node snapping, and bucket distance checks.

### 4. Unified precision — future

`GeometryPrecisionReducer` plus a consistent `PrecisionModel` could reduce tolerance drift
across passes. This should be tested behind golden-output regression cases before changing
the main pipeline.

## Adoption Guidance

The current safest sequence is: keep `ClosureSolver` for scoring and room extraction, grow
real-model regression coverage (closing the §1 gaps and adding a real-model corpus), then
index the remaining all-pairs merge passes (`MergeColinearWalls`,
`MergeParallelOneLevel`) and revisit precision reduction or line merging — each behind
golden-output tests. Robust noding should stay skipped until a real model demonstrates a
clear closure improvement over the default splitter.
