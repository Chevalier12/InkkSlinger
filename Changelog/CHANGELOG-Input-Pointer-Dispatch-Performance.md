# Changelog: Input Pointer Dispatch Performance Recovery

Date: 2026-02-15

## Summary
Pointer-input performance in interactive list/scroll scenarios was regressing badly, including visible freezes while moving the mouse and scrolling at the same time.  
The input pipeline was profiled and optimized, and the hotspot has now been reduced to a stable low-cost path.

## Initial Problem
- Scenario: moving mouse over `ListBox` items (especially while wheel scrolling) in `TwoScrollViewersView`.
- Observed runtime impact:
  - CPU usage frequently > 30% in active hover/scroll interaction.
  - UI could appear frozen until mouse movement stopped.
- Measured perf samples before fixes:
  - `InputMs` frequently in the `20ms-40ms` range per frame.
  - `DispatchMs` ~= `InputMs` (dispatch path dominated cost).
  - `PointerMs` ~= `DispatchMs`.
  - `ResolveMs` dominated pointer cost (often `~20ms-40ms`).

## Root Cause (Measured)
- The expensive path was pointer target resolution in the input dispatch phase, not render/layout work.
- The previous behavior did repeated pointer target validation/resolution on high-frequency move/wheel paths.

## Fixes Applied
- Added targeted input instrumentation to isolate sub-phase costs.
- Optimized pointer dispatch strategy:
  - Reuse hovered target for high-frequency move/wheel paths.
  - Restrict precise full target resolve to button transition paths (`MouseDown` / `MouseUp`) and fallback cases.
  - Cache wheel target ancestors (`TextBox` / `ScrollViewer`) and refresh on hover target changes.
- Reduced hot-path key-diff overhead in `InputManager.Capture()`.
- Added regression tests to lock behavior:
  - `PointerMove_ReusesHoveredTarget_WithoutRepeatedHitTests`
  - `MouseWheel_WithHoveredTarget_AvoidsHitTesting`

## Results After Fix
- New measured perf samples in the same scenario:
  - `InputMs`: typically `~0.20ms-0.48ms`
  - `DispatchMs`: typically `~0.16ms-0.32ms`
  - `VisualUpdateMs`: typically `~0.03ms-0.10ms`
  - `Hit:0` consistently during active move/wheel samples
  - `HitTestNeighbor` and `HitTestFullFallback`: `0`
- Practical effect:
  - CPU usage dropped substantially (user-observed around `<8%` during active testing).
  - No recurring freeze behavior in the reproduced scenario.

## Status
- Performance regression for pointer dispatch in this scenario is resolved.
- Current metrics indicate a healthy baseline for active hover/scroll interaction.

## User Note
I fought like hell to push this below 5% CPU and still couldn't fully nail it yet.  
I’ll keep grinding through more edge cases in future passes until it’s tighter.
