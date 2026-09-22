# Project Progress

## Branch
`sow/2026-Q3`

## Last updated
2026-09-22 - app.config cleanup merged; MSB3243 closeout

## Current status
Part of the repo-family .NET Framework `app.config` cleanup: base [SAM#126](https://github.com/SAM-BIM/SAM/pull/126) plus 17 sibling PRs, all merged into `sow/2026-Q3` on 2026-09-22 (SAM first), with their branches deleted.

## Completed
- [SAM_Solver#12](https://github.com/SAM-BIM/SAM_Solver/pull/12) merged as `b7e9b132`: removed dead .NET Framework `app.config` files.
- Closeout (branch `build/netfx-cleanup-closeout`): dropped the bare `<Reference Include="System.Data.DataSetExtensions" />` and `<Reference Include="Microsoft.CSharp" />` from `Rhino/SAM.Analytical.Rhino.Solver/SAM.Analytical.Rhino.Solver.csproj`. Under net8.0-windows both come from the shared framework, and the bare references caused its MSB3243 "no way to resolve conflict" warnings.

## Decisions / assumptions
- Every project here targets `netstandard2.0` or `net8.0(-windows)` and is an `OutputType Library`. Library `.dll.config` files are never read at runtime (only the host `Rhino.exe`/`Revit.exe` config is), so the net472-era binding redirects, `<supportedRuntime>` and `loadFromRemoteSources` were inert. They only emitted stale `.dll.config` files into `build/` and `%APPDATA%\SAM`.
- No `ConfigurationManager`/`AppSettings` use in the repo; deleted files held binding/runtime config only.

## Files changed
- `Grasshopper/SAM.Analytical.Grasshopper.Solver/app.config` (deleted)
- `Grasshopper/SAM.Core.Grasshopper.Solver/app.config` (deleted)
- `Grasshopper/SAM.Geometry.Grasshopper.Solver/app.config` (deleted)
- `SAM_Solver/SAM.Analytical.Solver/app.config` (deleted)
- `Rhino/SAM.Analytical.Rhino.Solver/SAM.Analytical.Rhino.Solver.csproj` (closeout: reference dropped)

## Validation
- Before merge, full `BuildAlls_v4.bat` (Debug Restore;Clean;Rebuild of every repo, starting from an emptied `%APPDATA%\SAM`): exit 0, 0 errors. The redeployed `%APPDATA%\SAM` has no `SAM.*.dll.config`.
- CI on the PR: build pass (no spdx workflow).
- Closeout: full `BuildAlls_v4.bat` clean rebuild after the `System.Data.DataSetExtensions` removal - exit 0, 0 errors; the one remaining MSB3243 here was the `Microsoft.CSharp` reference, removed afterwards. Re-run after that: exit 0, 0 errors, **0 MSB3243**.

## Issues / blockers
- None known for this cleanup.

## Next step
- None for this cleanup once the `build/netfx-cleanup-closeout` PR is merged.
