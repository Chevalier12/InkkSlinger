# Changelog - ScrollViewer IScrollInfo Parity Branch

Date: 2026-02-13

## Added
- WPF-style scrolling contracts and event payloads:
  - `UI/Controls/IScrollInfo.cs`
  - `UI/Controls/ScrollChangedEventArgs.cs`
- Dedicated scroll content presenter for clipping, translation, and viewport/extent tracking:
  - `UI/Controls/ScrollContentPresenter.cs`
- ScrollViewer parity regression tests:
  - `InkkSlinger.Tests/ScrollViewerParityTests.cs`

## Changed
- `UI/Controls/ScrollViewer.cs`
  - Refactored to use `ScrollContentPresenter` and template part resolution for presenter/scrollbars.
  - Added `CanContentScroll` support and `IScrollInfo` handoff for line/page/wheel/offset operations.
  - Added computed scrollbar visibility state (`ComputedHorizontalScrollBarVisibility`, `ComputedVerticalScrollBarVisibility`) with auto-axis cascading behavior.
  - Added routed `ScrollChanged` event emission with extent/viewport/offset deltas.
  - Updated offset synchronization/coercion so viewer offsets stay consistent with `IScrollInfo` content.
  - Updated wheel handling to mark input handled only when offset actually changes.
- `UI/Controls/VirtualizingStackPanel.cs`
  - Now implements `IScrollInfo` and exposes extent/viewport/offset state.
  - Added scrolling command methods (`Line*`, `Page*`, `MouseWheel*`, `Set*Offset`, `MakeVisible`).
  - Added `ScrollOwner` invalidation flow so the host `ScrollViewer` can recompute scrollbars/metrics.
- `InkkSlinger.Tests/ScrollingInfrastructureTests.cs`
  - Documented known WPF-first behavioral divergences for scrolling semantics.

## Fixed
- Auto-scrollbar visibility now cascades correctly between axes when one scrollbar reduces the opposite viewport.
- Hidden scrollbar policy can keep scrolling enabled, while disabled policy blocks that axis.
- `ScrollChanged` notifications are suppressed when no effective scrolling metric changed.
