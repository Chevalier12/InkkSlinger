# InkkSlinger UI Framework

[![.NET](https://img.shields.io/badge/.NET-9%20%2F%2010-512BD4?logo=dotnet&logoColor=white)](#build-and-test)
[![MonoGame](https://img.shields.io/badge/MonoGame-DesktopGL-FF7F50)](#vision)
[![Tests](https://img.shields.io/badge/tests-290%20passed%20%7C%201%20failed%20%7C%201%20skipped-yellow)](#build-and-test)
[![WPF Parity](https://img.shields.io/badge/WPF%20parity-ongoing-blue)](#parity-matrix)

InkkSlinger is a custom UI framework for MonoGame/DesktopGL with a single primary objective:

**achieve practical WPF parity as closely as possible** across APIs, behavior, composition model, and authoring workflow.

This repository is framework-first.  
The files in `Views/` are validation/demo surfaces used to exercise framework features and regressions, not the end product.

## Why This Exists

WPF provides a powerful UI model, but it is tied to its own runtime stack and not directly available in game-oriented rendering pipelines like MonoGame/DesktopGL.

This project exists to close that gap by bringing a familiar WPF-style model into a real-time game loop:

- build complex desktop-style UI in a MonoGame host
- preserve WPF-like composition and authoring patterns
- validate behavior against parity expectations through tests

## Parity Snapshot

| Category | Implemented | Planned / Open |
|---|---:|---:|
| Tracked WPF controls | 65 | 12 |
| Core parity areas (DP, events, input, layout, markup, resources/styles, triggers, binding, templates, animation, virtualization, rendering/invalidation, tooling) | 12 | 0 |
| Deeper parity tracks (menu/commanding depth, rich text/doc stack, adorner depth, full-edge parity) | 0 | 4 |

## Vision

Bring a WPF-like development model into a game/render-loop environment by implementing:

- dependency properties and metadata-driven behavior
- routed events and focus/input semantics
- layout and measure/arrange style composition
- styling, resources, triggers, and templating
- MVVM-style binding primitives
- animation/timeline orchestration
- broad control coverage with parity-focused tests

## WPF Parity Position

Parity is treated as a long-running engineering target, not a marketing claim.  
The current implementation is already deep, but intentionally explicit about gaps.

## Parity Matrix

| Area | Status | Detail |
|---|---|---|
| Dependency property system | Implemented | `DependencyObject`, `DependencyProperty`, metadata/value-source model is present. |
| Routed events | Implemented | Routed event registration, args types, and event manager infrastructure are in place. |
| Input/focus/capture | Implemented | Focus management and input processing pipeline are integrated with UI update loop. |
| Layout system | Implemented | Measure/arrange pipeline is implemented across core panels and controls. |
| Markup loading | Implemented | XML markup loading via `XamlLoader`, including diagnostics with line/position context. |
| Resources + styles | Implemented | `x:Key`, static resource lookup, implicit styles, `BasedOn`, style precedence behavior. |
| Trigger system | Implemented | `Trigger`, `DataTrigger`, `MultiDataTrigger`, enter/exit action support. |
| Binding system | Implemented | `Path`, `Source`, `ElementName`, `RelativeSource`, plus lifecycle rebinding behavior. See “Not Yet Implemented / Out of Scope” for omitted WPF binding features (converters, MultiBinding, validation, etc.). |
| Templates | Implemented | `ControlTemplate`, `DataTemplate`, template selection/resolution pipeline. |
| Animation system | Implemented | Storyboards, timelines, keyframes, easing primitives, trigger-driven animation hooks. |
| Virtualization | Implemented | `VirtualizingStackPanel` and virtualization behavior for large collections. |
| Invalidation/render efficiency | Implemented | Dirty-region tracking and cached-frame composition in `UiRoot`. |
| Hit testing | Implemented | Hit testing is supported (`VisualTreeHelper.HitTest`, `IsHitTestVisible`). |
| Drawing/geometry | Implemented | Shape/geometry primitives and transforms are implemented (`UI/Geometry`, `UI/Controls/Shape`). Rendering fidelity is WPF-inspired but not guaranteed to match every WPF edge behavior. |
| Basic text | Implemented | Text rendering/layout primitives exist (`UI/Text`, wrapping, selection/caret behaviors in text controls). Advanced typography/IME/document features are explicitly out of scope for now. |
| Tooling support | Implemented | XML schemas in `Schemas/` + `x:Name` source generation. |
| Items/selection system | Implemented | Items + selection infrastructure exists across list/tree/grid controls, validated by tests. Some broader WPF data features (views/grouping/sorting/etc.) are out of scope for now. |
| Popup/windowing | Partial | `Popup`, `ContextMenu`, and `Window` exist; deeper edge parity remains ongoing. |
| Menu/commanding parity depth | Partial | Core routed commanding (`RoutedCommand`, `CommandBinding`, `InputBinding`, `KeyGesture`) is implemented and wired through menu/button demo paths; deeper WPF edge parity (navigation/access keys/behavior nuances) remains ongoing. |
| Adorner layer depth | Partial | Adorner primitives exist; deeper parity (composition behaviors and layout nuances) remains open. |
| Rich document text stack | Planned | `RichTextBox` and flow-document-level parity not implemented yet. |
| Full WPF parity | Ongoing | Objective is closest practical parity over time, validated through tests and real behavior. |

## Not Yet Implemented / Out of Scope (For Now)

This project aims for practical WPF parity, but the term “WPF parity” is easy to
misread as “everything in WPF exists here.” It does not.

Below is a detailed list of major WPF areas/features that are either not
implemented yet, implemented only partially, or intentionally out of scope right
now. If you need any of these, please treat them as open work unless explicitly
stated otherwise in code/tests.

### Data Binding: Missing WPF Features

- Value converters: `IValueConverter`, `IMultiValueConverter`
- `MultiBinding` (multi-source binding)
- Binding validation stack: `Validation`, `ValidationRule`, `Binding.ValidationRules`
- `IDataErrorInfo` and `INotifyDataErrorInfo` pipelines
- `UpdateSourceExceptionFilter`-style hooks and WPF-specific binding error behaviors
- WPF binding diagnostics/tracing feature parity
- WPF `CollectionView` integration and collection view behaviors (current item, filtering, sorting, grouping)
- `PriorityBinding`
- `BindingGroup`
- `MultiDataTrigger` does exist, but WPF-level binding + trigger interaction edge cases are not claimed as complete

### Commanding: Missing WPF Features

- Core routed commanding is implemented; full WPF routed-commanding edge parity remains open
- WPF `ICommandSource` ecosystem parity across all controls/edge cases
- Full menu keyboard interaction parity (accelerators, access keys, deeper navigation behaviors)

### Markup / XAML: Missing WPF Features

Markup loading exists, but WPF XAML is an ecosystem. Not currently claimed:

- Full WPF XAML language feature parity (`MarkupExtension` breadth, type converters parity, XAML namespace breadth)
- `DynamicResource` semantics and WPF-level resource invalidation behaviors (only what is implemented is supported)
- Full design-time tooling parity (WPF designer, blend behaviors, etc.)
- XAML compilation parity (this project uses runtime loading and its own pipeline)

### Styles / Resources / Templating: Missing WPF Features

- Full WPF resource dictionary merge behavior parity (`MergedDictionaries`-level edge behaviors)
- WPF theme dictionary system parity and theme fallback rules
- Full Visual State Manager parity (`VSM` is present as primitives, but not claimed as WPF-complete)
- Template binding parity beyond what is implemented and tested (WPF has many subtle ordering/precedence rules)

### Text / Typography: Missing WPF Features

“Basic text” exists. The following are not claimed:

- IME composition and WPF-level text services integration
- Advanced typography shaping/bidi behaviors at WPF fidelity
- Flow document stack (FlowDocument, `Paragraph`, `Run`, `Inline`, etc.)
- Rich document controls (`RichTextBox`) and document viewers
- Printing and document pagination parity

### Accessibility / Automation: Missing WPF Features

- UI Automation / `AutomationPeer` infrastructure
- Accessibility semantics parity (screen reader integration, automation tree parity)

### Rendering / Graphics: Missing WPF Features

Rendering is WPF-inspired in structure, but not a pixel-identical WPF clone:

- WPF hardware acceleration pipeline parity (WPF is DirectX-based; this project is MonoGame-based)
- Pixel-identical text and shape rasterization parity with WPF
- Full WPF effect stack parity (BlurEffect/DropShadowEffect-style behaviors)
- Full WPF composition visual layer parity (WPF has a dedicated composition engine)

### Layout: Missing WPF Features

Layout is implemented and widely used, but WPF layout has many edge rules:

- Layout rounding and DPI rounding parity (beyond what is implemented)
- Every WPF panel/control layout edge case and rounding rule
- Full attached layout property behavior parity across all controls (only what is implemented is supported)

### Input / Focus: Missing WPF Features

Input/focus/capture is implemented, but WPF’s input stack is large:

- WPF keyboard navigation parity in all edge cases (focus scopes, access keys, full traversal semantics)
- Tablet/stylus input stack parity
- Touch input stack parity (manipulations, inertia, etc.)

### DataGrid / Virtualization: Missing WPF Features

Virtualization exists, but:

- WPF virtualization modes, recycling semantics, and all edge cases are not claimed as complete
- Full DataGrid feature parity (grouping, column types, edit pipelines, validation, clipboard, etc.)

### Media / Interop: Missing WPF Features

- Media stack parity (`MediaElement`) beyond stub/non-existence
- Web/content interop parity (`Frame`, navigation stack)

### General WPF “Surface Area” Not Tracked

The repository tracks a large set of controls and core behaviors, but it does
not currently claim parity across WPF’s full surface area, including:

- Attached behaviors ecosystem parity, `Freezable` semantics, and many framework service patterns
- Full dependency property metadata option parity and every precedence rule edge case
- Localization, globalization, and resource culture behaviors at WPF parity

## Control Coverage Snapshot

Checklist source of truth: `TODO.md` (`## WPF Control Coverage`).

- Implemented: `65`
- Not yet implemented: `12`
- Total tracked controls: `77`

Implemented controls:

`Border`, `Button`, `Canvas`, `CheckBox`, `ComboBox`, `ComboBoxItem`, `ContentControl`, `ContentPresenter`, `ContextMenu`, `Control`, `DataGrid`, `DataGridCell`, `DataGridColumnHeader`, `DataGridDetailsPresenter`, `DataGridRow`, `DataGridRowHeader`, `Decorator`, `DockPanel`, `Expander`, `Grid`, `GridSplitter`, `GroupBox`, `HeaderedContentControl`, `HeaderedItemsControl`, `Image`, `ItemsControl`, `Label`, `ListBox`, `ListBoxItem`, `ListView`, `ListViewItem`, `Menu`, `MenuItem`, `Panel`, `Popup`, `ProgressBar`, `RadioButton`, `RepeatButton`, `ResizeGrip`, `ScrollBar`, `ScrollViewer`, `Separator`, `Slider`, `StackPanel`, `StatusBar`, `StatusBarItem`, `TabControl`, `TabItem`, `TextBlock`, `TextBox`, `Thumb`, `ToggleButton`, `ToolBar`, `ToolBarOverflowPanel`, `ToolBarPanel`, `ToolBarTray`, `ToolTip`, `TreeView`, `TreeViewItem`, `UniformGrid`, `UserControl`, `Viewbox`, `VirtualizingStackPanel`, `WrapPanel`, `Window`

Open controls:

`AccessText`, `Calendar`, `DatePicker`, `DocumentViewer`, `Frame`, `GroupItem`, `InkCanvas`, `InkPresenter`, `MediaElement`, `Page`, `PasswordBox`, `RichTextBox`

## Architecture Overview

### Host Layer

- `Program.cs` selects runtime mode.
- `Game1.cs` runs the MonoGame update/draw lifecycle and initializes UI root surfaces.

### UI Core

- `UI/Core/`: dependency property system, value precedence, metadata/callback infrastructure, dispatcher.
- `UI/Events/`: routed event registration/dispatch model and event args.
- `UI/Input/`: keyboard/mouse routing, focus behavior, capture/cursor state handling.

### Visual/Layout/Rendering

- `UI/Controls/`: framework and concrete controls.
- `UI/Managers/LayoutManager.cs`: layout orchestration for visual tree updates.
- `UI/Managers/UiRoot.cs`: top-level update/draw pipeline, redraw decisioning, dirty-region behavior.
- `UI/Rendering/`: draw helpers and dirty-region tracking primitives.

### Authoring/Composition

- `UI/Xaml/XamlLoader.cs`: XML parsing, object construction, property assignment, template/style/binding hookup.
- `Schemas/`: XML schema support for editor assistance and authoring validation.

### MVVM/Binding/Styling/Templating

- `UI/Binding/`: bindings and expression plumbing.
- `UI/Styling/`: styles, setters, triggers, condition/action behavior.
- `UI/Templating/`: control/data templates and resolution logic.
- `UI/Resources/`: resource dictionary stack and resolution behavior.

### Animation

- `UI/Animation/`: storyboards, timeline model, keyframes, easing, animation manager/property path resolution.

### Source Generation

- `InkkSlinger.XamlNameGenerator/`: incremental Roslyn generator that produces strongly-typed backing members for `x:Name` references in XML views.

### Tests

- `InkkSlinger.Tests/`: parity/regression test suite spanning controls, layout, rendering invalidation, templating, binding, animation, and source generation behavior.

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

- Markup files use `.xml` (WPF-inspired structure with `x:` support).
- `x:Name` is supported and mapped to generated members.
- Schemas are included for local editor assistance:
  - `Schemas/InkkSlinger.UI.xsd`
  - `Schemas/Xaml2006.xsd`

## Build and Test

Prerequisites:

- .NET SDK 10.x recommended

Build:

```powershell
dotnet restore InkkSlinger.sln
dotnet build InkkSlinger.sln -v minimal
```

Run tests:

```powershell
dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj -v minimal
```

## Running the Host Application

Default host mode:

```powershell
dotnet run --project InkkSlinger.csproj
```

Alternate validation surfaces:

```powershell
dotnet run --project InkkSlinger.csproj -- --main-menu
dotnet run --project InkkSlinger.csproj -- --window-demo
dotnet run --project InkkSlinger.csproj -- --paint-shell
dotnet run --project InkkSlinger.csproj -- --commanding-demo
```

Current default launch surface is the commanding demo when no explicit mode flag is provided.

## Current Parity Focus Areas

Based on `TODO.md`, priority remaining areas include:

- deeper menu and commanding behavior parity
- rich document text stack (`RichTextBox` + flow-document style concepts)
- additional advanced parity edges where WPF behavior is broader

## Repository Layout

```text
InkkSlinger/
|-- InkkSlinger.sln
|-- README.md
|-- LICENSE
|-- TODO.md
|-- USAGE-PERMISSION-POLICY.md
|-- InkkSlinger.csproj
|-- Program.cs
|-- Game1.cs
|-- UI/
|-- Schemas/
|-- Views/
|-- ViewModels/
|-- Content/
|-- InkkSlinger.XamlNameGenerator/
|-- InkkSlinger.Tests/
```

## Notes

- The framework is intentionally explicit about parity gaps and tracks them in `TODO.md`.
- Usage is permission-based. See `LICENSE` and `USAGE-PERMISSION-POLICY.md`.

## License and Usage

This repository is source-available under a permission-based model:

- Default: permitted for non-commercial use under `LICENSE`.
- Commercial use is available via a paid monthly subscription, a one-time perpetual option (see `LICENSE`), or a written waiver/grant (see `USAGE-PERMISSION-POLICY.md`). If you make money from a product that uses InkkSlinger, assume commercial use.
- Subscribers get updates and priority issue support. Response-time target: 1-2 working days, with an explicit status update if the maintainer cannot meet that target due to schedule/medical/etc. See `LICENSE` for details.
- Significant contributors may be granted a free lifetime commercial license at maintainer discretion (see `LICENSE` / `USAGE-PERMISSION-POLICY.md`).
- If a commercial waiver/grant is given, the maintainer may list your studio/project in this repository (for example in `README.md`) as an approved user.

## Contributing and Governance

- Contributing guide: `CONTRIBUTING.md`
- Governance model: `GOVERNANCE.md`

## Commercial License FAQ

- See `COMMERCIAL-LICENSE-FAQ.md` for 10 concrete scenarios.
