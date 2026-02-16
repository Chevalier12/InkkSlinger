# Changelog: UI Framework Reorganization and Move CPU Diagnostics

Date: 2026-02-16

## Summary
This pass introduces a full UI-framework folder reorganization (no `Views` changes), adds dedicated mouse-move CPU diagnostics, and keeps scroll/click diagnostics available but disabled by default for focused profiling.

## Included Changes
- Added new `[MoveCpu]` diagnostics stream in `UiRoot` input/update/draw pipeline.
- Added `INKKSLINGER_MOVE_CPU_LOGS` env gating for move diagnostics.
- Kept `[ScrollCpu]` and `[ClickCpu]` diagnostics in place but set default launch env to disabled for focused move profiling.
- Added click/scroll/move diagnostics CPU% reporting consistency in summary output.
- Reorganized `UI` folder structure across framework domains:
  - `UI/Managers` grouped into `Root`, `Root/Services`, `Root/Services/Diagnostics`, `Layout`, `Tree`.
  - `UI/Controls` split into feature subfolders (`Base`, `Panels`, `Buttons`, `Inputs`, `Scrolling`, `Items`, `DataGrid`, `Selection`, `Adorners`, `Containers`, `Primitives`, `Presenters`).
  - Remaining UI domains grouped into internal subfolders (`Core`, `Types`, `Args`, `State`, etc.) for consistency.

## Validation
- Build: `dotnet build InkkSlinger.csproj -nologo` passed.
- Tests: `dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj --filter InputDispatchOptimizationTests --nologo` passed.

## Status
- UI framework structure is now consistently grouped by feature and responsibility.
- Move diagnostics are ready for profiling sessions with scroll/click diagnostics toggleable by env variables.
