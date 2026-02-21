# InkkSlinger UI Framework TODO

## Status
- Core XAML/resources/styles/bindings pipeline is implemented and tested.
- Current framework is usable for menu/data/rich-text oriented MonoGame UI.
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
- [x] Binding parity gap #5 shipped: `PriorityBinding`, `BindingGroup` (inherited + named group resolution), and `UpdateSourceExceptionFilter` across `Binding`/`MultiBinding` with XAML + schema + regression coverage.
- [x] Binding regression safety-net expansion: added coverage for `BindingGroup` atomic commit/rollback, `PriorityBinding` edge cases, `UpdateSourceExceptionFilter` semantics, parser negative cases, and mode-trigger-validation matrix combinations.
- [x] Frame-latency diagnostics shipped (`INKKSLINGER_FRAME_LATENCY_LOGS`): alert-only 60 FPS thresholds, `event->next-draw` latency focus, miss-rate/dominant-phase attribution, and coalescing-aware sampling for move/scroll.
- [x] Rich document XAML strict-validation pass: explicit container child handling, invalid-structure enforcement, attribute validation (`TableCell` spans, `Hyperlink.NavigateUri`, `LineBreak` metadata-only), and rich parser regression coverage.
- [x] Rich clipboard fidelity pass: range-based fragment serialization/deserialization, rich-fragment-first paste with plain-text fallback on invalid payload, and regression coverage for copy/cut/paste semantics.
- [x] Rich advanced-structure pass: list indent/outdent transforms, table insert/split/merge and cell navigation behavior (`Tab`/`Shift+Tab`, boundary delete handling), hyperlink activation routing, and inline/block UI-container boundary support in rich layout/serialization.
- [x] Rich diagnostics/perf hardening pass: `RichTextBoxPerformanceSnapshot` metrics (layout cache + p95/p99 build timing, render/selection timing, undo depth/op counts, clipboard serde timing), env-var-driven rich diagnostics logs, and regression tests for bounded local-edit invalidation/allocation behavior.
- [x] Rich parity hardening: keyboard/reporting consistency for read-only editing keys and `Shift+Tab` no-op handling, plus list/table boundary keyboard space insertion safety (`Enter` + `Space`) without caret jump into adjacent table content.
- [x] Rich paste perf/interoperability hardening: external clipboard plain-text interop (Windows native clipboard read + fallback), per-paste clipboard snapshotting (single sync/read per paste), batched structured text paste insertion, and dedicated `INKKSLINGER_RICHTEXT_PASTE_CPU_LOGS` diagnostics with stage breakdown + clipboard sync counters.
- [x] ContextMenu parity hardening: right-click open and keyboard open (`Shift+F10`/Apps), no-layout-impact overlay behavior, hover-to-highlight/expand semantics (including submenu depth), deterministic first-hover submenu open after right-click, and dedicated hover diagnostics (`INKKSLINGER_CONTEXTMENU_HOVER_LOGS`).
- [x] ContextMenu performance diagnostics hardening: dedicated CPU diagnostics (`INKKSLINGER_CONTEXTMENU_CPU_LOGS`) with hover/open/invoke timing splits, resolver-path attribution, deep-branch traversal counters, and first-open vs warm-open invalidation breakdowns.
- [x] Adorner authoring ergonomics pass: introduced reusable `AnchoredAdorner`/`HandlesAdornerBase` APIs and added dedicated `AdornersLabView` demo surface (`--adorners-lab`).

## Current Workstream Snapshot
- [x] DataTrigger parity improvements, including `MultiDataTrigger`.
- [x] Trigger `EnterActions` / `ExitActions` support.
- [x] `ListView` / `ListViewItem` control support with keyboard navigation fixes.
- [x] `ContextMenu` parity rebase: WPF-style menu semantics (`ItemsControl` + `MenuItem` containers), attached-property element XAML shape (`<Button.ContextMenu>`), right-click open pipeline, and keyboard/pointer submenu traversal behavior.
- [x] `ProgressBar` control support (determinate + indeterminate rendering).
- [x] `TreeView` / `TreeViewItem` text rendering and keyboard navigation stability fixes.
- [x] `ResizeGrip` control support with drag and keyboard resizing.
- [x] `Thumb` control support with drag lifecycle events and demo.

## Diagnostics Backlog
- [x] Tail-latency diagnostics (`p95`/`p99`) for click/move/scroll interaction windows, based on `event->next-draw` latency.
- [x] Frame-budget miss diagnostics for 60 FPS (`16.6ms`) with dominant phase attribution (`input`, `layout`, `draw`, `other update`).
- [x] Dirty-region effectiveness diagnostics: partial redraw success rate, dirty-rect merge ratio, and full-redraw fallback trigger breakdown.
- [x] Render-cache churn diagnostics: per-frame hit/miss/rebuild ratios and top invalidation sources.
- [x] Allocation and GC diagnostics during interaction windows (allocated bytes delta, Gen0/1/2 collection deltas).
- [x] Input-routing complexity diagnostics: route depth and handler invocation counts for pointer/wheel/key routes.
- [x] No-op invalidation diagnostics (invalidations that produce no layout/visual delta).
- [x] Per-control hot-spot diagnostics (sampled top N controls by cumulative layout/draw/dispatch time).

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
- [x] Menu and commanding ecosystem:
`Menu`, `MenuItem`, declarative key-binding routing parity shipped, including menu-mode keyboard traversal (`F10`, `Alt+<letter>`, nested arrow navigation, `Esc` close/focus restore), pointer-driven menu-mode interactions, and parity-lab validation surface.
- [x] Virtualization:
`VirtualizingStackPanel` and container virtualization behavior for large lists/trees.
- [x] Transform-based `ScrollViewer` content scrolling parity:
plain panel hosts (for example `StackPanel`) use transform-based scrolling by default; `ScrollViewer.UseTransformContentScrolling="False"` provides explicit opt-out when translated arrange behavior is needed.
- [x] Data templating depth:
`DataTemplate` parity expansion and template selection support.
- [x] Animation system:
Storyboards, timelines, keyframes, easing, trigger-driven animations.
- [x] Binding parity gap #5:
`PriorityBinding`, `BindingGroup`, `UpdateSourceExceptionFilter`, plus dedicated demo surface (`--binding-parity-gap5-demo`).
- [x] Rich text and document layer:
`RichTextBox` and flow-document style content stack.
- [x] Advanced layout/adornment layer depth:
Adorner composition behaviors and layout nuances beyond the current primitives.
- [x] Windowing/popup edge parity:
Popup/menu/window interaction parity and additional edge behavior validation.
- [x] Context menu structural parity depth:
Rebased `ContextMenu` toward WPF-like menu semantics (`MenuItem`/`Separator`, keyboard submenu behavior) with right-click and keyboard open flows.
- [x] Context menu hover/input robustness:
Pointer-move dispatch is forced while any context menu is open (so hover transitions are not dropped when raw hit targets jitter), and hover diagnostics now emit `BeforeMove`/`AfterMove` state.

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
- [x] RichTextBox
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
