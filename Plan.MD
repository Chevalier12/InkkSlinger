# App.xml Theme Compatibility Plan

## Goal
Make the current external `App.xml` theme load and apply in a WPF-like workflow, with predictable behavior across the controls used by the catalog.

## Current Baseline
- Global app resource bootstrap now exists (`App.xml` is loaded at startup into `UiApplication.Current.Resources`).
- External resource dictionaries and merged dictionaries are supported.
- Styles, triggers, and control templates are partially supported.

## Confirmed Gaps (from current `App.xml` + framework inspection)

### P0: Loader/Parser blockers (theme cannot fully load as-is)
1. Missing element type support for `<Color>` resources.
- Why: XAML type map resolves only public types in the UI assembly + shape aliases; `Color` (XNA struct) is not an XAML object type in the map.
- Evidence: `XamlLoader.BuildTypeMap()` only maps UI assembly types and shape aliases.

2. Missing element type support for `<SolidColorBrush>` resources.
- Why: No brush type exists in the framework and no `SolidColorBrush` type is mapped.
- Impact: All keyed brush resources in `App.xml` fail.

3. `Setter.Value` object-element syntax is not supported.
- Why: `BuildSetter` currently requires `Value` attribute and does not parse `<Setter.Value> ... </Setter.Value>`.
- Impact: Every template setter in the theme fails (`Template`, `ItemContainerStyle`, etc.).

4. `TemplateBinding` markup extension is not parsed by loader.
- Why: No parser path for `{TemplateBinding ...}` in attribute values.
- Impact: Most control templates in the theme fail.

5. `Thickness` parser does not support WPF 2-value form.
- Why: Parser accepts only 1 or 4 values.
- Impact: entries like `Padding="20,10"` fail.

### P1: Missing dependency properties used by this theme
6. Missing style-settable properties in framework DPs:
- `CornerRadius`
- `Visibility`
- `FontFamily`
- `FontSize`
- `FontWeight`
- `Cursor`
- `SnapsToDevicePixels`
- `ItemContainerStyle`
- `TextDecorations`
- `Focusable`
- `RecognizesAccessKey`

7. DataGrid-specific properties used in theme are missing:
- `GridLinesVisibility`
- `HeadersVisibility`
- `HorizontalGridLinesBrush`
- `VerticalGridLinesBrush`
- `AlternatingRowBackground`

### P1: Missing/invalid target types in templates
8. `GridViewRowPresenter` is referenced in `App.xml` but is not implemented as a framework type.

9. `Window` is not a styleable UI control in this framework (wrapper around native game window), so WPF-style `<Style TargetType="{x:Type Window}">` has no meaningful application target.

10. `Hyperlink` in this framework is a document model text element, not a `DependencyObject`/`FrameworkElement`; WPF-like style setters/triggers do not map to current architecture.

### P2: Architectural parity gaps (behavior differences)
11. Brush-based styling model is missing.
- Framework visual properties are mostly `Color` DPs, while the theme is brush-centric.
- Even with parser fixes, many WPF brush patterns remain incompatible without conversion or native brush DPs.

12. Implicit style behavior is centered on `Control` path.
- WPF-wide style behavior across all `FrameworkElement`-like objects is not complete parity.

## Likely Additional Gaps (should be validated during implementation)
1. Template trigger semantics for templated-parent vs named target may differ from WPF edge-cases.
2. Control-specific template parts may not fully match WPF conventions even after parser parity.
3. Some WPF visual-only properties may need no-op compatibility DPs to avoid loader failures.

## Implementation Plan (phased)

### Phase 1: Make current App.xml parse and load (hard blockers)
1. Add XAML primitive/object support for `Color` resources.
2. Introduce minimal `SolidColorBrush` support (or compatibility coercion path).
3. Add `<Setter.Value>` parsing support in `BuildSetter`.
4. Implement `{TemplateBinding ...}` parsing and wiring into control templates.
5. Expand `Thickness` parser to WPF forms (1,2,4; optionally 3 for compatibility safety).

Exit criteria:
- `App.xml` loads without parser exceptions.

### Phase 2: Property compatibility required by this theme
Phase 1 parser/load blockers are complete.
Phase 2 focuses on runtime property compatibility for controls used by `App.xml`, so themed values are not only parsed, but visibly applied.

Summary:
1. Implement the property-compatibility layer with runtime behavior parity for the controls used by `App.xml`.
2. Keep parser strictness unchanged outside explicit compatibility shims.
3. Keep Phase 3 architecture targets out of scope (`Window` / `Hyperlink` runtime styling parity remains deferred).

Public API / interface changes:
1. `ComboBoxItem` is a real selectable item container (inherits `ListBoxItem`) while preserving existing `Text` behavior and selection/background semantics.
2. No new compatibility DPs are introduced unless a concrete missing `App.xml` property is discovered during implementation.
3. Internal-only test helpers are allowed for DataGrid header/gridline assertions.

