# Designer IDE Editor QoL TODO

## Implemented

- [x] Split generic text editing commands into `IDEEditorTextCommandService`.
- [x] Split XML-specific editor behavior into `DesignerXmlEditorLanguageService`.
- [x] Preserve XML syntax highlighting after automatic indentation/document edits.
- [x] Infer and indent closing tags when typing `</` after converting a self-closing tag.
- [x] Smart `Enter` indentation for XML tags and immediate close-tag splits.
- [x] `Tab` and `Shift+Tab` line/block indent and outdent.
- [x] `Ctrl+/` XML line/block comment toggle.
- [x] `Ctrl+Shift+F` lightweight XML document formatting.
- [x] `Alt+Up` and `Alt+Down` move selected/current lines.
- [x] `Alt+Shift+Up` and `Alt+Shift+Down` duplicate selected/current lines.
- [x] `Ctrl+Shift+K` delete selected/current lines.
- [x] Quote/bracket pair insertion and skip-over for `"`, `'`, `(`, `[`, and `{`.
- [x] `Ctrl+M` select matching XML open/close tag.
- [x] Rename paired XML tag when editing either tag name.
- [x] Folding for XML elements and region-like comments.
- [x] Interactive rendered minimap/document overview for XML source navigation.
- [x] Configurable indentation settings for smart enter, tab indent, outdent, and format.
- [x] xUnit coverage for the new source-editor keyboard behaviors.

## Still Missing

- [ ] In-editor find/replace UI with match navigation and selection-safe replace.
- [ ] Diagnostics gutter, squiggles, and parse error navigation.
- [ ] Hover tooltips for diagnostics, control docs, bindings, and resources.
- [ ] Symbol-aware XML completion for attributes, enum values, bindings, resources, events, and attached properties.
- [ ] Snippets for common XML structures.
- [ ] Wrap selected text with typed quote/bracket pairs through a pre-text-input hook.
- [ ] Multi-cursor or multi-selection editing.
- [ ] Expand/shrink semantic selection.
- [ ] Command palette entries for editor actions.
- [ ] Go-to-definition for controls, resources, bindings, and handlers.
- [ ] Breadcrumb or outline navigation.
- [ ] Undo grouping for multi-step editor commands.
- [ ] Automatic trailing whitespace cleanup and final-newline normalization.
- [ ] Format selection instead of only format document.
- [ ] Paste-time indentation normalization.
- [ ] Auto-import/resource insertion assistance where the XML compiler can resolve candidates.
