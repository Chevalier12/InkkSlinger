# Rich Text Future Work TODO (RTF_TODO)

Last updated: 2026-02-18

This file captures the remaining work after the initial RichText foundation landed.
It is intentionally detailed so implementation can continue without needing prior chat context.

## Current Reality Snapshot

Implemented already:
- `ITextInputControl` abstraction and `UiRootInputPipeline` refactor for text input controls.
- `RichTextBox` control exists and is wired into input, render-cache policy, XAML type resolution, schema, and demo launch.
- Core document model exists (`FlowDocument`, `Paragraph`, `Run`, spans, list/table types, containers).
- Basic serialization and plain-text document editing helpers exist.
- Basic undo/redo exists (serialized-snapshot based, not full granular transaction model).
- Clipboard supports plain text + typed payload store (internal rich format key added).
- Baseline tests for model/serializer/undo/richtextbox/xaml loading were added and are green.

Not yet implemented at required parity depth:
- Full transactional grouped undo model (semantic, granular units).
- Rich line layout engine for formatting/lists/tables beyond plain text flattening.
- Complete RichTextBox interaction semantics (selection granularity, navigation parity depth).
- Editing command system parity (`EditingCommands`-like routed command surface).
- Strict full XAML validation for all rich document semantics/invalid structures.
- Full rich clipboard range-fidelity semantics.
- Advanced list/table/UI container/hyperlink behavior parity.
- Rich diagnostics/perf hardening for large documents.

---

## M2 (Remaining): Transaction Engine + Grouped Undo/Redo

### Goal
Replace snapshot-based undo with operation-level immutable units and grouping semantics that match typing/edit workflows.

### Required implementation

1. Add immutable operation model:
- New folder: `UI/Text/Documents/Operations/`
- Types:
  - `IDocumentOperation`
  - `InsertTextOperation`
  - `DeleteTextOperation`
  - `ReplaceTextOperation`
  - `ApplyInlineFormatOperation`
  - `SplitParagraphOperation`
  - `MergeParagraphOperation`
  - `InsertBlockOperation`
  - `RemoveBlockOperation`
  - `ListIndentOperation`
  - `ListOutdentOperation`
  - `TableCellSplitOperation`
  - `TableCellMergeOperation`
- Each operation must carry:
  - Target identity/path
  - Before/after payload (smallest required diff, not whole document)
  - `Apply()` and `Revert()`

2. Replace current undo unit behavior:
- File to rework: `UI/Text/Documents/DocumentEditing.cs`
- Introduce:
  - `DocumentEditSession`
  - `BeginTransaction(string reason, GroupingPolicy policy)`
  - `CommitTransaction()`, `RollbackTransaction()`
- Grouping policies:
  - `TypingBurst` (coalesce consecutive character inserts)
  - `DeletionBurst`
  - `FormatBurst`
  - `StructuralAtomic` (never auto-coalesce with others)

3. Add typing-window coalescing:
- Time-window based default: 400ms between inserts.
- Break group on:
  - caret jump
  - selection change
  - command type change
  - paste/cut/undo/redo/format command

4. Add redo invalidation correctness:
- Any new committed operation after undo must clear redo stack.

5. Remove serializer dependency from standard undo path:
- Keep snapshot fallback only for catastrophic unknown operation types (debug fallback).

### Edge cases to handle
- Undo across paragraph split + subsequent typing.
- Undo formatted range when partial selection intersects existing formatting containers.
- Undo operations when selection is collapsed/expanded transitions.
- Undo/redo after paste with multi-line content.

### Tests required
- New file: `InkkSlinger.Tests/DocumentUndoTransactionTests.cs`
- Cases:
  - grouped typing forms single undo unit
  - caret move breaks group
  - undo then new edit clears redo
  - mixed structure+text transaction undoes atomically
  - rollback leaves document unchanged

### Acceptance gate
- No snapshot-based undo in normal editing path.
- Deterministic undo/redo for text + structure operations.
- All new tests pass; existing tests stay green.

---