Implementation:
1. Complete `ItemContainerStyle` runtime support in `ItemsControl`:
- Apply `ItemContainerStyle` on generated container prepare/regenerate paths.
- Respect explicit per-container local style overrides.
- React to `ItemContainerStyle` changes by regenerating/refreshing realized containers.
- Ensure shared behavior works for `ListBox`, `ComboBox`, `ListView`, and `TreeView`.
2. Implement full ComboBox item-container host path in `ComboBox` + `ComboBoxItem`:
- `ComboBox` generates `ComboBoxItem` containers (not plain labels).
- Dropdown population uses real `ComboBoxItem` containers so `TargetType=ComboBoxItem` styles apply directly.
- Keep selection/index synchronization between combo and dropdown list.
- Preserve popup behavior and outside-click dismissal behavior.
3. Add generic hover-host compatibility in `UiRootInputPipeline`:
- Promote hover target to nearest interactive container host (not only `Button`).
- Toggle `IsMouseOver` consistently for themed hosts (`ListBoxItem`, `ListViewItem`, `ComboBoxItem`, `DataGridRow`, `TabItem`, `TreeViewItem`) plus existing text-input/button flow.
- Keep pointer-target caching and fast paths unchanged.
4. Make DataGrid theme properties affect runtime visuals:
- Wire `HeadersVisibility` into measure/arrange and row/column header visibility.
- Wire `GridLinesVisibility`, `HorizontalGridLinesBrush`, and `VerticalGridLinesBrush` into row/cell gridline behavior.
- Keep `AlternatingRowBackground` and `RowBackground` striping, but allow style-triggered `DataGridRow.Background` to override when active.
- Ensure DP changes trigger correct invalidation/rebuild paths.
5. Keep compatibility/no-op policy explicit for this phase:
- `Cursor`, `SnapsToDevicePixels`, `RecognizesAccessKey`, `Focusable`, and typography metadata DPs remain load-safe and behaviorally no-op unless already implemented.
- Do not expand into Phase 3 runtime styling architecture.

Test plan:
1. Add `AppXmlPhase2CompatibilityTests`:
- `ItemContainerStyle` applies to generated `ListBox` containers and affects visible state.
- `ComboBox` dropdown uses `ComboBoxItem` containers and honors `ItemContainerStyle` setters/triggers.
- Hovering item containers toggles `IsMouseOver` and activates style triggers.
- `DataGrid HeadersVisibility=Column` hides row headers and keeps column headers.
- DataGrid gridline visibility/brush properties map to effective line-render config.
- DataGrid row hover style trigger updates effective row background.
2. Extend existing ComboBox popup parity tests for dropdown container type/style behavior.
3. Keep existing `AppXml*` tests green as regression baseline.

Exit criteria:
1. `dotnet test InkkSlinger.sln --filter "FullyQualifiedName~AppXml|FullyQualifiedName~ComboBox|FullyQualifiedName~ListBox|FullyQualifiedName~DataGrid|FullyQualifiedName~TabControl|FullyQualifiedName~TreeView"` passes.
2. `App.xml` loads with no unknown-property failures.
3. Representative styled controls visibly reflect configured values (especially list item containers and DataGrid behavior).

### Phase 3: Type and architecture alignment
1. Implement `GridViewRowPresenter` or provide an equivalent supported presenter and mapping strategy.
2. Decide policy for `Window` styling:
- Option A: Introduce a styleable UI window/root control abstraction.
- Option B: Document unsupported and ignore `Window` styles during app resource load.
3. Decide policy for `Hyperlink` styling:
- Option A: Add styleable text-element layer.
- Option B: Document unsupported and avoid/strip such styles.

Exit criteria:
- No unsupported target type crashes from `App.xml`.

### Phase 4: WPF-like behavior polish
Phase 1-3 are implemented. This phase focuses on parity polish, coercion consistency, and diagnostics quality without changing the supported control surface.

Summary:
1. Unify implicit-style behavior for non-`Control` style hosts where parity is expected.
2. Centralize brush/color coercion so all value-ingress paths behave consistently.
3. Upgrade XAML diagnostics from line-only failures to actionable element/property diagnostics.

Scope guardrails:
1. No new control families or major rendering architecture changes.
2. No behavioral expansion into unsupported WPF subsystems (custom markup extensions, full DynamicResource in setters/triggers, etc.).
3. Preserve strict loader behavior by default; diagnostics improve failure quality, not silent fallback.

Implementation workstreams:
1. Implicit style parity beyond `Control`
- Introduce a shared implicit-style policy path for:
  - `FrameworkElement` instances that are not `Control` (where style is a valid DP and resources are scoped).
  - `TextElement`-based document nodes already participating in style application (`Hyperlink`, etc.).
  - Existing `Window` implicit-style behavior so precedence semantics match the shared policy.
