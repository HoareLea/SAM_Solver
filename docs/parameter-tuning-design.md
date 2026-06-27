# Design: auto-tuning bucket size & extension (gap-driven local escalation)

**Status:** Phase 1 implemented (opt-in, additive). See `AutoTuneSolver<T>`
(`SAM.Analytical.Solver/Classes/AutoTuneSolver.cs`) and the dedicated Grasshopper
`AutoTuneSolver` component (`SAM.Analytical.Grasshopper.Solver/Component/AutoTuneSolver.cs`).
The standard `Solver` and its component are untouched; the tuner is a separate driver/component so it
can be tested in isolation. The sections below describe the intended approach; the notes inline mark
where the implementation simplifies it.

## Problem

Bucket size, weight, and max-extension are fixed heuristics computed once per panel
(`SAM.Analytical.Solver/Modify/SetBucketSizes.cs` with `factor = 0.6`,
`SetMaxExtends.cs` with `0.33 / 0.5 / 0.6`, `SetWeights.cs`). When a model doesn't fully close with
those values, there is no automated way to find better ones — they are guessed and the solve re-run.

The closure scorer `SnapSolver.ComputeClosure` returns the count of real (non-sliver) closed rooms
and their enclosed area. That objective is exactly the scoring function a parameter tuner needs.

## Goal

Search for better bucket/extension values automatically, guided by the closure objective, **without
over-merging** distinct rooms. The agreed approach is **gap-driven local escalation** — ramp
parameters only where naked ends remain — rather than a brute-force global grid sweep, because it is
cheaper and far less likely to over-merge clean areas. The feature is **opt-in**; default behaviour
is unchanged. Speed is not a constraint.

## Objective function — "minimal sufficient aggressiveness"

Rank candidate solves lexicographically. The third criterion is what stops the tuner from snapping
ever harder (which always looks "better" until distinct rooms merge into one):

1. **maximize** real (non-sliver) closed-loop count;
2. then **minimize** naked-end count;
3. then **minimize** parameter aggressiveness (smallest bucket/extension among ties).

Closed loops + area come from `SnapSolver.ComputeClosure`. Naked-end count is already returned as
`nakedPoint3Ds` by `Solver<T>.Execute` / `Modify.Snap`.

## Approach — gap-driven local escalation

A new opt-in driver wrapping `Solver<T>.Execute` (`SAM.Analytical.Solver/Classes/Solver.cs`):

1. **Baseline solve** with current default parameters; record the score and `nakedPoint3Ds`.
2. **Locate stubborn gaps** — for each naked point, find the output wall(s) whose endpoint sits on it
   (`SnapSolver.SnappedWalls` + `SnappedSources`, the output→source-panel map) and collect the
   **source panels** feeding those walls.
3. **Escalate locally** — increase `BucketSize` and `MaxExtend` (`SolverParameter` via `SetValue`)
   **only on those source panels**, one step up a small factor ladder (e.g. ×1.25, ×1.5, ×2.0),
   leaving every other panel untouched.
4. **Re-solve and re-score** — accept the escalation only if the score improves; otherwise revert
   those panels to their pre-escalation values.
5. **Repeat** until naked ends reach zero, no escalation improves the score, or a max-rounds cap.
6. Return the **best-scoring** result, and surface the chosen parameters for transparency.

Restricting escalation to gap-adjacent panels, and accepting it only on a score improvement, means
clean regions keep their minimal parameters (objective rule 3) — this is what avoids gratuitous
over-merge.

## Reuse (existing code)

- Scoring: `SnapSolver.ComputeClosure` (`SAM.Geometry.Solver/Classes/SnapSolver.cs`).
- Per-panel parameter read/write: `SolverParameter` + `IParameterizedSAMObject.SetValue/TryGetValue`,
  as in `SetBucketSizes.cs` / `SetMaxExtends.cs` / `SetWeights.cs`.
- Solve + naked/source outputs: `Modify.Snap` (`Modify/Snap.cs`) and `Geometry.Solver.Query.Snap`
  (`SAM.Geometry.Solver/Query/Snap.cs`) — expose `SnappedWalls`, `SnappedSources`, `NakedEnds`.
- Entry point to wrap: `Solver<T>.Execute` (`SAM.Analytical.Solver/Classes/Solver.cs`).

## Opt-in

A separate `AutoTuneSolver<T>` wraps `Solver<T>.Execute` rather than adding a branch inside it, and a
dedicated Grasshopper component exposes it. The standard `Solver` and its component are untouched, so
existing graphs behave exactly as today.

## Implementation notes (Phase 1, as built)

- **Driver, not a flag in `Solver.cs`.** To keep the standard solve path untouched, the escalation
  lives in a separate `AutoTuneSolver<T>` that *wraps* `Solver<T>.Execute` rather than an `AutoTune`
  branch inside it. A dedicated Grasshopper component exposes it, so existing graphs are unaffected.
