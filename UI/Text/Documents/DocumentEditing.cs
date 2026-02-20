using System;
using System.Collections.Generic;

namespace InkkSlinger;

public interface IDocumentUndoUnit
{
    void Undo();

    void Redo();
}

public enum GroupingPolicy
{
    TypingBurst,
    DeletionBurst,
    FormatBurst,
    StructuralAtomic
}

public readonly record struct DocumentEditContext(
    int CaretBefore,
    int CaretAfter,
    int SelectionStartBefore,
    int SelectionLengthBefore,
    int SelectionStartAfter,
    int SelectionLengthAfter,
    string CommandType);

public sealed class DocumentUndoManager
{
    private static readonly TimeSpan TypingCoalesceWindow = TimeSpan.FromMilliseconds(400);
    private readonly Stack<IDocumentUndoUnit> _undo = [];
    private readonly Stack<IDocumentUndoUnit> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoDepth => _undo.Count;

    public int RedoDepth => _redo.Count;

    public int UndoOperationCount => CountOperations(_undo);

    public int RedoOperationCount => CountOperations(_redo);

    public void Push(IDocumentUndoUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit is DocumentOperationUndoUnit incoming &&
            _undo.TryPeek(out var top) &&
            top is DocumentOperationUndoUnit previous &&
            previous.TryCoalesceWith(incoming, TypingCoalesceWindow))
        {
            _redo.Clear();
            return;
        }

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

    private static int CountOperations(IEnumerable<IDocumentUndoUnit> units)
    {
        var total = 0;
        foreach (var unit in units)
        {
            if (unit is DocumentOperationUndoUnit operationUnit)
            {
                total += operationUnit.OperationCount;
            }
            else
            {
                total++;
            }
        }

        return total;
    }
}

public sealed class DocumentEditSession
{
    private readonly FlowDocument _document;
    private readonly DocumentUndoManager _undoManager;
    private readonly List<IDocumentOperation> _operations = [];
    private string? _fallbackBeforeXml;
    private bool _fallbackRequested;
    private bool _isTransactionOpen;
    private string _reason = string.Empty;
    private GroupingPolicy _policy = GroupingPolicy.StructuralAtomic;
    private DocumentEditContext _context;

    public DocumentEditSession(FlowDocument document, DocumentUndoManager undoManager)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
    }

    public bool IsTransactionOpen => _isTransactionOpen;

    public void BeginTransaction(string reason, GroupingPolicy policy, DocumentEditContext context)
    {
        if (_isTransactionOpen)
        {
            throw new InvalidOperationException("A transaction is already open.");
        }

        _operations.Clear();
        _fallbackBeforeXml = null;
        _fallbackRequested = false;
        _reason = reason ?? string.Empty;
        _policy = policy;
        _context = context;
        _isTransactionOpen = true;
    }

    public void ApplyOperation(IDocumentOperation operation)
    {
        EnsureTransactionOpen();
        ArgumentNullException.ThrowIfNull(operation);
        EnsureFallbackCaptureIfNeeded(operation);
        operation.Apply(_document);
        _operations.Add(operation);
    }

    public void AddOperation(IDocumentOperation operation)
    {
        EnsureTransactionOpen();
        ArgumentNullException.ThrowIfNull(operation);
        EnsureFallbackCaptureIfNeeded(operation);
        _operations.Add(operation);
    }

    public void CommitTransaction()
    {
        EnsureTransactionOpen();
        if (_operations.Count == 0)
        {
            Reset();
            return;
        }

        var fallbackAfter = _fallbackRequested ? FlowDocumentSerializer.Serialize(_document) : null;
        _undoManager.Push(
            new DocumentOperationUndoUnit(
                _document,
                _operations.ToArray(),
                _reason,
                _policy,
                _context,
                DateTime.UtcNow,
                _fallbackBeforeXml,
                fallbackAfter));
        Reset();
    }

    public void RollbackTransaction()
    {
        EnsureTransactionOpen();
        for (var index = _operations.Count - 1; index >= 0; index--)
        {
            _operations[index].Revert(_document);
        }

        Reset();
    }

    private void EnsureFallbackCaptureIfNeeded(IDocumentOperation operation)
    {
        if (operation is IDeterministicDocumentOperation)
        {
            return;
        }

        if (!_fallbackRequested)
        {
            _fallbackBeforeXml = FlowDocumentSerializer.Serialize(_document);
            _fallbackRequested = true;
        }
    }

    private void EnsureTransactionOpen()
    {
        if (!_isTransactionOpen)
        {
            throw new InvalidOperationException("No active document transaction.");
        }
    }

    private void Reset()
    {
        _operations.Clear();
        _fallbackBeforeXml = null;
        _fallbackRequested = false;
        _isTransactionOpen = false;
        _reason = string.Empty;
        _policy = GroupingPolicy.StructuralAtomic;
        _context = default;
    }
}

