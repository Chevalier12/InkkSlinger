# InkkSlinger

A WPF-style UI framework built on MonoGame/XNA-style rendering, with a strict XML markup pipeline, deep runtime telemetry, and a first-class automation/diagnostics system called **InkkOops**.

## What It Is

InkkSlinger reimplements the WPF programming model on top of a MonoGame rendering backend. If you know WPF, the mental model transfers directly: dependency properties, routed events, data binding, styles, templates, triggers, visual states, resource dictionaries, and a measure/arrange layout engine are all present.

It is not a small widget library. It is a broad UI platform with:

- ~470 source files in the core framework
- 85+ controls across the full WPF control surface
- A runtime XML loader (`.xml` markup, not `.xaml`)
- 234 tests covering parity, regressions, layout, rendering, input, and automation
- 86 demo/repro views
- 122 public documentation pages

## Projects

| Project | Purpose |
|---|---|
| `InkkSlinger.UI` | Core framework and runtime (`net9.0`) |
| `InkkSlinger.Tests` | xUnit test suite (`net10.0`) |
| `InkkSlinger.DemoApp` | Demo app and InkkOops launch target |
| `InkkSlinger.XamlNameGenerator` | Roslyn source generator for `x:Name` support |
| `InkkOops.Cli` | Runtime automation CLI host (`net10.0`) |
| `InkkSlinger.WpfLab` | WPF comparison/lab app |
| `InkkSlinger.Template` | Minimal consumer starter app |
| `InkkSlinger.TemplatePack` | `dotnet new` template package |

## Key Conventions

- Markup files use `.xml`, not `.xaml`. Code-behind files are `*.xml.cs`.
- `x:Name` bindings require the `InkkSlinger.XamlNameGenerator` source generator.
- `InkkSlingerXamlStrictMode=true` is enabled — strict XAML validation is enforced at compile time.
- The test project targets `net10.0`; the main framework targets `net9.0`. A .NET 10 SDK is required to build and run tests.
- Views in `InkkSlinger.DemoApp/Views/` are demo and repro surfaces, not product UI.

## Building

```powershell
dotnet build InkkSlinger.sln
```

## Running Tests

```powershell
dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj
```

Run a single test by name:

```powershell
dotnet test InkkSlinger.Tests/InkkSlinger.Tests.csproj --filter "FullyQualifiedName~TestName"
```

The test suite covers WPF parity, app XML compatibility phases, resource and style behavior, automation and accessibility, input/focus/overlay routing, layout and grid/scrolling regressions, retained rendering and dirty-region correctness, telemetry correctness, and InkkOops runtime usage.

## InkkOops — Runtime Automation and Diagnostics

InkkOops is the built-in automation, replay, and diagnostics platform. It is the preferred tool for investigating resize, hover, drag, and retained-render bugs that are difficult to reproduce in unit tests.

It supports commands including click, drag, hover, pointer down/up, wheel, resize window, maximize window, scroll to/by, wait-for-idle, wait-for-element, frame capture, telemetry dump, and assertions. Runs can be recorded and replayed, and each action in a replay can capture per-action diagnostics from registered contributor classes.

Run a named script:

```powershell
dotnet run --project InkkSlinger.csproj -- --inkkoops-script "script-name"
```

Replay a recorded session (pass the `.json` file path, not the folder):

```powershell
dotnet run --project InkkSlinger.csproj -- --inkkoops-recording "C:\path\to\recording.json"
```

See `site/docs/inkkoops.html` for full documentation.

## Architecture

### Object Model and Property System

The core hierarchy is `DependencyObject` → `UIElement` → `FrameworkElement` → `Control`, following the WPF shape exactly.

`DependencyObject` stores effective values in a layered precedence model:

1. Animation
2. Local
3. Template trigger
4. Style trigger
5. Template
6. Style
7. Metadata default / inherited fallback

Property metadata carries flags such as `AffectsMeasure`, `AffectsArrange`, `AffectsRender`, and `Inherits`. The system supports coercion, validation, attached properties, and inheritable property caching by type.

### Tree Model

The framework distinguishes visual and logical trees. `UIElement` exposes both `VisualParent` and `LogicalParent`. Rendering and hit testing walk the visual tree; resource and data-context lookups follow the logical/resource ancestry. Name scope registration and lookup is supported via `FrameworkElement`, backed by the source generator at compile time.

### Layout Engine

Layout runs measure then arrange, coordinated by `UiRootLayoutScheduler`. `FrameworkElement` tracks per-element invalidation state, previous measure inputs, desired size, and arrangement rect. The scheduler is aware of repair loops and layout stability. Known regression areas include multi-pass stability and parent/descendant invalidation interactions.