## M3 (Remaining): Rich Layout + Rendering Pipeline

### Goal
Replace flattened plain-text rendering with true rich layout/runs supporting formatting inheritance, lists, and tables.

### Required implementation

1. Build document layout engine:
- New folder: `UI/Text/Layout/`
- Types:
  - `DocumentLayoutEngine`
  - `DocumentLayoutResult`
  - `DocumentLayoutLine`
  - `DocumentLayoutRun`
  - `DocumentLayoutBlockMetrics`
  - `DocumentViewportLayoutCache`
- Inputs:
  - `FlowDocument`, available width, font defaults, wrapping, DPI scale (if available later)
- Outputs:
  - positioned runs with style info
  - caret hit-testing map
  - selection geometry map

2. Style inheritance and run shaping:
- Resolve effective style chain for inline tree:
  - base from `RichTextBox` defaults
  - then span containers (`Bold/Italic/Underline/Hyperlink`)
- Add text decoration rendering for underline.

3. List rendering:
- Marker generation:
  - unordered bullet
  - ordered decimal
- Marker layout aligned with paragraph baseline.
- Nested list indentation rules.

4. Table layout:
- Compute column widths from cell content constraints.
- Respect `RowSpan`, `ColumnSpan`.
- Render cell borders/backgrounds (if style exists later).
- Provide caret navigation boundaries at cell edges.

5. Dirty-range invalidation:
- Keep and diff previous layout result.
- Invalidate only changed run bounds for typing/selection/caret blink when possible.

### Edge cases to handle
- Long unbreakable tokens in rich spans.
- Mixed `LineBreak` and wrapped runs.
- Empty paragraphs and empty table cells.
- Very long documents with viewport scrolling.

### Tests required
- New file: `InkkSlinger.Tests/RichTextLayoutTests.cs`
- Cases:
  - style run segmentation correct
  - list marker positions stable
  - table cell width/span calculation deterministic
  - caret geometry maps to expected run offsets
  - selection rectangles match multi-line/multi-run ranges

### Acceptance gate
- `RichTextBox` no longer uses flattened plain-text draw loop.
- Layout cache hit/miss behavior measurable and stable.
- No regressions in existing dirty-region tests.

---

## M4 (Remaining): RichTextBox Interaction Parity Depth

### Goal
Complete keyboard/pointer editing semantics to parity depth beyond the current basic behavior.

### Required implementation

1. Selection granularity:
- Double-click selects word.
- Triple-click selects paragraph.
- Shift+click extends selection from anchor.
- Ctrl+Shift+Arrow selects by word.

2. Navigation model:
- Implement logical caret movement over:
  - runs
  - line breaks
  - paragraph boundaries
  - list marker boundaries
  - table cell transitions
- Home/End behavior:
  - line home/end
  - Ctrl+Home/Ctrl+End document edges

3. Scrolling behavior:
- Vertical scroll must track rich layout extent.
- Ensure caret visible on all moves and edits.
- Preserve anchor when auto-scrolling selection drag.

4. Pointer behavior:
- Drag selection across lines/runs.
- Selection in tables should stay cell-scoped unless crossing boundaries intentionally.

5. Read-only semantics:
- Navigation + selection + copy allowed.
- All content mutations blocked.

### Tests required
- New file: `InkkSlinger.Tests/RichTextInputParityTests.cs`
- Cases:
  - word/paragraph selection gestures
  - shift/ctrl navigation semantics
  - ensure-caret-visible after edits
  - read-only mutation blocked while selection works

### Acceptance gate
- Interaction behavior is stable for typical editor flows.
- No regressions in `MenuParityInputTests` and other input tests.

---

## M5 (Not started): Editing Commands Parity

### Goal
Add routed command surface for rich text editing and formatting operations.

### Required implementation

