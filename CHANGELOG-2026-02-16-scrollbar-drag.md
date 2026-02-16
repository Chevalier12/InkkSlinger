# Changelog - 2026-02-16 - Scrollbar Drag Interaction

## Added
- Full pointer interaction for `ScrollBar` (track hit-testing, thumb press detection, page-step track clicks, and drag-to-value mapping).
- `ScrollViewer` pointer handlers for scrollbar mouse down/move/up to support live offset updates while dragging.

## Changed
- `UiRoot` input pipeline now routes scrollbar clicks and drag capture through owning `ScrollViewer` instances.
- Click target classification now treats `ScrollBar` and `ScrollViewer` as click-capable controls.

## Validation
- `dotnet build` succeeded with 0 warnings and 0 errors.
