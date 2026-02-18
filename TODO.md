# InkkSlinger UI Framework TODO

## Status
- Core XAML/resources/styles/bindings pipeline is implemented and tested.
- Current framework is usable for menu-oriented MonoGame UI.
- This file tracks completed milestones, known parity gaps, and control coverage.

## Completed Milestones
- [x] XAML resources: `UserControl.Resources` / `Panel.Resources`, `x:Key`, `{StaticResource ...}`.
- [x] XAML resources: `{DynamicResource ...}` for attribute-based dependency and attached property assignment (setter/trigger value usage deferred).
- [x] Style completeness: `BasedOn`, explicit style references, implicit style lookup by `TargetType`.
- [x] Binding parity: `Source`, `ElementName`, `RelativeSource`.
- [x] Cleanup: `#nullable enable`, reflection-assigned `x:Name` warning cleanup.
- [x] Popup/window visuals moved to XAML resources/styles.
- [x] Baseline tests for resources/styles/bindings/style precedence.
- [x] Binding lifecycle robustness: detach/rebind on reparent and DataContext/source changes.
- [x] Resource resolution and precedence hardening: local/tree/app lookup and implicit style replacement behavior.
- [x] Input/focus edge-case hardening: capture/focus recovery, popup open/close behavior, keyboard focus traversal.
- [x] Layout and resize correctness under viewport changes and DPI-like scaling scenarios.
- [x] Rendering and invalidation efficiency pass (reduced redundant invalidations/updates).
- [x] Performance pass completed and baseline behavior verified in tests.
- [x] Wheel input routing fix: when hover-bypass mode is enabled and hover target is null, wheel now falls back to normal target resolution instead of dropping the event.
- [x] Better XAML diagnostics with element/property context plus line/position details.
- [x] Shared text layout module introduced (`TextLayout`) with reusable wrapping support (`TextWrapping` enum).
- [x] Text wrapping wired into text-bearing controls (`TextBlock`/`Label`, `Button`, popup title rendering path).
- [x] XML markup migration completed for views (`.xaml` -> `.xml`) with runtime/project path updates.
- [x] Schema/tooling hardening for XML authoring: expanded `InkkSlinger.UI.xsd` for resources/styles/triggers/bindings and added local `Xaml2006.xsd` mapping support for `x:` namespace.
- [x] Declarative commanding input model: `UIElement.InputBindings` + `KeyBinding`/`KeyGesture` routing, XAML authoring support, and menu shortcut auto-text derivation when unset.

## Current Workstream Snapshot
- [x] DataTrigger parity improvements, including `MultiDataTrigger`.
- [x] Trigger `EnterActions` / `ExitActions` support.
- [x] `ListView` / `ListViewItem` control support with keyboard navigation fixes.
- [x] `ContextMenu` control support and click-to-close behavior fix.
- [x] `ProgressBar` control support (determinate + indeterminate rendering).
- [x] `TreeView` / `TreeViewItem` text rendering and keyboard navigation stability fixes.
- [x] `ResizeGrip` control support with drag and keyboard resizing.
- [x] `Thumb` control support with drag lifecycle events and demo.

## Diagnostics Backlog
- [ ] Tail-latency diagnostics (`p95`/`p99`) for `LastUpdateMs`, `LastInputPhaseMs`, `LastLayoutPhaseMs`, and `LastDrawMs` in click/move/scroll windows.
- [ ] Frame-budget miss diagnostics (for example 16.6ms/8.3ms targets) with dominant phase attribution.
- [ ] Dirty-region effectiveness diagnostics: partial redraw success rate, dirty-rect merge ratio, and full-redraw fallback trigger breakdown.
- [ ] Render-cache churn diagnostics: per-frame hit/miss/rebuild ratios and top invalidation sources.
- [ ] Allocation and GC diagnostics during interaction windows (allocated bytes delta, Gen0/1/2 collection deltas).
- [ ] Input-routing complexity diagnostics: route depth and handler invocation counts for pointer/wheel/key routes.
- [ ] No-op invalidation diagnostics (invalidations that produce no layout/visual delta).
- [ ] Per-control hot-spot diagnostics (sampled top N controls by cumulative layout/draw/dispatch time).

