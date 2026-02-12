# InkkSlinger UI Framework TODO

## Status
- Core XAML/resources/styles/bindings pipeline is implemented and tested.
- Current framework is usable for menu-oriented MonoGame UI.
- This file tracks completed milestones, known parity gaps, and control coverage.

## Completed Milestones
- [x] XAML resources: `UserControl.Resources` / `Panel.Resources`, `x:Key`, `{StaticResource ...}`.
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
- [x] Better XAML diagnostics with element/property context plus line/position details.
- [x] Shared text layout module introduced (`TextLayout`) with reusable wrapping support (`TextWrapping` enum).
- [x] Text wrapping wired into text-bearing controls (`TextBlock`/`Label`, `Button`, popup title rendering path).
- [x] XML markup migration completed for views (`.xaml` -> `.xml`) with runtime/project path updates.
- [x] Schema/tooling hardening for XML authoring: expanded `InkkSlinger.UI.xsd` for resources/styles/triggers/bindings and added local `Xaml2006.xsd` mapping support for `x:` namespace.

## Current Workstream Snapshot
- [x] DataTrigger parity improvements, including `MultiDataTrigger`.
- [x] Trigger `EnterActions` / `ExitActions` support.
- [x] `ListView` / `ListViewItem` control support with keyboard navigation fixes.
- [x] `ContextMenu` control support and click-to-close behavior fix.
- [x] `ProgressBar` control support (determinate + indeterminate rendering).
- [x] `TreeView` / `TreeViewItem` text rendering and keyboard navigation stability fixes.
- [x] `ResizeGrip` control support with drag and keyboard resizing.
- [x] `Thumb` control support with drag lifecycle events and demo.

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
`Menu`, `MenuItem`, richer routed-command behaviors and keyboard/menu interaction parity.
- [x] Virtualization:
`VirtualizingStackPanel` and container virtualization behavior for large lists/trees.
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
- [ ] PasswordBox
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
