using System;
using System.Collections.Generic;

namespace InkkSlinger;

public interface IDocumentUndoUnit
{
    void Undo();

    void Redo();
}

public sealed class DocumentUndoManager
{
    private readonly Stack<IDocumentUndoUnit> _undo = [];
    private readonly Stack<IDocumentUndoUnit> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Push(IDocumentUndoUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        _undo.Push(unit);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var unit = _undo.Pop();
        unit.Undo();
        _redo.Push(unit);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var unit = _redo.Pop();
        unit.Redo();
        _undo.Push(unit);
        return true;
    }
}

public sealed class DocumentEditTransaction
{
    private readonly DocumentUndoManager _manager;
    private readonly List<IDocumentUndoUnit> _units = [];

    public DocumentEditTransaction(DocumentUndoManager manager)
    {
        _manager = manager;
    }

    public void Add(IDocumentUndoUnit unit)
    {
        _units.Add(unit);
    }

    public void Commit()
    {
        if (_units.Count == 0)
        {
            return;
        }

        _manager.Push(new CompositeUndoUnit(_units.ToArray()));
        _units.Clear();
    }

    private sealed class CompositeUndoUnit : IDocumentUndoUnit
    {
        private readonly IDocumentUndoUnit[] _units;

        public CompositeUndoUnit(IDocumentUndoUnit[] units)
        {
            _units = units;
        }

        public void Undo()
        {
            for (var i = _units.Length - 1; i >= 0; i--)
            {
                _units[i].Undo();
            }
        }

        public void Redo()
        {
            for (var i = 0; i < _units.Length; i++)
            {
                _units[i].Redo();
            }
        }
    }
}

public static class DocumentEditing
{
    public static string GetText(FlowDocument document)
    {
        return FlowDocumentPlainText.GetText(document);
    }

    public static void ReplaceAllText(FlowDocument document, string? text, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var before = FlowDocumentSerializer.Serialize(document);
        FlowDocumentPlainText.SetText(document, text ?? string.Empty);
        var after = FlowDocumentSerializer.Serialize(document);
        if (undoManager != null)
        {
            undoManager.Push(new SerializedDocumentUndoUnit(document, before, after));
        }
    }

    public static void InsertTextAt(FlowDocument document, int offset, string? text, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var insert = text ?? string.Empty;
        if (insert.Length == 0)
        {
            return;
        }

        var content = FlowDocumentPlainText.GetText(document);
        var clamped = Math.Clamp(offset, 0, content.Length);
        var merged = content.Insert(clamped, insert);
        ReplaceAllText(document, merged, undoManager);
    }

    public static void DeleteRange(FlowDocument document, int start, int length, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (length <= 0)
        {
            return;
        }

        var content = FlowDocumentPlainText.GetText(document);
        var clampedStart = Math.Clamp(start, 0, content.Length);
        var clampedLength = Math.Clamp(length, 0, content.Length - clampedStart);
        var merged = content.Remove(clampedStart, clampedLength);
        ReplaceAllText(document, merged, undoManager);
    }

    private sealed class SerializedDocumentUndoUnit : IDocumentUndoUnit
    {
        private readonly FlowDocument _target;
        private readonly string _before;
        private readonly string _after;

        public SerializedDocumentUndoUnit(FlowDocument target, string before, string after)
        {
            _target = target;
            _before = before;
            _after = after;
        }

        public void Undo()
        {
            Rehydrate(_before);
        }

        public void Redo()
        {
            Rehydrate(_after);
        }

        private void Rehydrate(string xml)
        {
            var snapshot = FlowDocumentSerializer.Deserialize(xml);
            ReplaceDocumentContent(_target, snapshot);
        }
    }

    private static void ReplaceDocumentContent(FlowDocument target, FlowDocument source)
    {
        target.Blocks.Clear();
        foreach (var block in source.Blocks)
        {
            target.Blocks.Add(CloneBlock(block));
        }

        if (target.Blocks.Count == 0)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(string.Empty));
            target.Blocks.Add(paragraph);
        }
    }

    private static Block CloneBlock(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                var clonedParagraph = new Paragraph();
                foreach (var inline in paragraph.Inlines)
                {
                    clonedParagraph.Inlines.Add(CloneInline(inline));
                }

                if (clonedParagraph.Inlines.Count == 0)
                {
                    clonedParagraph.Inlines.Add(new Run(string.Empty));
                }

                return clonedParagraph;
            case Section section:
                var clonedSection = new Section();
                foreach (var nested in section.Blocks)
                {
                    clonedSection.Blocks.Add(CloneBlock(nested));
                }

                return clonedSection;
            case BlockUIContainer blockUiContainer:
                return new BlockUIContainer
                {
                    Child = blockUiContainer.Child
                };
            case List list:
                var clonedList = new List();
                foreach (var item in list.Items)
                {
                    var clonedItem = new ListItem();
                    foreach (var nested in item.Blocks)
                    {
                        clonedItem.Blocks.Add(CloneBlock(nested));
                    }

                    clonedList.Items.Add(clonedItem);
                }

                return clonedList;
            case Table table:
                var clonedTable = new Table();
                foreach (var rowGroup in table.RowGroups)
                {
                    var clonedRowGroup = new TableRowGroup();
                    foreach (var row in rowGroup.Rows)
                    {
                        var clonedRow = new TableRow();
                        foreach (var cell in row.Cells)
                        {
                            var clonedCell = new TableCell
                            {
                                RowSpan = cell.RowSpan,
                                ColumnSpan = cell.ColumnSpan
                            };
                            foreach (var nested in cell.Blocks)
                            {
                                clonedCell.Blocks.Add(CloneBlock(nested));
                            }

                            clonedRow.Cells.Add(clonedCell);
                        }

                        clonedRowGroup.Rows.Add(clonedRow);
                    }

                    clonedTable.RowGroups.Add(clonedRowGroup);
                }

                return clonedTable;
            default:
                throw new InvalidOperationException($"Unsupported block type '{block.GetType().Name}'.");
        }
    }

    private static Inline CloneInline(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                return new Run(run.Text);
            case LineBreak:
                return new LineBreak();
            case Bold bold:
                return CloneSpan(bold, new Bold());
            case Italic italic:
                return CloneSpan(italic, new Italic());
            case Underline underline:
                return CloneSpan(underline, new Underline());
            case Hyperlink hyperlink:
                return CloneSpan(
                    hyperlink,
                    new Hyperlink
                    {
                        NavigateUri = hyperlink.NavigateUri
                    });
            case Span span:
                return CloneSpan(span, new Span());
            case InlineUIContainer inlineUiContainer:
                return new InlineUIContainer
                {
                    Child = inlineUiContainer.Child
                };
            default:
                throw new InvalidOperationException($"Unsupported inline type '{inline.GetType().Name}'.");
        }
    }

    private static TSpan CloneSpan<TSpan>(Span source, TSpan destination)
        where TSpan : Span
    {
        foreach (var nested in source.Inlines)
        {
            destination.Inlines.Add(CloneInline(nested));
        }

        return destination;
    }
}