## WPF Drawing & Geometry Coverage
- [x] `Path`
- [x] `Ellipse`
- [x] `Rectangle`
- [x] `Line`
- [x] `Polygon`
- [x] `Polyline`
- [x] `PathGeometry`
- [x] `GeometryGroup`
- [x] `CombinedGeometry`
- [x] `Transform` (`MatrixTransform`, `TranslateTransform`, `ScaleTransform`, `RotateTransform`, `SkewTransform`, `TransformGroup`)

## WPF Parity Gaps (From Discussion)
- [x] Templates and visual composition depth:
`ControlTemplate` parity expansion, template triggers, richer template binding behavior, named-part conventions.
- [x] Items system parity:
`HeaderedContentControl`, `HeaderedItemsControl`, `ContentPresenter`, `ItemsPresenter`.
- [ ] Menu and commanding ecosystem:
`Menu`, `MenuItem`, declarative key-binding routing parity shipped; additional keyboard/menu edge behaviors still ongoing.
- [x] Virtualization:
`VirtualizingStackPanel` and container virtualization behavior for large lists/trees.
- [x] Transform-based `ScrollViewer` content scrolling parity:
plain panel hosts (for example `StackPanel`) use transform-based scrolling by default; `ScrollViewer.UseTransformContentScrolling="False"` provides explicit opt-out when translated arrange behavior is needed.
- [x] Data templating depth:
`DataTemplate` parity expansion and template selection support.
- [x] Animation system:
Storyboards, timelines, keyframes, easing, trigger-driven animations.
- [ ] Rich text and document layer:
`RichTextBox` and flow-document style content stack.
- [ ] Advanced layout/adornment layer depth:
Adorner composition behaviors and layout nuances beyond the current primitives.
- [ ] Windowing/popup edge parity:
Popup/menu/window interaction parity and additional edge behavior validation.

## WPF Control Coverage
- [ ] AccessText
- [x] Border
- [x] Button
- [ ] Calendar
- [x] Canvas
- [x] CheckBox
- [x] ComboBox
- [x] ComboBoxItem
- [x] ContentControl
- [x] ContentPresenter
- [x] ContextMenu
- [x] Control
- [x] DataGrid
- [x] DataGridCell
- [x] DataGridColumnHeader
- [x] DataGridDetailsPresenter
- [x] DataGridRow
- [x] DataGridRowHeader
- [ ] DatePicker
- [x] Decorator
- [x] DockPanel
- [ ] DocumentViewer
- [x] Expander
- [ ] Frame
- [x] Grid
- [x] GridSplitter
- [x] GroupBox
- [ ] GroupItem
- [x] HeaderedContentControl
- [x] HeaderedItemsControl
- [x] Image
- [ ] InkCanvas
- [ ] InkPresenter
- [x] ItemsControl
- [x] Label
- [x] ListBox
- [x] ListBoxItem
- [x] ListView
- [x] ListViewItem
- [ ] MediaElement
- [x] Menu
- [x] MenuItem
- [ ] Page
- [x] Panel
- [x] PasswordBox
- [x] Popup
- [x] ProgressBar
- [x] RadioButton
- [x] RepeatButton
- [x] ResizeGrip
- [ ] RichTextBox
- [x] ScrollBar
- [x] ScrollViewer
- [x] Separator
- [x] Slider
- [x] StackPanel
- [x] StatusBar
- [x] StatusBarItem
- [x] TabControl
- [x] TabItem
- [x] TextBlock
- [x] TextBox
- [x] Thumb
- [x] ToggleButton
- [x] ToolBar
- [x] ToolBarOverflowPanel
- [x] ToolBarPanel
- [x] ToolBarTray
- [x] ToolTip
- [x] TreeView
- [x] TreeViewItem
- [x] UniformGrid
- [x] UserControl
- [x] Viewbox
- [x] VirtualizingStackPanel
- [x] WrapPanel
- [x] Window