1. Add command definitions:
- New file: `UI/Commanding/EditingCommands.cs`
- Required commands:
  - text: `Backspace`, `Delete`, `EnterParagraphBreak`, `EnterLineBreak`, `TabForward`, `TabBackward`
  - clipboard: `Copy`, `Cut`, `Paste`
  - selection: `SelectAll`, `MoveLeftByCharacter`, `MoveRightByCharacter`, `MoveLeftByWord`, `MoveRightByWord`
  - formatting: `ToggleBold`, `ToggleItalic`, `ToggleUnderline`
  - structure: `IncreaseListLevel`, `DecreaseListLevel`, `InsertTable`, `SplitCell`, `MergeCells`

2. Bind commands in `RichTextBox`:
- Register command bindings in constructor or static initializer.
- Implement `CanExecute` based on selection/read-only/structure context.

3. Gesture mapping:
- Integrate with existing key handling and `InputGestureService` without double-executing.
- Prefer command execution path; key handlers should delegate into commands.

### Tests required
- New file: `InkkSlinger.Tests/RichTextCommandingTests.cs`
- Cases:
  - `CanExecute` states
  - command execution mutates expected document parts
  - key gesture invokes command through routed system

### Acceptance gate
- Rich text mutations primarily flow through command infrastructure.
- Existing commanding tests remain green.

---

## M6 (Remaining): XAML Authoring + Strict Validation

### Goal
Make rich-document XAML robust with strict invalid-structure diagnostics.

### Required implementation

1. Extend `XamlLoader` object-children support:
- File: `UI/Xaml/Core/XamlLoader.cs`
- Add explicit container handlers for:
  - `FlowDocument.Blocks`
  - `Paragraph.Inlines`
  - `Span.Inlines`
  - `Section.Blocks`
  - `List.Items`
  - `ListItem.Blocks`
  - `Table.RowGroups`
  - `TableRowGroup.Rows`
  - `TableRow.Cells`
  - `TableCell.Blocks`
  - `RichTextBox.Document`

2. Enforce strict child validity:
- Examples:
  - `Paragraph` cannot contain `Block`.
  - `TableRow` cannot directly contain `Paragraph`.
  - `Run` cannot have children.
  - `LineBreak` cannot have attributes except supported metadata.

3. Attribute validation:
- Validate numeric spans > 0.
- Validate URI format for `Hyperlink.NavigateUri` (at least non-empty string, strict parsing optional).

4. Improve diagnostics:
- Ensure all failures include line/position and offending element/attribute name.

### Tests required
- New file: `InkkSlinger.Tests/RichTextXamlParserTests.cs`
- Positive cases:
  - nested formatting + lists + tables load
- Negative cases:
  - invalid child structure
  - unsupported attributes
  - invalid span values

### Acceptance gate
- Rich XAML authoring works in demo views.
- Invalid documents fail with precise diagnostics.

---

## M7 (Remaining): Clipboard + Range Serialization Fidelity

### Goal
Make rich clipboard behavior selection-aware and deterministic.

### Required implementation

1. Range serialization:
- Add methods:
  - `FlowDocumentSerializer.SerializeRange(FlowDocument doc, DocumentTextSelection selection)`
  - `FlowDocumentSerializer.DeserializeFragment(string xml)`
- Preserve inline formatting and block boundaries within selected range.

2. Clipboard behavior in `RichTextBox`:
- Copy:
  - plain text (selected text)
  - rich fragment payload (selected fragment, not full document)
- Cut:
  - same payload capture + delete selection
- Paste:
  - rich payload preferred when present
  - plain text fallback

3. Interop behavior:
- If rich payload invalid/corrupt, fail gracefully to text paste.

### Tests required
- New file: `InkkSlinger.Tests/RichTextClipboardTests.cs`
- Cases:
  - copy/paste preserves inline formatting in selected fragment
  - cut removes exact selection and stores payloads
  - invalid rich payload falls back to text

### Acceptance gate
- Clipboard operations are range-fidelity correct and robust to bad payloads.

---

## M8 (Not started): Advanced Structure Semantics

### Goal
Finish advanced structure behavior for lists/tables/containers/hyperlinks.

### Required implementation

1. Lists:
- Nest/un-nest behavior on indent/outdent commands.
- Numbering continuity in ordered lists.
- Paragraph-to-list and list-to-paragraph transformations.

