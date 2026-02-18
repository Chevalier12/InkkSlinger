using System;
using System.Collections.Generic;

namespace InkkSlinger;

public readonly record struct TextPointer(Run Run, int Offset)
{
    public TextPointer Normalize()
    {
        var clamped = Math.Clamp(Offset, 0, Run.Text.Length);
        return clamped == Offset ? this : new TextPointer(Run, clamped);
    }
}

public readonly record struct TextRange(TextPointer Start, TextPointer End)
{
    public bool IsEmpty => Start.Run == End.Run && Start.Offset == End.Offset;

    public TextRange Normalize()
    {
        var left = Start.Normalize();
        var right = End.Normalize();
        if (DocumentPointers.Compare(left, right) <= 0)
        {
            return new TextRange(left, right);
        }

        return new TextRange(right, left);
    }
}

public readonly record struct DocumentTextSelection(TextPointer Anchor, TextPointer Active)
{
    public TextRange ToRange()
    {
        return new TextRange(Anchor, Active).Normalize();
    }
}

public static class DocumentPointers
{
    public static int Compare(TextPointer left, TextPointer right)
    {
        if (ReferenceEquals(left.Run, right.Run))
        {
            return left.Offset.CompareTo(right.Offset);
        }

        var leftIndex = GetRunDocumentOrderIndex(left.Run);
        var rightIndex = GetRunDocumentOrderIndex(right.Run);
        return leftIndex.CompareTo(rightIndex);
    }

    public static TextPointer CreateAtDocumentOffset(FlowDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        var clamped = Math.Max(0, offset);
        var traversed = 0;
        foreach (var run in EnumerateRuns(document))
        {
            var runLength = run.Text.Length;
            if (clamped <= traversed + runLength)
            {
                return new TextPointer(run, clamped - traversed);
            }

            traversed += runLength;
        }

        var fallback = EnsureTrailingRun(document);
        return new TextPointer(fallback, fallback.Text.Length);
    }

    public static int GetDocumentOffset(TextPointer pointer)
    {
        var root = FindOwningDocument(pointer.Run);
        var traversed = 0;
        foreach (var run in EnumerateRuns(root))
        {
            if (ReferenceEquals(run, pointer.Run))
            {
                return traversed + Math.Clamp(pointer.Offset, 0, run.Text.Length);
            }

            traversed += run.Text.Length;
        }

        return traversed;
    }

    private static int GetRunDocumentOrderIndex(Run run)
    {
        var root = FindOwningDocument(run);
        var index = 0;
        foreach (var candidate in EnumerateRuns(root))
        {
            if (ReferenceEquals(candidate, run))
            {
                return index;
            }

            index++;
        }

        return int.MaxValue;
    }

    private static FlowDocument FindOwningDocument(TextElement element)
    {
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (current is FlowDocument document)
            {
                return document;
            }
        }

        throw new InvalidOperationException("TextPointer target is not attached to a FlowDocument.");
    }

    private static Run EnsureTrailingRun(FlowDocument document)
    {
        foreach (var run in EnumerateRuns(document))
        {
            return run;
        }

        var paragraph = new Paragraph();
        var seed = new Run(string.Empty);
        paragraph.Inlines.Add(seed);
        document.Blocks.Add(paragraph);
        return seed;
    }

    private static IEnumerable<Run> EnumerateRuns(FlowDocument document)
    {
        foreach (var paragraph in FlowDocumentPlainText.EnumerateParagraphs(document))
        {
            foreach (var run in EnumerateRuns(paragraph.Inlines))
            {
                yield return run;
            }
        }
    }

    private static IEnumerable<Run> EnumerateRuns(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    yield return run;
                    break;
                case Span span:
                    foreach (var nested in EnumerateRuns(span.Inlines))
                    {
                        yield return nested;
                    }
                    break;
            }
        }
    }
}