- **Gap adjacency** is approximated by plan (X/Y) proximity of each source panel's bounding box to a
  naked end (naked ends are reported on WorldXY). The radius is `nakedNodeSnapDistance + max ladder
  target`, not just the snap distance: the wall that must grow across a gap can start 1-2 m away, so a
  snap-distance-only radius misses it (verified on a real model - radius 0.5 m left the gap, ~2 m closed
  it). A future pass can use the exact `SnappedSources` map for tighter scoping.
- **Lever = MaxExtend, ramped to absolute metre targets** (`{0.5, 0.75, 1.0, 1.5}`), not BucketSize.
  Empirically (1320-panel, 10-storey model with two ~0.12 m sliver gaps): raising MaxExtend extends a
  wall to reach the perpendicular wall and closed both gaps (naked 2 -> 0) while *increasing* closed
  loops (220 -> 222); raising BucketSize - per-panel or the global `bucketBetweenLevels` - left the gaps
  open or created many more naked ends. Targets are absolute because `SetMaxExtends` caps the per-wall
  baseline at ~0.49 x length, so a short stub next to a gap can't reach across with a multiplier.
- **The escalation climbs through "neutral" rounds.** A step that neither closes a gap nor over-merges
  is kept and the next step goes higher, until naked = 0, the ladder is exhausted, or a step reduces the
  closed-loop count (over-merge) - which is rolled back and stops the climb.
- **Double-wall slit merge (opt-in, `mergeSlitWidth_`).** Closing naked ends with MaxExtend does not
  remove *parallel* "double-wall" slits - two near-parallel wall lines a sliver apart whose endpoints
  both connect (so they are not naked). `SnapSolver.PerpendicularMergeTolerance` (driven by the tuner's
  `MergeSlitWidth` / the component's `_mergeSlitWidth_`) runs a final pass that merges near-parallel,
  overlapping axes within that perpendicular distance onto one length-weighted axis. It is
  closure-guarded per level: a level whose merge would drop the count or area of rooms *wider than the
  merge width* is left unmerged, so any room wider than the chosen width is never erased. (A genuine
  space narrower than the width is bounded by the same parallel wall pair as a slit and cannot be told
  apart, so keep the width below the narrowest real room/corridor.) On the real model the merge cleanly
  removes ~100 of 189 slits at 0.20 m while keeping all 222 rooms; at 0.25 m the guard skips the
  room-destroying merges (rooms stay 222, fewer slits removed) rather than erasing real ~0.2 m spaces.
  0 = off (default).
- **Closure scoring + targeting use NetTopologySuite.** Each level's wall axes are snap-rounded
  (`SnapRoundingNoder`, ~0.01 m grid), noded and polygonised (`Polygonizer`): `GetPolygons` gives the
  rooms (sliver-filtered) and `GetDangles` gives the dangling edges - the naked ends - with their exact
  endpoints, which the next round targets directly (instead of `SnapSolver.ComputeClosure` + a
  bounding-box guess). Snap-rounding alone does **not** close these gaps (they are dangling extension
  gaps, not vertex-coincidence slivers), so MaxExtend remains the closer; NTS just provides the robust
  metric and precise targeting. Verified on the real model: naked 2 -> 0, rooms 220 -> 222.
- **Scoring** maximises real closed loops (via `ComputeClosure`, sectioning the solved panels per level)
  then minimises naked-end count. Closed-loop scoring is best-effort (any geometry hiccup degrades to the
  naked-end criterion). "Minimal aggressiveness among ties" is implicit: an escalation round is accepted
  only on a strict score improvement, so clean regions are never escalated.
- **Termination:** naked ends reach zero, the ladder is exhausted on all gap panels, a full gap round
  fails to improve the score, or `MaxRounds` is hit.
- **Tests** for the tuner are deferred: a meaningful test needs real `Panel` geometry and the sibling
  SAM analytical DLLs (the current `SAM.Solver.Tests` references only Core/Geometry). The closure
  objective it scores on is already covered by `ClosureTests`.

## Phasing

- **Phase 1:** analytical-level scoring helper (closed loops + naked count of a solved result,
  reusing `ComputeClosure`); the gap-driven escalation driver; opt-in branch in `Solver.cs`;
  tests in `SAM.Solver.Tests`.
- **Phase 2 (follow-ons):** per-level tuning; gap-sized extension (extend a naked end to its nearest
  neighbour instead of by a constant); report chosen parameters; memoize per-level closure.

## Risks & mitigations

- **Over-merge** (two real rooms merged): mitigated by local-only escalation and the "smallest
  parameters among ties" objective; clean regions are never escalated.
- **Cost** (each candidate is a full solve): bounded by gap-driven scope, a max-rounds cap, and an
  early stop when naked ends hit zero.
- **Coarse metric** (loop + area can be gamed): keep escalation conservative and surface the chosen
  parameters so a human can sanity-check.
- **Unproven on real models:** validated by CI for compile/units; real value confirmed only on real
  models.

## Out of scope

- Global grid sweep (rejected in favour of gap-driven escalation).
- Changing default parameters or public APIs while the feature is off.
