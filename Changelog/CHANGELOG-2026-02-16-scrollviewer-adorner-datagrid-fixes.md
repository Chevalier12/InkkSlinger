# Changelog: ScrollViewer/Adorner Clip Integrity + DataGrid Horizontal Scroll Consistency

Date: 2026-02-16

## Summary
This change set resolves two UI correctness issues discovered during PaintShell validation:

- DataGrid horizontal scrolling could desync headers and row content.
- Selection adorners could render over ScrollViewer scrollbar bands instead of clipping to content viewport.

## Included Changes
- Virtualized arrange-cache origin tracking in `VirtualizingStackPanel`:
  - Added arrange-origin cache state and cache invalidation when parent origin changes.
  - Ensures children are re-arranged when `ScrollViewer` horizontal offset changes content origin.
- Adorner viewport clipping hardening:
  - `Adorner` now resolves nearest ancestor `ScrollViewer` and clips to content viewport bounds.
  - Added internal `ScrollViewer.TryGetContentViewportClipRect(...)` helper for clip propagation.
- Regression tests:
  - Added `VirtualizingStackPanel_RearrangesChildren_WhenViewerHorizontalOriginChanges`.
  - Added new `Adorner_ClipsToScrollViewerContentViewport_InsteadOfScrollbarBand` test class/file.

## Validation
- `dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj --filter ScrollViewerViewerOwnedScrollingTests --nologo` passed.
- `dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj --filter AdornerClippingTests --nologo` passed.

## Notes
- This changelog is additive and summarizes all currently uncommitted UI scrolling/clipping fixes in the working tree.