Layout-facing controls include `Grid`, `Canvas`, `DockPanel`, `StackPanel`, `WrapPanel`, `UniformGrid`, `VirtualizingStackPanel`, `ScrollViewer`, `ScrollBar`, `Track`, `GridSplitter`, `Viewbox`, and popup placement.

### Rendering

`UiRoot` is the central per-frame coordinator. It owns a retained render list, dirty render queues with span coalescing, dirty-region tracking, layout sample capture, overlay and context-menu registries, and update participant orchestration.

Retained rendering, dirty-region rendering, and conditional draw scheduling are all enabled by default. The goal is to minimize redraw cost while preserving correctness. A full-redraw settle period runs after resize.

### Input, Focus, and Commands

`UIElement` declares preview and bubbling routed events for mouse move, mouse down/up, mouse wheel, key down/up, text input, and focus gain/loss. `UiRoot` maintains input-resolution caches for click targets, pointer targets, wheel targets, and keyboard menu scope state, all overlay-aware.

The command system includes `RoutedCommand`, `RoutedUICommand`, `CommandBinding`, `CommandManager`, and editing and navigation commands. Controls implement command-source behavior including command subscriptions and enabled-state updates.

Known fragile areas: hover retargeting, scroll interactions, and overlay dismissal.

### Styling, Templates, and Resources

The styling stack supports `Style`, `Setter`, `EventSetter`, `Trigger`, `DataTrigger`, `MultiTrigger`, `MultiDataTrigger`, `EventTrigger`, trigger actions, `VisualStateManager`, `ImplicitStylePolicy`, and `StyleSelector`.

Templates include `ControlTemplate`, `TemplateBinding`, `TemplateTriggerEngine`, `ItemsPanelTemplate`, `DataTemplate`, `DataTemplateResolver`, and `DataTemplateSelector`.

Resources are backed by `ResourceDictionary` with merged dictionary support, parent-scope traversal, application-level lookup, and dynamic refresh when resource scopes change.

### Data Binding

Binding support includes `Binding`, `BindingExpression`, `MultiBinding`, `PriorityBinding`, `BindingOperations`, `BindingGroup`, collection views with grouping and sorting, value converters, multi-value converters, and validation rules with error models and exception filter callbacks.

### XML Loading

The runtime XML loader lives under `UI/Xaml/Core` across 16 partial source files. It resolves types across the UI assembly, the current code-behind assembly, and the entry assembly. It caches string-documents, file-documents, type maps, resource references, and enum values. Strict mode validation is compile-time enforced.

### Text and Documents

Text is a full subsystem: `TextLayout`, access text parsing, a document model with editing buffers and selection, clipboard integration, a document layout engine, and rich text box feature slices covering formatting, list operations, navigation, and table operations.

### Animation

The animation system includes timeline base types, storyboards, key frames, easing, property path resolution, and integration with the dependency property system via animation sinks on freezables.

### Telemetry

39 telemetry snapshot types are distributed across the framework, covering layout timing, rendering, animation, styles, effects, hit testing, UI element invalidation, and individual controls including `Border`, `Button`, `Calendar`, `ComboBox`, `Grid`, `Panel`, text controls, `UserControl`, and `WrapPanel`. Telemetry is treated as a standard debugging tool, not an optional add-on.

## Controls

The built-in control surface spans the full WPF range:

- **Base:** `Control`, `ContentControl`, `ItemsControl`, `Selector`, `MultiSelector`, `Panel`, `Decorator`
- **Buttons:** `Button`, `CheckBox`, `RadioButton`, `RepeatButton`, `ToggleButton`, `Thumb`
- **Containers:** `Window`, `UserControl`, `Page`, `Frame`, `Popup`, `Expander`, `GroupBox`, `ToolBar`, `ToolBarTray`, `StatusBar`, `Viewbox`, `GridSplitter`, `ToolTip`
- **Data:** `DataGrid` with row, cell, header, and details presenter types
- **Inputs:** `TextBox`, `PasswordBox`, `RichTextBox`, `Slider`, `Calendar`, `DatePicker`
- **Item controls:** `ComboBox`, `ListBox`, `ListView`, `Menu`, `MenuItem`, `TabControl`, `TreeView`
- **Panels:** `Grid`, `Canvas`, `DockPanel`, `StackPanel`, `WrapPanel`, `UniformGrid`, `VirtualizingStackPanel`
- **Primitives:** `Border`, `Label`, `Image`, `TextBlock`, `Separator`, `ProgressBar`, `RenderSurface`, shape types
- **Scrolling:** `ScrollViewer`, `ScrollBar`, `Track`

## Documentation

Full public documentation is available under `site/docs/`, including:

- WPF Bootcamp
- Getting Started guide
- RenderSurface concept series
- InkkOops reference
- Per-control reference pages (85 controls)