internal sealed class DocumentOperationUndoUnit : IDocumentUndoUnit
{
    private readonly FlowDocument _target;
    private readonly IDocumentOperation[] _operations;
    private readonly string? _fallbackBeforeXml;
    private readonly string? _fallbackAfterXml;

    public DocumentOperationUndoUnit(
        FlowDocument target,
        IDocumentOperation[] operations,
        string reason,
        GroupingPolicy policy,
        DocumentEditContext context,
        DateTime timestampUtc,
        string? fallbackBeforeXml,
        string? fallbackAfterXml)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        Reason = reason ?? string.Empty;
        Policy = policy;
        Context = context;
        TimestampUtc = timestampUtc;
        _fallbackBeforeXml = fallbackBeforeXml;
        _fallbackAfterXml = fallbackAfterXml;
    }

    public string Reason { get; private set; }

    public GroupingPolicy Policy { get; private set; }

    public DocumentEditContext Context { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public int OperationCount => _operations.Length;

    public bool TryCoalesceWith(DocumentOperationUndoUnit incoming, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (Policy != incoming.Policy || Policy == GroupingPolicy.StructuralAtomic)
        {
            return false;
        }

        if (!string.Equals(Context.CommandType, incoming.Context.CommandType, StringComparison.Ordinal))
        {
            return false;
        }

        if (incoming.TimestampUtc - TimestampUtc > window)
        {
            return false;
        }

        if (Context.SelectionLengthAfter != incoming.Context.SelectionLengthBefore ||
            Context.SelectionStartAfter != incoming.Context.SelectionStartBefore ||
            Context.CaretAfter != incoming.Context.CaretBefore)
        {
            return false;
        }

        if (_operations.Length != 1 || incoming._operations.Length != 1 ||
            _operations[0] is not ReplaceTextOperation first ||
            incoming._operations[0] is not ReplaceTextOperation second)
        {
            return false;
        }

        if (!first.TryCoalesce(second, Policy, out var merged))
        {
            return false;
        }

        _operations[0] = merged;
        Context = incoming.Context;
        TimestampUtc = incoming.TimestampUtc;
        Reason = incoming.Reason;
        return true;
    }

    public void Undo()
    {
        try
        {
            for (var index = _operations.Length - 1; index >= 0; index--)
            {
                _operations[index].Revert(_target);
            }
        }
        catch when (_fallbackBeforeXml != null)
        {
            DocumentEditing.RehydrateFromSerializedSnapshot(_target, _fallbackBeforeXml);
        }
    }

    public void Redo()
    {
        try
        {
            for (var index = 0; index < _operations.Length; index++)
            {
                _operations[index].Apply(_target);
            }
        }
        catch when (_fallbackAfterXml != null)
        {
            DocumentEditing.RehydrateFromSerializedSnapshot(_target, _fallbackAfterXml);
        }
    }
}

public static class DocumentEditing
{
    public static string GetText(FlowDocument document)
    {
        return GetLogicalText(document);
    }

    public static void ReplaceAllText(FlowDocument document, string? text, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var replacement = NormalizeLogicalNewlines(text);
        if (undoManager == null)
        {
            FlowDocumentPlainText.SetText(document, replacement);
            return;
        }

        var beforeDocument = CloneDocument(document);
        var beforePayload = GetText(document);
        FlowDocumentPlainText.SetText(document, replacement);
        var afterDocument = CloneDocument(document);
        var session = new DocumentEditSession(document, undoManager);
        session.BeginTransaction(
            "ReplaceAllText",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(0, replacement.Length, 0, beforePayload.Length, 0, 0, "ReplaceAllText"));
        session.AddOperation(new ReplaceDocumentOperation("Document", beforePayload, replacement, beforeDocument, afterDocument));
        session.CommitTransaction();
    }

