# AutoTuneSolver component guide

This note describes how the AutoTuneSolver Grasshopper component works end to end:
the data flow from canvas inputs, through the tuning engine and the NTS closure scorer,
to the outputs. It is an opt-in companion to the standard Solver component.

## What It Is For

The standard solver snaps, trims, extends, and merges wall geometry per level using each
panel's `BucketSize` and `MaxExtend` parameters. Choosing those values by hand is
fiddly: too small and gaps stay open, too large and real rooms get merged away.
`AutoTuneSolver` automates that search. It repeatedly re-solves, measures closed rooms
and remaining naked ends, and raises `MaxExtend` only on walls next to a remaining gap,
accepting a change only while it does not destroy a room.

## The Three Layers

### 1. Grasshopper Component

`Grasshopper/SAM.Analytical.Grasshopper.Solver/Component/AutoTuneSolver.cs`

The canvas node:

- Reads `_panels`, optional per-panel starting parameters (`bucketSizes_`, `weights_`,
  `maxExtensions_`), `levels_`, tuning knobs (`_maxRounds_`, `escalationLadder_`,
  `_escalateMaxExtend_`, `_mergeSlitWidth_`, `mergeSlitWidths_`,
  `_slitMinGap_`, `_slitMaxGap_`, `_slitMaxOverlap_`), and the usual solver tolerances.
- Converts each input panel to a SAM `Panel`, stamping any supplied bucket, weight, or
  extension onto it as solver parameters.
- Converts `levels_` to `Range<double>`.
- Builds an `AutoTuneSolver<Panel>`, copies the settings across, and runs `Execute`.
- Writes the tuned panels, remaining naked ends, report, remaining slit diagnostics,
  panels touching those slits, and recognised rooms.

### 2. Tuning Engine

`SAM_Solver/SAM.Analytical.Solver/Classes/AutoTuneSolver.cs`

`AutoTuneSolver<T>.Execute` is the search loop:

- Baseline: seed missing `BucketSize` and `MaxExtend`, run one ordinary `Solver<T>`,
  and score the result with `Evaluate`.
- Escalation rounds: when `_escalateMaxExtend_` is true, while dangles remain and
  `MaxRounds` is not hit, select panels whose footprint lies within
  `SnapDistance + max(escalationLadder_)` of a dangle on the same level, raise their
  `MaxExtend` by one ladder step, then re-solve. When false, AutoTune keeps the normal
  solver/default reach and leaves remaining naked ends.
- Acceptance: if room count drops, rollback that round and stop. Otherwise keep the
  round, and update the best result when dangles decrease or room count increases.
- Slit merge selection: if `mergeSlitWidths_` is supplied, run normal gap tuning with
  slit merging off, then test each candidate width and select the best result that does
  not reduce rooms or increase naked ends. 0 is always included as a fallback.
- Finish: restore global `SnapSolver` flags, populate `AutoTuneReport` including the
  selected merge width, extract remaining slit markers, and extract room polygons from
  the best result.

### 3. Closure Scorer

`SAM_Solver/SAM.Geometry.Solver/Classes/ClosureSolver.cs`

`ClosureSolver.Polygonize` is the shared measuring path used by both `Evaluate` and
`ExtractRooms`. For one level's wall axes it:

- Snap-rounds and nodes the segments on a fixed grid.
- Runs the noded edges through NetTopologySuite `Polygonizer`.
- Keeps room polygons whose bounding-box minimum side is at least 0.2 m.
- Reports dangling edges as naked-end targets for the next escalation round.

Because scoring and room extraction use the same method, the `rooms` output matches the
closed-loop count in the report.

## Inputs

| Input | Meaning |
|-------|---------|
| `_panels` | Wall panels as surface breps. |
| `bucketSizes_`, `weights_`, `maxExtensions_` | Optional per-panel starting parameters. |
| `levels_` | Per-floor elevation intervals. |
| `_escalateMaxExtend_` | Allow AutoTune to raise `MaxExtend` beyond default/caller values. Default: false. |
| `_maxRounds_` | Hard cap on escalation rounds. |
| `escalationLadder_` | Absolute `MaxExtend` targets in metres. Default: 0.5 / 0.75 / 1.0 / 1.5. |
| `_mergeSlitWidth_` | Manual merge width for parallel double-wall slits up to this gap in metres; 0 = off. Ignored when `mergeSlitWidths_` is supplied. |
| `mergeSlitWidths_` | Optional candidate merge widths for automatic slit-merge selection. Recommended starter list: 0 / 0.05 / 0.10 / 0.20 / 0.30. |
| `_slitMinGap_` | Minimum perpendicular gap width shown in `slits` diagnostics. Default: 0.02 m. |
| `_slitMaxGap_` | Maximum perpendicular gap width shown in `slits` diagnostics. Default: 0.5 m. For your typical actionable gaps, try 0.4 m. |
| `_slitMaxOverlap_` | Maximum parallel overlap length shown in `slits` diagnostics. Default: 2.0 m. Lower it to hide long side-by-side walls; set 0 for no overlap limit. |
| tolerances, `_levelOffset_`, `bucketBetweenLevels_`, `_nakedNodeSnapDistance_`, `_minWallSegmentLength_` | Mirror the standard solver settings. |

## Outputs

| Output | Shape | Meaning |
|--------|-------|---------|
| `panels` | tree, by level | Tuned wall panels. |
| `nakedEnds` | list | Remaining naked points after tuning. |
| `report` | item | Text trace: rounds, selected merge width, settings, rooms, area, naked ends, escalated panels, and remaining slits. |
| `slits` | tree, by level | Remaining internal double-wall/slit diagnostics as short `Segment3D` connectors between near-parallel wall axes. |
| `slitPanels` | tree, by level | Panels whose section axes touch the remaining `slits`; use these to target bucket-size overrides. |
| `rooms` | tree, by level | Closed room polygons (`Face3D`), grouped onto the same level branches as `panels`. |

## Scoring

The shared `ClosureReport` metric captures:

- Closed rooms: polygonised loops whose bounding-box minimum side is at least 0.2 m.
- Enclosed area: total area of those rooms.
- Naked ends: dangling edges left open.
- Wall segments: number of section segments on the level.

The standard Solver can emit a `ClosureReport`. AutoTuneSolver reports both the baseline
standard solve and the final tuned solve, so the two components can be compared directly.

The tuning objective is conservative: maximise closed rooms first, then minimise naked
ends. Any round that reduces room count is rolled back.

Automatic slit-merge selection is also conservative. Among non-regressing merge widths,
it prefers candidates with no protected merge skips, then fewer remaining slits, then
fewer solved wall panels. This avoids selecting an aggressive width simply because it
deletes more geometry.

The `slits` output is intentionally diagnostic. `_slitMinGap_` and `_slitMaxGap_`
filter by perpendicular gap width between walls, while `_slitMaxOverlap_` filters out
long side-by-side wall runs.

## Status

The `rooms` output is the implemented form of "Polygonizer -> emit rooms" from
`solver-improvements-assessment.md`. Rooms are currently flat floor outlines at the
section elevation, not 3D volumes, and internal holes are not represented.
