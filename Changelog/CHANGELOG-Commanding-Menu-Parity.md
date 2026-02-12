# Changelog - Commanding and Menu Parity Branch

Date: 2026-02-12

## Added
- Routed commanding foundation:
  - `UI/Commanding/` command infrastructure.
  - Input gesture/binding support:
    - `UI/Input/InputBinding.cs`
    - `UI/Input/InputGesture.cs`
    - `UI/Input/KeyGesture.cs`
- Commanding coverage tests:
  - `InkkSlinger.Tests/CommandingTests.cs`
- New interactive demo view for commanding/menu parity:
  - `Views/CommandingMenuDemoView.xml`
  - `Views/CommandingMenuDemoView.xml.cs`

## Changed
- App/demo startup wiring and project inclusion updates:
  - `Program.cs`
  - `Game1.cs`
  - `InkkSlinger.csproj`
- Control and dependency behavior updates to support commanding/menu parity and interaction flow:
  - `UI/Controls/Control.cs`
  - `UI/Core/DependencyObject.cs`
  - `UI/Controls/Menu.cs`
  - `UI/Controls/MenuItem.cs`
  - `UI/Controls/ItemsControl.cs`
  - `UI/Controls/Panel.cs`
  - `UI/Controls/Presenters.cs`
  - `UI/Controls/ListBox.cs`
  - `UI/Controls/ScrollViewer.cs`
  - `UI/Controls/UIElement.cs`
  - `UI/Controls/VirtualizingStackPanel.cs`
- Input/event pipeline updates:
  - `UI/Input/InputManager.cs`
  - `UI/Managers/VisualTreeHelper.cs`

## Fixed
- Restored `ListBox` behavior to `ItemsPresenter`-based composition while keeping demo compatibility.
- Corrected item hit-test behavior/perf path when pointer is over list content during scrolling.
- Reduced scroll update overhead by avoiding unnecessary offset churn/sync loops in `ScrollViewer`.
- Removed per-hit-test transform-chain allocation cost from `UIElement` hit-testing.

## Performance Notes
- Resolved severe interaction hitch seen when hovering list items and scrolling near top of list content.
- Preserved expected smooth interaction when interacting directly with scrollbar controls.

## Misc
- Updated ignore/config housekeeping in `.gitignore`.