    public static void ReplaceTextRange(
        FlowDocument document,
        int start,
        int length,
        string? replacement,
        DocumentEditSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var content = GetText(document);
        var clampedStart = Math.Clamp(start, 0, content.Length);
        var clampedLength = Math.Clamp(length, 0, content.Length - clampedStart);
        var removedText = content.Substring(clampedStart, clampedLength);
        var insertText = NormalizeLogicalNewlines(replacement);

        if (removedText.Length == 0 && insertText.Length == 0)
        {
            return;
        }

        var operation = new ReplaceTextOperation(clampedStart, removedText, insertText);
        if (session == null)
        {
            operation.Apply(document);
            return;
        }

        session.ApplyOperation(operation);
    }

    public static void InsertTextAt(FlowDocument document, int offset, string? text, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var insert = text ?? string.Empty;
        if (insert.Length == 0)
        {
            return;
        }

        if (undoManager == null)
        {
            ReplaceTextRange(document, offset, 0, insert);
            return;
        }

        var session = new DocumentEditSession(document, undoManager);
        var clamped = Math.Clamp(offset, 0, GetText(document).Length);
        session.BeginTransaction(
            "InsertText",
            GroupingPolicy.TypingBurst,
            new DocumentEditContext(clamped, clamped + insert.Length, clamped, 0, clamped + insert.Length, 0, "InsertText"));
        ReplaceTextRange(document, clamped, 0, insert, session);
        session.CommitTransaction();
    }

    public static void DeleteRange(FlowDocument document, int start, int length, DocumentUndoManager? undoManager = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (length <= 0)
        {
            return;
        }

        if (undoManager == null)
        {
            ReplaceTextRange(document, start, length, string.Empty);
            return;
        }

        var current = GetText(document);
        var clampedStart = Math.Clamp(start, 0, current.Length);
        var clampedLength = Math.Clamp(length, 0, current.Length - clampedStart);
        if (clampedLength == 0)
        {
            return;
        }

        var session = new DocumentEditSession(document, undoManager);
        session.BeginTransaction(
            "DeleteRange",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(clampedStart + clampedLength, clampedStart, clampedStart, clampedLength, clampedStart, 0, "DeleteRange"));
        ReplaceTextRange(document, clampedStart, clampedLength, string.Empty, session);
        session.CommitTransaction();
    }

    internal static string ReadTextRange(FlowDocument document, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(document);
        var content = GetText(document);
        var clampedStart = Math.Clamp(start, 0, content.Length);
        var clampedLength = Math.Clamp(length, 0, content.Length - clampedStart);
        return content.Substring(clampedStart, clampedLength);
    }

    internal static void ApplyTextReplacement(FlowDocument document, int start, int length, string? replacement)
    {
        ArgumentNullException.ThrowIfNull(document);
        var content = GetText(document);
        var clampedStart = Math.Clamp(start, 0, content.Length);
        var clampedLength = Math.Clamp(length, 0, content.Length - clampedStart);
        var next = content.Remove(clampedStart, clampedLength).Insert(clampedStart, NormalizeLogicalNewlines(replacement));
        FlowDocumentPlainText.SetText(document, next);
    }

    internal static void RehydrateFromSerializedSnapshot(FlowDocument target, string xml)
    {
        var snapshot = FlowDocumentSerializer.Deserialize(xml);
        ReplaceDocumentContent(target, snapshot);
    }

    internal static void ReplaceDocumentContent(FlowDocument target, FlowDocument source)
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

    internal static FlowDocument CloneDocument(FlowDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = new FlowDocument();
        foreach (var block in source.Blocks)
        {
            clone.Blocks.Add(CloneBlock(block));
        }

        if (clone.Blocks.Count == 0)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(string.Empty));
            clone.Blocks.Add(paragraph);
        }

        return clone;
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
                var clonedList = new List
                {
                    IsOrdered = list.IsOrdered
                };
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
                return new Run(run.Text)
                {
                    Foreground = run.Foreground
                };
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

    private static string GetLogicalText(FlowDocument document)
    {
        return NormalizeLogicalNewlines(FlowDocumentPlainText.GetText(document));
    }

    private static string NormalizeLogicalNewlines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
