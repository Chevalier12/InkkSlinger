using System;

namespace InkkSlinger;

public interface IDocumentOperation
{
    string TargetPath { get; }

    string BeforePayload { get; }

    string AfterPayload { get; }

    void Apply(FlowDocument document);

    void Revert(FlowDocument document);
}

public interface IDeterministicDocumentOperation : IDocumentOperation;

public sealed class ReplaceTextOperation : IDeterministicDocumentOperation
{
    public ReplaceTextOperation(int offset, string beforeText, string afterText)
    {
        Offset = Math.Max(0, offset);
        BeforeText = beforeText ?? string.Empty;
        AfterText = afterText ?? string.Empty;
    }

    public int Offset { get; }

    public string BeforeText { get; }

    public string AfterText { get; }

    public string TargetPath => $"Document:PlainText@{Offset}";

    public string BeforePayload => BeforeText;

    public string AfterPayload => AfterText;

    public void Apply(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var existing = DocumentEditing.ReadTextRange(document, Offset, BeforeText.Length);
        if (!string.Equals(existing, BeforeText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ReplaceTextOperation apply mismatch.");
        }

        DocumentEditing.ApplyTextReplacement(document, Offset, BeforeText.Length, AfterText);
    }

    public void Revert(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var existing = DocumentEditing.ReadTextRange(document, Offset, AfterText.Length);
        if (!string.Equals(existing, AfterText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ReplaceTextOperation revert mismatch.");
        }

        DocumentEditing.ApplyTextReplacement(document, Offset, AfterText.Length, BeforeText);
    }

    public bool TryCoalesce(ReplaceTextOperation next, GroupingPolicy policy, out ReplaceTextOperation merged)
    {
        ArgumentNullException.ThrowIfNull(next);
        merged = this;

        if (policy == GroupingPolicy.TypingBurst &&
            BeforeText.Length == 0 &&
            next.BeforeText.Length == 0 &&
            next.Offset == Offset + AfterText.Length)
        {
            merged = new ReplaceTextOperation(Offset, string.Empty, AfterText + next.AfterText);
            return true;
        }

        if (policy == GroupingPolicy.DeletionBurst &&
            AfterText.Length == 0 &&
            next.AfterText.Length == 0)
        {
            var thisEnd = Offset + BeforeText.Length;
            var nextEnd = next.Offset + next.BeforeText.Length;
            if (nextEnd == Offset)
            {
                merged = new ReplaceTextOperation(next.Offset, next.BeforeText + BeforeText, string.Empty);
                return true;
            }

            if (next.Offset == thisEnd)
            {
                merged = new ReplaceTextOperation(Offset, BeforeText + next.BeforeText, string.Empty);
                return true;
            }
        }

        return false;
    }
}

public sealed class InsertTextOperation : IDeterministicDocumentOperation
{
    private readonly ReplaceTextOperation _inner;

    public InsertTextOperation(int offset, string text)
    {
        _inner = new ReplaceTextOperation(offset, string.Empty, text ?? string.Empty);
    }

    public string TargetPath => _inner.TargetPath;

    public string BeforePayload => _inner.BeforePayload;

    public string AfterPayload => _inner.AfterPayload;

    public void Apply(FlowDocument document)
    {
        _inner.Apply(document);
    }

    public void Revert(FlowDocument document)
    {
        _inner.Revert(document);
    }
}

public sealed class DeleteTextOperation : IDeterministicDocumentOperation
{
    private readonly ReplaceTextOperation _inner;

    public DeleteTextOperation(int offset, string deletedText)
    {
        _inner = new ReplaceTextOperation(offset, deletedText ?? string.Empty, string.Empty);
    }

    public string TargetPath => _inner.TargetPath;

    public string BeforePayload => _inner.BeforePayload;

    public string AfterPayload => _inner.AfterPayload;

    public void Apply(FlowDocument document)
    {
        _inner.Apply(document);
    }

    public void Revert(FlowDocument document)
    {
        _inner.Revert(document);
    }
}

public sealed class ReplaceDocumentOperation : DocumentCloneOperation
{
    public ReplaceDocumentOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class ApplyInlineFormatOperation : DocumentCloneOperation
{
    public ApplyInlineFormatOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class SplitParagraphOperation : IDeterministicDocumentOperation
{
    private readonly ReplaceTextOperation _inner;

    public SplitParagraphOperation(int offset)
    {
        _inner = new ReplaceTextOperation(offset, string.Empty, "\n");
    }

    public string TargetPath => _inner.TargetPath;

    public string BeforePayload => _inner.BeforePayload;

    public string AfterPayload => _inner.AfterPayload;

    public void Apply(FlowDocument document)
    {
        _inner.Apply(document);
    }

    public void Revert(FlowDocument document)
    {
        _inner.Revert(document);
    }
}

public sealed class MergeParagraphOperation : IDeterministicDocumentOperation
{
    private readonly ReplaceTextOperation _inner;

    public MergeParagraphOperation(int offset)
    {
        _inner = new ReplaceTextOperation(offset, "\n", string.Empty);
    }

    public string TargetPath => _inner.TargetPath;

    public string BeforePayload => _inner.BeforePayload;

    public string AfterPayload => _inner.AfterPayload;

    public void Apply(FlowDocument document)
    {
        _inner.Apply(document);
    }

    public void Revert(FlowDocument document)
    {
        _inner.Revert(document);
    }
}

public sealed class InsertBlockOperation : DocumentCloneOperation
{
    public InsertBlockOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class RemoveBlockOperation : DocumentCloneOperation
{
    public RemoveBlockOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class ListIndentOperation : DocumentCloneOperation
{
    public ListIndentOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class ListOutdentOperation : DocumentCloneOperation
{
    public ListOutdentOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class TableCellSplitOperation : DocumentCloneOperation
{
    public TableCellSplitOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public sealed class TableCellMergeOperation : DocumentCloneOperation
{
    public TableCellMergeOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
        : base(targetPath, beforePayload, afterPayload, beforeDocument, afterDocument)
    {
    }
}

public abstract class DocumentCloneOperation : IDeterministicDocumentOperation
{
    private readonly FlowDocument _beforeDocument;
    private readonly FlowDocument _afterDocument;

    protected DocumentCloneOperation(
        string targetPath,
        string beforePayload,
        string afterPayload,
        FlowDocument beforeDocument,
        FlowDocument afterDocument)
    {
        TargetPath = targetPath ?? string.Empty;
        BeforePayload = beforePayload ?? string.Empty;
        AfterPayload = afterPayload ?? string.Empty;
        _beforeDocument = DocumentEditing.CloneDocument(beforeDocument ?? throw new ArgumentNullException(nameof(beforeDocument)));
        _afterDocument = DocumentEditing.CloneDocument(afterDocument ?? throw new ArgumentNullException(nameof(afterDocument)));
    }

    public string TargetPath { get; }

    public string BeforePayload { get; }

    public string AfterPayload { get; }

    public virtual void Apply(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DocumentEditing.ReplaceDocumentContent(document, _afterDocument);
    }

    public virtual void Revert(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DocumentEditing.ReplaceDocumentContent(document, _beforeDocument);
    }
}
