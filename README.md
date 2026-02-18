# InkkSlinger UI Framework

[![.NET](https://img.shields.io/badge/.NET-9%20%2F%2010-512BD4?logo=dotnet&logoColor=white)](#build-and-test)
[![MonoGame](https://img.shields.io/badge/MonoGame-DesktopGL-FF7F50)](#vision)
[![WPF Parity](https://img.shields.io/badge/WPF%20parity-ongoing-blue)](#wpf-parity-position)

InkkSlinger is a custom UI framework for MonoGame/DesktopGL with one primary objective:

**achieve practical WPF parity as closely as possible** across APIs, behavior, composition model, and authoring workflow.

This repository is framework-first.
The files in `Views/` are validation/demo surfaces used to exercise framework features and regressions, not the end product.

## Why This Exists

WPF provides a strong UI model, but it is not directly available inside game-oriented rendering pipelines like MonoGame/DesktopGL.

This project closes that gap by bringing WPF-style composition into a real-time loop:

- build desktop-style UI in a MonoGame host
- preserve familiar WPF-like authoring patterns
- validate behavior through parity-focused tests

## For Who This Is

Me.

This framework is unapologetically opinionated. It exists to satisfy a very specific bar for architecture, behavior, and authoring feel.

## Vision

Bring a WPF-like development model into a game/render-loop environment by implementing:

- dependency properties and metadata-driven behavior
- routed events and focus/input semantics
- measure/arrange style layout composition
- resources, styles, triggers, and templating
- MVVM-style binding primitives
- timeline/storyboard-driven animation
- broad control coverage with regression tests

## WPF Parity Position

Parity is treated as a long-running engineering target, not a marketing claim.
The implementation is broad and deep, but intentionally explicit about gaps.

### Parity Summary

This matrix is compiled from `TODO.md` completed work, concrete type coverage under `UI/`, and the current regression suite.

| Area | Implemented Parity Surface | Evidence | Status |
|---|---|---|---|
| Dependency property system | Core DP registration, metadata callbacks/options, value source layering | `UI/Core/DependencyProperties/DependencyObject.cs`, `UI/Core/DependencyProperties/DependencyProperty.cs`, `UI/Core/DependencyProperties/FrameworkPropertyMetadata.cs` | Implemented |
| Routed event model | Routed event registration/dispatch + routed args types | `UI/Events/Core/EventManager.cs`, `UI/Events/Core/RoutedEvent.cs`, `UI/Events/Args/*` | Implemented |
| Input + focus pipeline | Input snapshots/delta, dispatch state, focus management integrated into root update | `UI/Input/Core/InputManager.cs`, `UI/Input/Core/FocusManager.cs`, `UI/Managers/Root/Services/UiRootInputPipeline.cs` | Implemented |
| UI dispatcher/phase model | Deferred operations + strict frame phase ordering | `UI/Core/Threading/Dispatcher.cs`, `UI/Managers/Root/Services/UiRootFrameState.cs`, `InkkSlinger.Tests/DispatcherPhaseOrderTests.cs` | Implemented (tested) |
| Layout framework | Measure/arrange across framework elements and panels | `UI/Controls/Base/FrameworkElement.cs`, `UI/Managers/Layout/LayoutManager.cs`, `UI/Controls/Panels/*` | Implemented |
| Visual tree utilities | Tree traversal + hit-test support for input/render decisions | `UI/Managers/Tree/VisualTreeHelper.cs` | Implemented |
| Resource dictionaries | Local/tree/app resource resolution with dictionary change notifications | `UI/Resources/Core/ResourceDictionary.cs`, `UI/Resources/Core/ResourceResolver.cs`, `UI/Resources/Types/ResourceDictionaryChangedEventArgs.cs` | Implemented |
| Style system | `Style`, `Setter`, implicit/explicit style application, `BasedOn` support | `UI/Styling/Core/Style.cs`, `UI/Styling/Core/Setter.cs`, `TODO.md` (`Completed Milestones`) | Implemented |
| Trigger framework | Property/data/multi-data/event triggers + condition evaluation | `UI/Styling/Triggers/Trigger.cs`, `UI/Styling/Triggers/DataTrigger.cs`, `UI/Styling/Triggers/MultiDataTrigger.cs`, `UI/Styling/Triggers/EventTrigger.cs` | Implemented |
| Trigger action runtime | `SetValueAction` + storyboard action family in trigger execution path | `UI/Styling/Actions/SetValueAction.cs`, `UI/Styling/Actions/StoryboardActions.cs`, `UI/Xaml/Core/XamlLoader.cs` (`BuildTriggerAction`) | Implemented |
| Control templating | `ControlTemplate`, template binding, template trigger engine | `UI/Templating/Core/ControlTemplate.cs`, `UI/Templating/Core/TemplateBinding.cs`, `UI/Templating/Core/TemplateTriggerEngine.cs` | Implemented |
| Data templating | `DataTemplate`, selector, resolver pipeline | `UI/Templating/Data/DataTemplate.cs`, `UI/Templating/Data/DataTemplateSelector.cs`, `UI/Templating/Data/DataTemplateResolver.cs` | Implemented |
| Binding core | `Path`, `Source`, `ElementName`, `RelativeSource`, modes and update triggers | `UI/Binding/Core/Binding.cs`, `UI/Binding/Core/BindingExpression.cs`, `UI/Binding/Core/BindingOperations.cs`, `UI/Binding/Types/BindingEnums.cs` | Implemented |
| Binding lifecycle robustness | Rebind behavior on source/data-context/tree changes | `TODO.md` (`Completed Milestones`: binding lifecycle robustness) | Implemented |
| Routed commanding core | `RoutedCommand`, command bindings, can-execute/execute pipeline | `UI/Commanding/RoutedCommand.cs`, `UI/Commanding/CommandManager.cs`, `UI/Commanding/CommandBinding.cs`, `InkkSlinger.Tests/CommandingTests.cs` | Implemented (tested) |
| Gesture-to-command bridge | Runtime keyboard chord routing into routed commands | `UI/Input/Core/InputGestureService.cs`, `Views/CommandingMenuDemoView.xml.cs` | Implemented |
| XAML/XML loader | Runtime object graph construction, attached properties, handlers, bindings, templates, triggers | `UI/Xaml/Core/XamlLoader.cs` | Implemented |
| XAML diagnostics | Element/attribute error reporting with contextual diagnostics | `UI/Xaml/Core/XamlLoader.cs`, `TODO.md` (`Completed Milestones`: better diagnostics) | Implemented |
| XML schema/tooling | Authoring schemas for framework + `x:` namespace support | `Schemas/InkkSlinger.UI.xsd`, `Schemas/Xaml2006.xsd`, `TODO.md` (`Completed Milestones`) | Implemented |
| Name scope + source gen | `x:Name` mapping via runtime scope + incremental generator | `UI/Core/Naming/NameScope.cs`, `InkkSlinger.XamlNameGenerator/XNameGenerator.cs`, `InkkSlinger.Tests/XNameSourceGeneratorTests.cs` | Implemented (tested) |
| Animation timeline system | Timelines/storyboards, begin/stop/pause/resume/seek, handoff modes | `UI/Animation/Timelines/*`, `UI/Animation/Core/AnimationManager.cs`, `UI/Animation/Types/AnimationPrimitives.cs` | Implemented |
| Keyframe/easing support | Double/color/point/thickness/int/object keyframes + easing/key-spline support | `UI/Animation/KeyFrames/*`, `UI/Animation/Easing/Easing.cs`, `UI/Animation/Types/*` | Implemented |
| Geometry and shapes | Shape primitives + geometry/transform model + path markup parsing | `UI/Controls/Primitives/Shape.cs`, `UI/Geometry/Core/Geometry.cs`, `UI/Geometry/Core/Transform.cs`, `UI/Geometry/Parsing/PathMarkupParser.cs` | Implemented |
| Text layout primitives | Shared text layout, wrapping, text rendering integration | `UI/Text/Core/TextLayout.cs`, `UI/Text/Types/TextWrapping.cs`, `UI/Rendering/Text/FontStashTextRenderer.cs` | Implemented |
| Text editing pipeline | Selection/edit/clipboard buffer + textbox parity checks | `UI/Text/Editing/TextEditingBuffer.cs`, `UI/Text/Editing/TextSelection.cs`, `UI/Text/Editing/TextClipboard.cs`, `InkkSlinger.Tests/TextEditingBufferTests.cs`, `InkkSlinger.Tests/TextPipelineParityTests.cs` | Implemented (tested) |
| Scrolling primitives | `ScrollViewer`, `ScrollBar`, visibility and owner-scrolling behavior | `UI/Controls/Scrolling/ScrollViewer.cs`, `UI/Controls/Scrolling/ScrollBar.cs`, `InkkSlinger.Tests/ScrollViewerViewerOwnedScrollingTests.cs` | Implemented (tested) |
| Virtualization | Virtualizing panel infrastructure for large item collections | `UI/Controls/Panels/VirtualizingStackPanel.cs`, `UI/Controls/Scrolling/VirtualizationEnums.cs`, `TODO.md` (`WPF Parity Gaps`: virtualization checked) | Implemented |
| Rendering invalidation | Dirty region tracking + conditional draw scheduling | `UI/Rendering/DirtyRegions/DirtyRegionTracker.cs`, `UI/Managers/Root/Services/UiRootFrameState.cs`, `InkkSlinger.Tests/DirtyRegionTrackingTests.cs`, `InkkSlinger.Tests/ConditionalDrawTests.cs` | Implemented (tested) |
| Render caching | Element cache policy/store + visual-layer cache behavior | `UI/Rendering/Cache/RenderCachePolicy.cs`, `UI/Rendering/Cache/RenderCacheStore.cs`, `InkkSlinger.Tests/RenderCachePolicyTests.cs`, `InkkSlinger.Tests/VisualLayerCachingTests.cs` | Implemented (tested) |
| Render queue and invalidation correctness | Queue ordering and invalidation semantics in root draw/update pipeline | `UI/Managers/Root/UiRoot.cs`, `InkkSlinger.Tests/RenderQueueTests.cs`, `InkkSlinger.Tests/InvalidationFlagsTests.cs` | Implemented (tested) |
| Adorner infrastructure | Adorner base/layer/decorator + clipping/selection adorners | `UI/Controls/Adorners/*`, `UI/Controls/Selection/SelectionRectangleAdorner.cs`, `InkkSlinger.Tests/AdornerClippingTests.cs` | Implemented (tested) |
| Control breadth snapshot | 65 implemented controls out of 77 tracked WPF controls | `TODO.md` (`## WPF Control Coverage`, computed: `65/77`) | Broad |
| Container/windowing primitives | `Window`, `Popup`, `ContextMenu`, `ToolTip`, `UserControl`, `Viewbox` | `UI/Controls/Containers/*`, `UI/Controls/Items/ContextMenu.cs` | Implemented (ongoing depth) |
| Item and data controls | `ListBox`, `ListView`, `TreeView`, `Menu`, `DataGrid` families | `UI/Controls/Items/*`, `UI/Controls/DataGrid/*`, `TODO.md` (`Current Workstream Snapshot`) | Implemented (ongoing depth) |
| Runtime telemetry/diagnostics | UiRoot frame/cache/draw/layout telemetry snapshot surfaces | `UI/Managers/Root/UiRootTypes.cs`, `UI/Diagnostics/*`, `InkkSlinger.Tests/UiRootTelemetryTests.cs` | Implemented (tested) |
| Regression safety net | 15 focused test files covering core pipeline/regressions | `InkkSlinger.Tests/*Tests.cs` (15 files, no skipped tests found) | Implemented |

### Implemented Foundations

- Dependency property system (`DependencyObject`, `DependencyProperty`, metadata/value-source model)
- Routed event infrastructure (`EventManager`, routed args and routing strategies)
- Input/focus pipeline integrated with UI update loop
- Layout system across base elements and panel/control composition
- XML markup loading via `XamlLoader`
- Resources, styles, setters, triggers, and visual state primitives
- Binding primitives (`Path`, `Source`, `ElementName`, `RelativeSource`)
- Templates (`ControlTemplate`, `DataTemplate`, selectors/resolvers)
- Storyboards, timelines, keyframes, easing, and animation manager
- Virtualization primitives (`VirtualizingStackPanel` and scrolling infrastructure)
- Transform-based `ScrollViewer` content scrolling for plain panel hosts by default, with attached-property opt-out (`ScrollViewer.UseTransformContentScrolling="False"`)
- Dirty-region-aware rendering and cache policy in `UiRoot`/`UI/Rendering`
- Tooling support via schemas and `x:Name` source generation

### Not Yet Implemented / Out of Scope (For Now)

This matrix is compiled from a full pass over `TODO.md`, `UI/` source limitations (`not supported` guards), schema coverage, and missing-type checks.

| Area | Gap / Limitation | Evidence | State |
|---|---|---|---|
| Control coverage | `AccessText` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `Calendar` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `DatePicker` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `DocumentViewer` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `Frame` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `GroupItem` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `InkCanvas` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `InkPresenter` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `MediaElement` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `Page` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `PasswordBox` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Control coverage | `RichTextBox` | `TODO.md` (`## WPF Control Coverage`) | Not implemented |
| Parity track | Menu + commanding ecosystem depth (keyboard/menu edge behavior) | `TODO.md` (`## WPF Parity Gaps`) | Ongoing |
| Parity track | Rich text + document layer depth (flow-document model) | `TODO.md` (`## WPF Parity Gaps`) | Not implemented |
| Parity track | Advanced adorner/layout composition depth | `TODO.md` (`## WPF Parity Gaps`) | Ongoing |
| Parity track | Windowing/popup edge parity and interaction depth | `TODO.md` (`## WPF Parity Gaps`) | Ongoing |
| Binding API | `IValueConverter` / `IMultiValueConverter` interfaces | `UI/Binding/Converters/IValueConverter.cs`, `UI/Binding/Converters/IMultiValueConverter.cs` | Implemented |
| Binding API | `MultiBinding` | `UI/Binding/Core/MultiBinding.cs`, `UI/Binding/Core/MultiBindingExpression.cs`, `UI/Xaml/Core/XamlLoader.cs` | Implemented |
| Binding API | `PriorityBinding` | No matching types in `UI/Binding` | Not implemented |
| Binding API | `BindingGroup` | No matching types in `UI/Binding` | Not implemented |
| Binding API | Validation stack (`Validation`, `ValidationRule`, data-error pipelines) | `UI/Binding/Validation/Validation.cs`, `UI/Binding/Validation/ValidationRule.cs`, `UI/Binding/Core/BindingExpression.cs`, `UI/Binding/Core/MultiBindingExpression.cs` | Implemented |
| Binding API | `IDataErrorInfo` / `INotifyDataErrorInfo` binding error pipelines | `UI/Binding/Core/BindingExpression.cs`, `UI/Binding/Core/MultiBindingExpression.cs` | Implemented |
| Binding API | `UpdateSourceExceptionFilter` behavior hooks | No matching support in `UI/Binding` | Not implemented |
| Data/view layer | WPF `CollectionView` stack (current item/filter/sort/group) | No matching collection-view types in `UI/Binding`/`UI/Controls` | Not implemented |
| Binding API | `BindingMode` includes WPF `OneWayToSource` and `Default` | `UI/Binding/Types/BindingEnums.cs`, `UI/Binding/Core/BindingExpressionUtilities.cs` | Implemented |
| Binding API | `UpdateSourceTrigger` includes WPF `LostFocus` and `Default` | `UI/Binding/Types/BindingEnums.cs`, `UI/Binding/Core/BindingExpressionUtilities.cs` | Implemented |
| XAML binding parser | Unrecognized binding attributes are rejected | `UI/Xaml/Core/XamlLoader.cs` (`BuildBindingElement`) | Partial |
| XAML binding parser | Unrecognized binding keys are rejected | `UI/Xaml/Core/XamlLoader.cs` (`ParseBinding`) | Partial |
| XAML `RelativeSource` parser | Only `Mode`, `AncestorType`, `AncestorLevel` keys are accepted | `UI/Xaml/Core/XamlLoader.cs` (`ParseRelativeSource`) | Partial |
| XAML resources | `DynamicResource` markup support for attribute-based dependency and attached properties; setter/trigger values remain deferred | `UI/Xaml/Core/XamlLoader.cs` (`TryParseDynamicResourceKey`, `TryApplyDynamicResourceExpression`, `ApplyAttachedProperty`) | Partial |
| XAML language | General WPF `MarkupExtension` ecosystem | No `MarkupExtension` base in `UI/`; parser uses explicit special cases | Not implemented |
| XAML language | Broader `x:` metadata surface (`x:Class`, `x:Static`, etc.) | `UI/Xaml/Core/XamlLoader.cs` (`ApplyAttributes` handles `x:Name`/`x:Key` paths only) | Partial |
| XAML tooling | WPF designer/Blend/XAML compilation parity | Runtime loader path in `UI/Xaml/Core/XamlLoader.cs`; no compile-time XAML toolchain in repo | Not implemented |
| Resources/XAML | Full merged-dictionary authoring + WPF merge edge semantics | Runtime merge API in `UI/Resources/Core/ResourceDictionary.cs`; no dedicated merged-dictionary parse path in `UI/Xaml/Core/XamlLoader.cs` | Partial |
| Styling/XAML | Trigger actions are limited to `SetValueAction`, `BeginStoryboard`, `StopStoryboard`, `PauseStoryboard`, `ResumeStoryboard`, `SeekStoryboard`, `RemoveStoryboard` | `UI/Xaml/Core/XamlLoader.cs` (`BuildTriggerAction`) | Partial |
| Styling API | `MultiTrigger` | No matching type in `UI/Styling/Triggers` | Not implemented |
| Styling API | `EventSetter` | No matching type in `UI/Styling` | Not implemented |
| Commanding/Input API | Declarative `InputBinding` / `KeyGesture` / `MouseGesture` model | No matching types in `UI/`; imperative registration via `UI/Input/Core/InputGestureService.cs` | Partial |
| Commanding API | `RoutedUICommand` surface | No matching `RoutedUICommand` type in `UI/Commanding` | Not implemented |
| Commanding API | `ICommandSource` ecosystem parity across controls | No matching `ICommandSource` type in `UI/`; command hookup is control-specific | Partial |
| Container behavior | `UserControl` custom `ControlTemplate` is blocked (`NotSupportedException`) | `UI/Controls/Containers/UserControl.cs` | Not supported |
| Geometry path parser | Path commands `S/s`, `T/t`, `A/a` are rejected | `UI/Geometry/Parsing/PathMarkupParser.cs` | Partial |
| Text pipeline | Virtual wrapped text layout optimization is intentionally disabled for correctness during resize reflow | `UI/Controls/Inputs/TextBox.cs` (`CanUseVirtualWrappedLayout`) | Temporarily disabled |
| Framework services | `Freezable` semantics and related WPF service patterns | No `Freezable` infrastructure in `UI/` | Not implemented |
| Layout parity | Layout rounding / DPI-specific rounding parity (`UseLayoutRounding`-style behavior) | No matching layout-rounding API in `UI/Layout`/`UI/Controls` | Not implemented |
| Input stack | Touch/stylus/tablet pipelines | No corresponding types under `UI/Input` | Not implemented |
| Accessibility | UI automation / `AutomationPeer` layer | No corresponding types in `UI/`; no automation tree APIs | Not implemented |
| Rendering parity | Pixel-identical WPF rendering/composition fidelity | Framework targets MonoGame parity behavior, not WPF pixel-clone output | Out of scope |

## UI Architecture Map

The following reflects `UI-FOLDER-MAP.md` (generated 2026-02-16):

- `UI/Animation`: timelines, keyframes, easing, animation orchestration
- `UI/Binding`: bindings, expressions, operations, command helpers
- `UI/Commanding`: routed command infrastructure
- `UI/Controls`: base layer + buttons, containers, data grid, inputs, items, panels, primitives, scrolling, selection, adorners
- `UI/Core`: dependency properties, naming, dispatcher
- `UI/Events`: routed event core, args, routing strategy
- `UI/Geometry`: geometry and transform primitives + path parsing
- `UI/Input`: focus/input managers and input state snapshots
- `UI/Layout`: shared layout types (`Thickness`, alignment, orientation, etc.)
- `UI/Managers`: layout manager, visual tree helper, `UiRoot` services and diagnostics
- `UI/Rendering`: draw pipeline, dirty regions, cache, text renderer
- `UI/Resources`: dictionaries and resolution/application resources
- `UI/Styling`: styles, setters, visual states, triggers, trigger actions
- `UI/Templating`: control/data templates and trigger engine
- `UI/Text`: text layout and editing pipeline
- `UI/Xaml`: runtime XML loading

## Runtime Model

At runtime, the framework generally flows as:

1. Input update and routing
2. Animation update
3. Layout pass when required
4. UI tree update
5. Conditional draw pass based on invalidation/redraw reasons
6. Dirty-region or full-frame composition

This keeps behavior deterministic while reducing unnecessary redraw work for large trees.

## Markup Model

- Markup files use `.xml` (WPF-inspired structure with `x:` support)
- `x:Name` is supported and mapped to generated members
- Schemas for editor assistance:
  - `Schemas/InkkSlinger.UI.xsd`
  - `Schemas/Xaml2006.xsd`

## Build and Test

Prerequisite:

- .NET SDK 10.x recommended

```powershell
dotnet restore InkkSlinger.sln
dotnet build InkkSlinger.sln -v minimal
dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj -v minimal
```

## Test Environment

Primary validation machine for current development/testing:

- OS: Windows 10 Pro (10.0.19045, 64-bit)
- CPU: Intel Core i7-5600U @ 2.60GHz (2 cores / 4 logical processors)
- RAM: 12 GB
- GPU: Intel HD Graphics 5500

## Running the Host Application

| Mode | Command |
|---|---|
| Default host mode | `dotnet run --project InkkSlinger.csproj` |
| Dark dashboard demo | `dotnet run --project InkkSlinger.csproj -- --dark-dashboard` |
| Main menu demo | `dotnet run --project InkkSlinger.csproj -- --main-menu` |
| Window demo | `dotnet run --project InkkSlinger.csproj -- --window-demo` |
| Paint shell demo | `dotnet run --project InkkSlinger.csproj -- --paint-shell` |
| Commanding demo | `dotnet run --project InkkSlinger.csproj -- --commanding-demo` |

Current default launch surface is the dark dashboard demo when no explicit mode flag is provided.

## Environment Variables

Environment switches are string-based and compared using ordinal checks.

- `enable-with-1` flags: only exact `"1"` enables; any other value (or unset) disables.
- `disable-with-0` flags: exact `"0"` disables; any other value (or unset) enables.
- Most flags are read at startup (`UiRoot` construction / `Game1` init). A few input flags are checked during update.

PowerShell example:

```powershell
$env:INKKSLINGER_EXPERIMENTAL_PARTIAL_REDRAW = "1"
$env:INKKSLINGER_RENDER_CACHE_OVERLAY = "1"
dotnet run --project InkkSlinger.csproj -- --commanding-demo
Remove-Item Env:INKKSLINGER_EXPERIMENTAL_PARTIAL_REDRAW
Remove-Item Env:INKKSLINGER_RENDER_CACHE_OVERLAY
```

### Rendering and Frame Scheduling

| Variable | Mode | Default | Read Timing | Effect |
|---|---|---|---|---|
| `INKKSLINGER_EXPERIMENTAL_PARTIAL_REDRAW` | enable-with-1 | disabled | `Game1` startup | Sets `UseRetainedRenderList`, `UseDirtyRegionRendering`, and `UseElementRenderCaches` together. |
| `INKKSLINGER_RECURSIVE_DRAW_FALLBACK` | enable-with-1 | disabled | `UiRoot` construction | Forces fallback away from retained render queue default path. |
| `INKKSLINGER_RETAINED_RENDER_QUEUE` | disable-with-0 | enabled | `UiRoot` construction | Controls retained render list default (`UseRetainedRenderList`). |
| `INKKSLINGER_DIRTY_REGION_RENDERING` | disable-with-0 | enabled | `UiRoot` construction | Controls dirty region rendering default (`UseDirtyRegionRendering`). |
| `INKKSLINGER_CONDITIONAL_DRAW` | disable-with-0 | enabled | `UiRoot` construction | Controls conditional draw scheduling default (`UseConditionalDrawScheduling`). |
| `INKKSLINGER_ALWAYS_DRAW` | enable-with-1 | disabled | `UiRoot` construction | Enables `AlwaysDrawCompatibilityMode` (forces draw every frame). |
| `INKKSLINGER_RENDER_CACHE` | disable-with-0 | enabled | `UiRoot` construction | Controls element render cache default (`UseElementRenderCaches`). |
| `INKKSLINGER_RENDER_CACHE_OVERLAY` | enable-with-1 | disabled | `UiRoot` construction | Enables cached-subtree bounds overlay (`ShowCachedSubtreeBoundsOverlay`). |
| `INKKSLINGER_RENDER_CACHE_COUNTERS` | enable-with-1 | disabled | `UiRoot` construction | Enables cache counter tracing (`TraceRenderCacheCounters`). |

### Input Pipeline

| Variable | Mode | Default | Read Timing | Effect |
|---|---|---|---|---|
| `INKKSLINGER_ENABLE_INPUT_PIPELINE` | disable-with-0 | enabled | each update | Disables `_inputManager.Capture()` + input delta processing when set to `"0"`. |
| `INKKSLINGER_ALWAYS_ROUTE_MOUSEMOVE` | enable-with-1 | disabled | each pointer move | Forces routing `PreviewMouseMove`/`MouseMove` even when hover target is unchanged. |
| `INKKSLINGER_BYPASS_MOVE_HITTEST` | enable-with-1 | disabled | pointer target resolution | Skips non-precise move/wheel hit-test fallback path and reuses hovered target path. |

### CPU Diagnostics Logging

| Variable | Mode | Default | Read Timing | Effect |
|---|---|---|---|---|
| `INKKSLINGER_SCROLL_CPU_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits scroll CPU summaries to `Debug` + `Console`. |
| `INKKSLINGER_MOVE_CPU_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits pointer-move CPU summaries to `Debug` + `Console`. |
| `INKKSLINGER_CLICK_CPU_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits click CPU summaries to `Debug` + `Console`. |
| `INKKSLINGER_WHEEL_ROUTE_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits detailed wheel-target routing traces (`[WheelRoute]`) to `Debug` + `Console`. |
| `INKKSLINGER_LISTBOX_SELECT_CPU_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits ListBox selection/click diagnostics to `Debug` + `Console`. |
| `INKKSLINGER_FILE_LOAD_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits framework file-load diagnostics to `Debug` + `Console`. |
| `INKKSLINGER_POPULATION_PHASE_LOGS` | enable-with-1 | disabled | diagnostics class init | Emits framework population-phase diagnostics to `Debug` + `Console`. |

## Notes

- The framework tracks parity gaps in `TODO.md`.
- Usage is permission-based. See `LICENSE` and `USAGE-PERMISSION-POLICY.md`.

## License and Usage

This repository is source-available under a permission-based model:

- Default: permitted for non-commercial use under `LICENSE`
- Commercial use: subscription, one-time perpetual option, or written waiver/grant (see `LICENSE` and `USAGE-PERMISSION-POLICY.md`)
- If you make money from a product using InkkSlinger, assume commercial use
- Significant contributors may be granted a free lifetime commercial license at maintainer discretion

## Contributing and Governance

- Contributing guide: `CONTRIBUTING.md`
- Governance model: `GOVERNANCE.md`

## Commercial License FAQ

- See `COMMERCIAL-LICENSE-FAQ.md` for scenario-based examples