- Ensure style precedence remains stable:
  - Local `Style` always wins.
  - Implicit style can replace only previous implicit style owned by the engine.
  - Re-evaluate on resource scope changes (local, ancestor, application dictionary changes).
- Target files:
  - `UI/Controls/Base/FrameworkElement.cs`
  - `UI/Controls/Base/Control.cs`
  - `UI/Controls/Containers/Window.cs`
  - `UI/Text/Documents/DocumentModel.cs`
  - `UI/Resources/Core/ResourceResolver.cs` (only if scope traversal adjustments are required)

2. Brush/color coercion consistency
- Move coercion decisions into one shared utility used by:
  - XAML loader assignment paths.
  - Style setter and trigger application.
  - Template binding assignments.
  - Dynamic resource updates (`SetResourceReference` refresh path).
- Standardize minimum coercion matrix:
  - `SolidColorBrush -> Color`
  - `Color -> Brush` (materialize `SolidColorBrush`)
  - Respect nullable targets where applicable.
- Keep coercion explicit and deterministic:
  - If coercion is impossible, throw a typed loader/style exception with property context.
  - Do not introduce silent conversion for unrelated types.
- Target files:
  - `UI/Core/DependencyProperties/DependencyObject.cs`
  - `UI/Xaml/Core/XamlLoader.cs`
  - `UI/Controls/Base/Control.cs`
  - `UI/Controls/Base/FrameworkElement.cs`
  - New helper under `UI/Core/DependencyProperties/` or `UI/Styling/Core/` (single source of truth)

3. Diagnostics precision for unsupported/invalid constructs
- Add structured diagnostics metadata with:
  - Error code/category (`UnsupportedConstruct`, `InvalidValue`, `UnknownProperty`, etc.).
  - Element type/name.
  - Property/attribute name when relevant.
  - Line and position.
  - Short remediation hint.
- Keep thrown exceptions, but include diagnostic payload in exception text and internal diagnostic record.
- Add an optional diagnostics sink for tests and tooling to assert exact errors without matching full strings.
- Target files:
  - `UI/Xaml/Core/XamlLoader.cs`
  - `UI/Diagnostics/` (new diagnostic record/collector types)

Test plan:
1. Add `InkkSlinger.Tests/AppXmlPhase4CompatibilityTests.cs` with:
- Implicit style application for non-`Control` `FrameworkElement` hosts from app resources.
- Implicit style re-evaluation when merged dictionaries are added/removed at runtime.
- `Hyperlink` implicit style precedence (local style override, resource refresh behavior).
- Style setter using `SolidColorBrush` into `Color` DP succeeds via shared coercion.
- Style/template/resource path using `Color` into `Brush` DP succeeds via shared coercion.
- Dynamic resource update maintains coercion invariants on each refresh.
- Unsupported construct surfaces diagnostic with element + property + line/position.
2. Extend existing loader tests for diagnostic payload assertions (not message-only assertions).
3. Keep all `AppXmlPhase1/2/3CompatibilityTests` green as regression baseline.

Verification command:
1. `dotnet test InkkSlinger.sln --filter "FullyQualifiedName~AppXml|FullyQualifiedName~XamlLoader|FullyQualifiedName~Style|FullyQualifiedName~Resource"`

Exit criteria:
1. Implicit style behavior is consistent across `Control`, `Window`, and supported non-`Control` style hosts.
2. Brush/color coercion behavior is identical across loader, style, template, and dynamic-resource pipelines.
3. Unsupported/invalid XAML failures include actionable element/property/line diagnostics.
4. Phase 1-3 compatibility tests remain green with no behavioral regressions.

## Test Plan
1. Parser tests for `App.xml`:
- `Color` resource entries.
- `SolidColorBrush` entries.
- `Setter.Value` object content.
- `TemplateBinding` in templates.
- Thickness 2-value parsing.

2. Runtime style tests:
- Implicit button style from app resources.
- Template-trigger state changes (`IsMouseOver`, `IsPressed`, `IsEnabled`).
- `ItemContainerStyle` application in list controls.
- DataGrid themed values where implemented.

3. Compatibility tests:
- Unsupported targets (`Window`, `Hyperlink`) produce controlled diagnostics rather than hard crashes.

## Recommended Execution Order
1. Phase 1 (unblocks loading).
2. Phase 2 (unblocks most visual application).
3. Phase 3 (resolve target-type mismatches).
4. Phase 4 (parity polish and diagnostics).

## Confidence
- High confidence on P0 and P1 gaps listed above; these are directly evidenced by current `App.xml` constructs and current framework/parser capabilities.
- Medium confidence on parity gaps in Phase 4 (these are behavioral and need staged verification after Phase 1-3 are complete).
