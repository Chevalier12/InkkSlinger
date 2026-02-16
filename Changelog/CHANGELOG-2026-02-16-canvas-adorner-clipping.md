# Changelog - 2026-02-16 - Canvas Adorner Clipping and PaintShell Default Demo

## Added
- Local clipping on `AdornerLayer` so selection/resize adorners are constrained to the adorner host bounds.

## Changed
- `PaintShell` is now the default startup demo when no CLI demo switch is provided.

## Fixed
- Selected-shape adorner visuals no longer render outside the canvas host region in `PaintShellView`.

## Validation
- `dotnet build InkkSlinger.sln -c Debug`