2. Tables:
- Cell selection model.
- Split/merge operations with span updates.
- Navigation with Tab/Shift+Tab across cells.
- Backspace/Delete behavior at cell boundaries.

3. UI containers:
- `InlineUIContainer` and `BlockUIContainer` hosting behavior:
  - measure/arrange participation
  - caret positioning around containers
  - selection boundaries around containers

4. Hyperlinks:
- Input behavior:
  - click routing event for activation
  - keyboard activation when caret/selection on hyperlink
- Visual state (at least hover/normal if style hooks exist).

### Tests required
- New file: `InkkSlinger.Tests/RichTextAdvancedStructureTests.cs`
- Cases:
  - nested list transforms
  - table split/merge deterministic structure updates
  - container selection boundaries
  - hyperlink activation routing

### Acceptance gate
- Advanced structures behave predictably under edit, selection, navigation, and commands.

---

## M9 (Not started): Diagnostics + Performance Hardening

### Goal
Add observability and ensure large-document interaction performance.

### Required implementation

1. Diagnostics snapshot type:
- New type: `RichTextBoxPerformanceSnapshot`
- Metrics:
  - layout cache hits/misses
  - layout build ms (avg/p95/p99 if sampled)
  - render ms
  - selection geometry build ms
  - undo stack depth and op counts
  - clipboard serialization/deserialization ms

2. Optional env-var logging toggles:
- Proposed:
  - `INKKSLINGER_RICHTEXT_LAYOUT_LOGS`
  - `INKKSLINGER_RICHTEXT_EDIT_LOGS`
  - `INKKSLINGER_RICHTEXT_CLIPBOARD_LOGS`

3. Large document scenarios:
- Synthetic docs:
  - 10k+ paragraphs
  - long tables
  - nested formatting depth
- Ensure interactive operations remain within acceptable budget.

4. Cache and invalidation tuning:
- Minimize full re-layout on local edits.
- Avoid excessive allocations in selection/caret hit-testing.

### Tests/bench validation required
- New file: `InkkSlinger.Tests/RichTextPerformanceTests.cs`
- Cases:
  - no pathological allocation spikes in repeated edits
  - local edit does not trigger full-doc layout in common scenarios

### Acceptance gate
- Performance snapshots available and stable.
- No severe interaction regressions on large documents.

---

## Cross-Cutting Work Required Across Milestones

1. Source generator/name wiring sanity:
- Ensure new demo view named fields are generated and tested similarly to other demos.

2. Render cache policy evolution:
- Revisit `DefaultRenderCachePolicy` for rich text once layout engine is granular.
- Current `IsRenderCacheStable` heuristic likely needs richer conditions.

3. Maintain backward compatibility:
- Do not regress `TextBox`/`PasswordBox` behavior while expanding generic text-input interfaces.

4. Keep tests first-class:
- For every feature: add positive + negative tests before or alongside implementation.

5. Documentation updates on each milestone:
- `README.md` parity table + run modes.
- `TODO.md` milestone status toggles.
- `UI-FOLDER-MAP.md` regeneration when folder structure changes.

---

## Suggested Implementation Order From Here

1. Finish M2 fully (true transaction/operation undo).
2. Build M3 layout engine skeleton + swap `RichTextBox` render path.
3. Complete M4 interaction parity on top of M3 caret geometry.
4. Implement M5 commands and route key handling through commands.
5. Tighten M6 strict parser behavior + negative tests.
6. Finish M7 range-fidelity clipboard.
7. Complete M8 advanced structures.
8. Do M9 diagnostics/perf hardening last with realistic workloads.

---

## Definition of Done (Global)

The RichText initiative is done only when:
- All milestones M2..M9 acceptance gates are satisfied.
- Existing test suite remains green.
- New rich text tests comprehensively cover model, editing, commands, xaml, clipboard, advanced structure, and perf.
- Demo mode `--richtextbox-demo` is stable for manual validation of core and advanced scenarios.
