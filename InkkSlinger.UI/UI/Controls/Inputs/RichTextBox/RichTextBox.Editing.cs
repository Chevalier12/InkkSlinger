using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private bool TryReplaceSelectionWithinPlainParagraphPreservingStructure(string replacement, string commandType, GroupingPolicy policy)
    {
        if (replacement.IndexOf('\n') >= 0)
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var localStart = start - entries[paragraphIndex].StartOffset;
        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var nextParagraphText = paragraphText.Remove(localStart, localLength).Insert(localStart, replacement);

        var editStart = Stopwatch.GetTimestamp();
        var beforeParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        var afterParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        ReplaceParagraphTextPreservingSimpleWrappers(afterParagraph, nextParagraphText);
        var caretAfter = start + replacement.Length;
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            afterParagraph,
            editStart);
    }

    private bool TryReplaceSelectionWithinParagraphPreservingInlineStyles(string replacement, string commandType, GroupingPolicy policy)
    {
        if (replacement.IndexOf('\n') >= 0 || !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        var paragraphLength = Math.Max(0, entries[paragraphIndex].EndOffset - entries[paragraphIndex].StartOffset);
        var localStart = Math.Clamp(start - entries[paragraphIndex].StartOffset, 0, paragraphLength);
        var localLength = Math.Clamp(length, 0, paragraphLength - localStart);
        var localEnd = localStart + localLength;

        var editStart = Stopwatch.GetTimestamp();
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var beforeParagraph = DocumentEditing.CloneParagraph(sourceParagraph);
        var destination = new Paragraph();
        CopyParagraphSettings(sourceParagraph, destination);
        destination.Inlines.Clear();
        AppendStyledInlineRangeFromParagraph(sourceParagraph, 0, localStart, destination);
        if (!string.IsNullOrEmpty(replacement))
        {
            destination.Inlines.Add(CreateReplacementInlinePreservingContext(sourceParagraph, localStart, replacement));
        }

        AppendStyledInlineRangeFromParagraph(sourceParagraph, localEnd, paragraphLength, destination);
        NormalizeAdjacentInlines(destination);
        if (destination.Inlines.Count == 0)
        {
            destination.Inlines.Add(new Run(string.Empty));
        }

        var caretAfter = start + replacement.Length;
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            destination,
            editStart);
    }

    private static void ReplaceParagraphTextPreservingSimpleWrappers(Paragraph paragraph, string text)
    {
        if (TryCloneSimpleSpanWrapperChain(paragraph, out var wrapper))
        {
            SetInnermostSpanText(wrapper!, text);
            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(wrapper!);
            return;
        }

        paragraph.Inlines.Clear();
        paragraph.Inlines.Add(new Run(text));
    }

    private static bool TryCloneSimpleSpanWrapperChain(Paragraph paragraph, out Span? wrapper)
    {
        wrapper = null;
        if (paragraph.Inlines.Count != 1 || paragraph.Inlines[0] is not Span root)
        {
            return false;
        }

        if (!TryCloneSpanChain(root, out var cloned))
        {
            return false;
        }

        wrapper = cloned;
        return true;
    }

    private static bool TryCloneSpanChain(Span source, out Span? cloned)
    {
        cloned = source switch
        {
            Bold => new Bold(),
            Italic => new Italic(),
            Underline => new Underline(),
            Hyperlink hyperlink => new Hyperlink { NavigateUri = hyperlink.NavigateUri },
            Span => new Span(),
            _ => null
        };

        if (cloned is null)
        {
            return false;
        }

        if (source.Inlines.Count == 0)
        {
            return true;
        }

        if (source.Inlines.Count == 1 && source.Inlines[0] is Span nested)
        {
            if (!TryCloneSpanChain(nested, out var nestedClone))
            {
                return false;
            }

            cloned.Inlines.Add(nestedClone!);
            return true;
        }

        if (source.Inlines.Count == 1 && source.Inlines[0] is Run)
        {
            return true;
        }

        return false;
    }

    private static void SetInnermostSpanText(Span root, string text)
    {
        var current = root;
        while (current.Inlines.Count == 1 && current.Inlines[0] is Span nested)
        {
            current = nested;
        }

        current.Inlines.Clear();
        current.Inlines.Add(new Run(text));
    }

    private bool TryInsertParagraphBreakWithinStructuredParagraph(string commandType, GroupingPolicy policy)
    {
        RecordOperation("StructuredEnter", "Begin");
        var structuredEnterStart = Stopwatch.GetTimestamp();
        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var collectEntriesStart = Stopwatch.GetTimestamp();
        var entries = CollectParagraphEntries(Document);
        var collectEntriesMs = Stopwatch.GetElapsedTime(collectEntriesStart).TotalMilliseconds;
        if (entries.Count == 0)
        {
            RecordOperation("StructuredEnter", "NoParagraphEntries");
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            RecordOperation("StructuredEnter", "SelectionNotInSingleParagraph");
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        var localStart = start - entries[paragraphIndex].StartOffset;
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            RecordOperation("StructuredEnter", "LocalStartOutOfRange");
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var postSelectionText = paragraphText.Remove(localStart, localLength);
        var leftText = postSelectionText[..localStart];
        var rightText = postSelectionText[localStart..];

        var editStart = Stopwatch.GetTimestamp();
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var beforeParagraph = DocumentEditing.CloneParagraph(sourceParagraph);
        var paragraph = sourceParagraph;
        if (paragraph.Parent is ListItem && paragraph.Parent.Parent is InkkSlinger.List)
        {
            var undoDepthBefore = _undoManager.UndoDepth;
            var redoDepthBefore = _undoManager.RedoDepth;
            var textLengthBefore = GetText().Length;
            var beforeText = GetText();
            var cloneDocumentStart = Stopwatch.GetTimestamp();
            var afterDocument = DocumentEditing.CloneDocument(Document);
            var cloneDocumentMs = Stopwatch.GetElapsedTime(cloneDocumentStart).TotalMilliseconds;
            var enumerateParagraphsStart = Stopwatch.GetTimestamp();
            var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
            var enumerateParagraphsMs = Stopwatch.GetElapsedTime(enumerateParagraphsStart).TotalMilliseconds;
            if (paragraphIndex >= paragraphs.Count)
            {
                RecordOperation("StructuredEnter", "ParagraphIndexOutOfRangeAfterClone");
                return false;
            }

            paragraph = paragraphs[paragraphIndex];
            if (paragraph.Parent is not ListItem clonedListItem || clonedListItem.Parent is not InkkSlinger.List clonedList)
            {
                RecordOperation("StructuredEnter", "ListCloneParentResolutionFailed");
                return false;
            }

            var listBehaviorStart = Stopwatch.GetTimestamp();
            if (TryApplyListEnterBehavior(paragraph, clonedListItem, clonedList, leftText, rightText, out afterDocument, out var caretAfter))
            {
                RecordOperation("StructuredEnter", "ListBehaviorApplied");
                var prepareParagraphsMs = Stopwatch.GetElapsedTime(listBehaviorStart).TotalMilliseconds;
                var commitStart = Stopwatch.GetTimestamp();
                var committed = CommitStructuredDocumentReplacement(
                    commandType,
                    policy,
                    start,
                    length,
                    caretAfter,
                    Document,
                    beforeText,
                    afterDocument,
                    textLengthBefore,
                    undoDepthBefore,
                    redoDepthBefore,
                    editStart);
                _perfTracker.ClearStructuredEnterCommitBreakdown();
                _perfTracker.RecordStructuredEnter(
                    collectEntriesMs,
                    cloneDocumentMs,
                    enumerateParagraphsMs,
                    prepareParagraphsMs,
                    Stopwatch.GetElapsedTime(commitStart).TotalMilliseconds,
                    Stopwatch.GetElapsedTime(structuredEnterStart).TotalMilliseconds,
                    usedDocumentReplacement: true);
                return committed;
            }

            RecordOperation("StructuredEnter", "ListBehaviorRejected");
            return false;
        }

        var prepareParagraphsStart = Stopwatch.GetTimestamp();
        var leftParagraph = CreateParagraphFromStyledRange(paragraph, 0, localStart);
        var rightParagraph = CreateParagraphFromStyledRange(paragraph, localStart + localLength, paragraphText.Length);
        RecordOperation("StructuredEnter", "ParagraphSplitApplied");
        var prepareParagraphsMsFinal = Stopwatch.GetElapsedTime(prepareParagraphsStart).TotalMilliseconds;
        var splitCommitStart = Stopwatch.GetTimestamp();
        var splitCommitted = CommitParagraphSplit(
            commandType,
            policy,
            start,
            length,
            start + 1,
            paragraphIndex,
            beforeParagraph,
            leftParagraph,
            rightParagraph,
            editStart);
        _perfTracker.RecordStructuredEnter(
            collectEntriesMs,
            0d,
            0d,
            prepareParagraphsMsFinal,
            Stopwatch.GetElapsedTime(splitCommitStart).TotalMilliseconds,
            Stopwatch.GetElapsedTime(structuredEnterStart).TotalMilliseconds,
            usedDocumentReplacement: false);
        return splitCommitted;
    }

    private bool CommitStructuredDocumentReplacement(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        FlowDocument beforeDocument,
        string beforeText,
        FlowDocument afterDocument,
        int textLengthBefore,
        int undoDepthBefore,
        int redoDepthBefore,
        long editStart)
    {
        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, Document, afterDocument));
            UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
            session.CommitTransaction();
        });
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private bool CommitParagraphReplacement(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int paragraphIndex,
        Paragraph beforeParagraph,
        Paragraph afterParagraph,
        long editStart)
    {
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(
                new ReplaceParagraphOperation(
                    paragraphIndex,
                    DocumentEditing.GetParagraphText(beforeParagraph),
                    DocumentEditing.GetParagraphText(afterParagraph),
                    beforeParagraph,
                    afterParagraph));
            UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
            session.CommitTransaction();
        });
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private bool CommitParagraphSplit(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int paragraphIndex,
        Paragraph beforeParagraph,
        Paragraph currentParagraphAfterSplit,
        Paragraph insertedParagraph,
        long editStart)
    {
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        var applyOperationMs = 0d;
        var commitTransactionMs = 0d;
        var mutationBatchStart = Stopwatch.GetTimestamp();
        ExecuteTextMutationBatch(() =>
        {
            var applyOperationStart = Stopwatch.GetTimestamp();
            session.ApplyOperation(
                new SplitStructuredParagraphOperation(
                    paragraphIndex,
                    DocumentEditing.GetParagraphText(beforeParagraph),
                    $"{DocumentEditing.GetParagraphText(currentParagraphAfterSplit)}\n{DocumentEditing.GetParagraphText(insertedParagraph)}",
                    beforeParagraph,
                    currentParagraphAfterSplit,
                    insertedParagraph));
            applyOperationMs = Stopwatch.GetElapsedTime(applyOperationStart).TotalMilliseconds;
            var commitTransactionStart = Stopwatch.GetTimestamp();
            session.CommitTransaction();
            commitTransactionMs = Stopwatch.GetElapsedTime(commitTransactionStart).TotalMilliseconds;
        });
        var mutationBatchMs = Stopwatch.GetElapsedTime(mutationBatchStart).TotalMilliseconds;
        var selectionStart = Stopwatch.GetTimestamp();
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var selectionMs = Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        var traceInvariantsStart = Stopwatch.GetTimestamp();
        TraceInvariants(commandType);
        var traceInvariantsMs = Stopwatch.GetElapsedTime(traceInvariantsStart).TotalMilliseconds;
        var ensureCaretVisibleStart = Stopwatch.GetTimestamp();
        EnsureCaretVisible();
        var ensureCaretVisibleMs = Stopwatch.GetElapsedTime(ensureCaretVisibleStart).TotalMilliseconds;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        var invalidateAfterMutationStart = Stopwatch.GetTimestamp();
        InvalidateAfterTextMutation(commandType);
        var invalidateAfterMutationMs = Stopwatch.GetElapsedTime(invalidateAfterMutationStart).TotalMilliseconds;
        _perfTracker.RecordStructuredEnterCommitBreakdown(
            mutationBatchMs,
            applyOperationMs,
            commitTransactionMs,
            selectionMs,
            traceInvariantsMs,
            ensureCaretVisibleMs,
            invalidateAfterMutationMs);
        return true;
    }

    private bool CommitParagraphMerge(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int previousParagraphIndex,
        Paragraph beforePreviousParagraph,
        Paragraph beforeCurrentParagraph,
        Paragraph mergedParagraph,
        long editStart)
    {
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(
                new MergeStructuredParagraphOperation(
                    previousParagraphIndex,
                    DocumentEditing.GetParagraphText(beforePreviousParagraph),
                    DocumentEditing.GetParagraphText(beforeCurrentParagraph),
                    DocumentEditing.GetParagraphText(mergedParagraph),
                    beforePreviousParagraph,
                    beforeCurrentParagraph,
                    mergedParagraph));
            session.CommitTransaction();
        });
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private static Paragraph CreateParagraphFromStyledRange(Paragraph source, int start, int end)
    {
        var paragraph = new Paragraph();
        var clampedStart = Math.Max(0, start);
        var clampedEnd = Math.Max(clampedStart, end);
        AppendStyledInlineRangeFromParagraph(source, clampedStart, clampedEnd, paragraph);
        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return paragraph;
    }

    private static void ReplaceParagraphInlines(Paragraph target, Paragraph source)
    {
        target.Inlines.Clear();
        while (source.Inlines.Count > 0)
        {
            var inline = source.Inlines[0];
            source.Inlines.RemoveAt(0);
            target.Inlines.Add(inline);
        }

        if (target.Inlines.Count == 0)
        {
            target.Inlines.Add(new Run(string.Empty));
        }
    }

    private static FlowDocument? GetDocumentFromElement(TextElement element)
    {
        TextElement? current = element;
        while (current is not null)
        {
            if (current is FlowDocument document)
            {
                return document;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool InsertParagraphAfterBlock(Block block, Paragraph paragraph)
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            doc.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            section.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            listItem.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is TableCell cell)
        {
            var index = cell.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            cell.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        return false;
    }

    private static bool RemoveBlockFromParent(Block block)
    {
        if (block.Parent is FlowDocument doc)
        {
            return doc.Blocks.Remove(block);
        }

        if (block.Parent is Section section)
        {
            return section.Blocks.Remove(block);
        }

        if (block.Parent is ListItem listItem)
        {
            return listItem.Blocks.Remove(block);
        }

        if (block.Parent is TableCell cell)
        {
            return cell.Blocks.Remove(block);
        }

        return false;
    }

    private static bool HasFollowingSiblingBlockOfType<TBlock>(Block block)
        where TBlock : Block
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            return index >= 0 && index + 1 < doc.Blocks.Count && doc.Blocks[index + 1] is TBlock;
        }

        if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            return index >= 0 && index + 1 < section.Blocks.Count && section.Blocks[index + 1] is TBlock;
        }

        return false;
    }

    private static Paragraph? TryFindParagraphAfterBlock(Block block)
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < doc.Blocks.Count && doc.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < section.Blocks.Count && section.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < listItem.Blocks.Count && listItem.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is TableCell cell)
        {
            var index = cell.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < cell.Blocks.Count && cell.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }

        return null;
    }

    private static int FindParagraphStartOffset(FlowDocument document, Paragraph paragraph)
    {
        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].Paragraph, paragraph))
            {
                return entries[i].StartOffset;
            }
        }

        return -1;
    }

    private static bool InsertParagraphAfter(Paragraph paragraph, Paragraph newParagraph)
    {
        if (paragraph.Parent is FlowDocument flowDocument)
        {
            var index = flowDocument.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            flowDocument.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            listItem.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is TableCell tableCell)
        {
            var index = tableCell.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            tableCell.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            section.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        return false;
    }

    private static void AppendStyledInlineRangeFromParagraph(Paragraph source, int start, int end, Paragraph destination)
    {
        if (end <= start)
        {
            return;
        }

        var cursor = 0;
        for (var i = 0; i < source.Inlines.Count; i++)
        {
            AppendInlineRange(source.Inlines[i], start, end, ref cursor, destination.Inlines.Add);
        }
    }

    private static void AppendInlineRange(Inline inline, int start, int end, ref int cursor, Action<Inline> append)
    {
        var inlineStart = cursor;
        var inlineLength = GetInlineLogicalLength(inline);
        var inlineEnd = inlineStart + inlineLength;
        cursor = inlineEnd;

        var overlapStart = Math.Max(start, inlineStart);
        var overlapEnd = Math.Min(end, inlineEnd);
        if (overlapEnd <= overlapStart)
        {
            return;
        }

        switch (inline)
        {
            case Run run:
            {
                var localStart = overlapStart - inlineStart;
                var localLength = overlapEnd - overlapStart;
                if (localLength <= 0)
                {
                    return;
                }

                var text = run.Text.Substring(localStart, localLength);
                var clone = new Run(text)
                {
                    Foreground = run.Foreground
                };
                append(clone);
                return;
            }
            case LineBreak:
                append(new LineBreak());
                return;
            case Bold or Italic or Underline or Hyperlink or Span:
            {
                if (inline is not Span span)
                {
                    return;
                }

                var clonedSpan = CloneSpanShell(span);
                var nestedCursor = inlineStart;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    AppendInlineRange(span.Inlines[i], start, end, ref nestedCursor, clonedSpan.Inlines.Add);
                }

                if (clonedSpan.Inlines.Count > 0)
                {
                    append(clonedSpan);
                }

                return;
            }
            case InlineUIContainer:
                append(new InlineUIContainer
                {
                    Child = ((InlineUIContainer)inline).Child
                });
                return;
            default:
                return;
        }
    }

    private static Span CloneSpanShell(Span span)
    {
        return span switch
        {
            Bold => new Bold(),
            Italic => new Italic(),
            Underline => new Underline(),
            Hyperlink hyperlink => new Hyperlink { NavigateUri = hyperlink.NavigateUri },
            _ => new Span()
        };
    }

    private static void CopyParagraphSettings(Paragraph source, Paragraph destination)
    {
        destination.DefaultIncrementalTab = source.DefaultIncrementalTab;
        for (var i = 0; i < source.Tabs.Count; i++)
        {
            var tab = source.Tabs[i];
            destination.Tabs.Add(new TextTabProperties(tab.Alignment, tab.Location, tab.TabLeader, tab.AligningCharacter));
        }
    }

    private static Inline CreateReplacementInlinePreservingContext(Paragraph source, int offset, string text)
    {
        var clampedOffset = Math.Clamp(offset, 0, GetParagraphLogicalLength(source));
        var cursor = 0;
        for (var i = 0; i < source.Inlines.Count; i++)
        {
            if (TryCreateInlineWithInheritedFormatting(source.Inlines[i], clampedOffset, ref cursor, text, out var replacement))
            {
                return replacement;
            }
        }

        return new Run(text);
    }

    private static bool TryCreateInlineWithInheritedFormatting(Inline inline, int offset, ref int cursor, string text, out Inline replacement)
    {
        var inlineStart = cursor;
        var inlineLength = GetInlineLogicalLength(inline);
        var inlineEnd = inlineStart + inlineLength;
        cursor = inlineEnd;
        if (offset < inlineStart || offset > inlineEnd)
        {
            replacement = null!;
            return false;
        }

        switch (inline)
        {
            case Run run:
                replacement = new Run(text)
                {
                    Foreground = run.Foreground
                };
                return true;
            case Span span:
            {
                var nestedCursor = inlineStart;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    if (!TryCreateInlineWithInheritedFormatting(span.Inlines[i], offset, ref nestedCursor, text, out var nestedReplacement))
                    {
                        continue;
                    }

                    var clonedSpan = CloneSpanShell(span);
                    clonedSpan.Inlines.Add(nestedReplacement);
                    replacement = clonedSpan;
                    return true;
                }

                var shell = CloneSpanShell(span);
                shell.Inlines.Add(new Run(text));
                replacement = shell;
                return true;
            }
            default:
                replacement = new Run(text);
                return true;
        }
    }

    private static void NormalizeAdjacentInlines(Paragraph paragraph)
    {
        var normalized = NormalizeInlineSequence(paragraph.Inlines);
        paragraph.Inlines.Clear();
        for (var i = 0; i < normalized.Count; i++)
        {
            paragraph.Inlines.Add(normalized[i]);
        }
    }

    private static List<Inline> NormalizeInlineSequence(IEnumerable<Inline> source)
    {
        var normalized = new List<Inline>();
        foreach (var inline in source)
        {
            var current = NormalizeInline(inline);
            if (normalized.Count > 0 && TryMergeAdjacentInlines(normalized[^1], current))
            {
                continue;
            }

            normalized.Add(current);
        }

        return normalized;
    }

    private static Inline NormalizeInline(Inline inline)
    {
        if (inline is not Span span)
        {
            return inline;
        }

        var normalizedChildren = NormalizeInlineSequence(span.Inlines);
        span.Inlines.Clear();
        for (var i = 0; i < normalizedChildren.Count; i++)
        {
            span.Inlines.Add(normalizedChildren[i]);
        }

        return span;
    }

    private static bool TryMergeAdjacentInlines(Inline previous, Inline current)
    {
        if (previous is Run previousRun && current is Run currentRun && previousRun.Foreground == currentRun.Foreground)
        {
            previousRun.Text += currentRun.Text;
            return true;
        }

        if (previous is not Span previousSpan || current is not Span currentSpan || !CanMergeSpanShells(previousSpan, currentSpan))
        {
            return false;
        }

        while (currentSpan.Inlines.Count > 0)
        {
            var child = currentSpan.Inlines[0];
            currentSpan.Inlines.RemoveAt(0);
            previousSpan.Inlines.Add(child);
        }

        return true;
    }

    private static bool CanMergeSpanShells(Span left, Span right)
    {
        if (left.GetType() != right.GetType())
        {
            return false;
        }

        if (left is Hyperlink leftHyperlink && right is Hyperlink rightHyperlink)
        {
            return string.Equals(leftHyperlink.NavigateUri, rightHyperlink.NavigateUri, StringComparison.Ordinal);
        }

        return true;
    }

    private void ExecuteEnterParagraphBreak()
    {
        RecordOperation("Command", "EnterParagraphBreak");
        if (IsReadOnly)
        {
            RecordOperation("Branch", "EnterParagraphBreak->ReadOnlyNoop");
            return;
        }

        if (TryInsertParagraphBreakWithinStructuredParagraph("EnterParagraphBreak", GroupingPolicy.StructuralAtomic))
        {
            RecordOperation("Branch", "EnterParagraphBreak->Structured");
            return;
        }

        RecordOperation("Branch", "EnterParagraphBreak->FallbackFragmentInsert");
        var fragment = new FlowDocument();
        fragment.Blocks.Add(CreateParagraph(string.Empty));
        fragment.Blocks.Add(CreateParagraph(string.Empty));
        ReplaceSelectionWithFragment(fragment, "EnterParagraphBreak", GroupingPolicy.StructuralAtomic);
    }

    private void ExecuteEnterLineBreak()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "EnterLineBreak",
            GroupingPolicy.StructuralAtomic,
            static (doc, start, length, _) =>
            {
                var fragment = new FlowDocument();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new LineBreak());
                fragment.Blocks.Add(paragraph);
                var merged = BuildDocumentWithFragment(doc, fragment, start, length);
                DocumentEditing.ReplaceDocumentContent(doc, merged);
                return true;
            },
            postApply: (_, start, _, _) =>
            {
                UpdateSelectionState(start + 1, start + 1, ensureCaretVisible: false);
            });
    }

    private void ExecuteTabForward()
    {
        if (TryMoveCaretToAdjacentTableCell(forward: true))
        {
            return;
        }

        ReplaceSelection("\t", "TabForward", GroupingPolicy.StructuralAtomic);
    }

    private void ExecuteTabBackward()
    {
        if (TryMoveCaretToAdjacentTableCell(forward: false))
        {
            return;
        }

        if (CanExecuteListLevelChange(increase: false))
        {
            ExecuteDecreaseListLevel();
            return;
        }
    }

    private void ReplaceSelection(string replacement, string commandType, GroupingPolicy policy)
    {
        const int BulkReplacementEventDeferralThreshold = 256;
        if (ShouldGuardRichFallback(commandType))
        {
            if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(replacement ?? string.Empty, commandType, policy))
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:ReroutedStylePreserving");
                return;
            }

            if (string.IsNullOrEmpty(replacement) &&
                SelectionLength > 0 &&
                TryDeleteSelectionPreservingStructure(commandType, policy))
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:ReroutedStructuredDelete");
                return;
            }

            if (!IsFullDocumentSelection())
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:BlockedInRichMode");
                return;
            }
        }

        RecordOperation(
            "MutationRoute",
            $"ReplaceSelection cmd={commandType} policy={policy} start={SelectionStart} len={SelectionLength} insLen={(replacement ?? string.Empty).Length}");
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var start = SelectionStart;
        var length = SelectionLength;
        var caretBefore = _caretIndex;
        var selectionStartBefore = start;
        var selectionLengthBefore = length;
        var normalizedReplacement = replacement ?? string.Empty;
        var deferEventFlush = normalizedReplacement.Length >= BulkReplacementEventDeferralThreshold;
        var caretAfter = start + normalizedReplacement.Length;
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                caretBefore,
                caretAfter,
                selectionStartBefore,
                selectionLengthBefore,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            DocumentEditing.ReplaceTextRange(Document, start, length, normalizedReplacement, session);
            UpdateSelectionState(start + normalizedReplacement.Length, start + normalizedReplacement.Length, ensureCaretVisible: false);
            session.CommitTransaction();
        }, deferEventFlush);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
    }

    private void InsertTypingFormattedText(string text, string commandType)
    {
        var fragment = CreateTypingFragment(text);
        ReplaceSelectionWithFragment(fragment, commandType, GroupingPolicy.TypingBurst);
    }

    private bool TryInsertTypingFormattedTextWithinParagraph(string text, string commandType, GroupingPolicy policy)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\n') >= 0)
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        var localStart = start - entries[paragraphIndex].StartOffset;
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var beforeText = paragraphText[..localStart];
        var afterText = paragraphText[(localStart + localLength)..];

        var editStart = Stopwatch.GetTimestamp();
        var beforeParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        var paragraph = new Paragraph();
        paragraph.Inlines.Clear();
        if (beforeText.Length > 0)
        {
            paragraph.Inlines.Add(new Run(beforeText));
        }

        Inline typed = new Run(text);
        if (_typingUnderlineActive)
        {
            var underline = new Underline();
            underline.Inlines.Add(typed);
            typed = underline;
        }

        if (_typingItalicActive)
        {
            var italic = new Italic();
            italic.Inlines.Add(typed);
            typed = italic;
        }

        if (_typingBoldActive)
        {
            var bold = new Bold();
            bold.Inlines.Add(typed);
            typed = bold;
        }

        paragraph.Inlines.Add(typed);
        if (afterText.Length > 0)
        {
            paragraph.Inlines.Add(new Run(afterText));
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        var caretAfter = start + text.Length;
        RecordOperation(
            "MutationRoute",
            $"InsertTextStyledStructured cmd={commandType} policy={policy} start={start} len={length} insLen={text.Length}");
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            paragraph,
            editStart);
    }

    private FlowDocument CreateTypingFragment(string text)
    {
        var fragment = new FlowDocument();
        var paragraph = new Paragraph();
        Inline inline = new Run(text ?? string.Empty);
        if (_typingUnderlineActive)
        {
            var underline = new Underline();
            underline.Inlines.Add(inline);
            inline = underline;
        }

        if (_typingItalicActive)
        {
            var italic = new Italic();
            italic.Inlines.Add(inline);
            inline = italic;
        }

        if (_typingBoldActive)
        {
            var bold = new Bold();
            bold.Inlines.Add(inline);
            inline = bold;
        }

        paragraph.Inlines.Add(inline);
        fragment.Blocks.Add(paragraph);
        return fragment;
    }

    private void ReplaceSelectionWithFragment(FlowDocument fragment, string commandType, GroupingPolicy policy)
    {
        if (IsRichStructuredDocument() &&
            RichGuardedFragmentCommands.Contains(commandType) &&
            !IsFullDocumentSelection())
        {
            RecordOperation("Guard", $"ReplaceSelectionWithFragment->{commandType}:BlockedInRichMode");
            return;
        }

        RecordOperation(
            "MutationRoute",
            $"ReplaceSelectionWithFragment cmd={commandType} policy={policy} start={SelectionStart} len={SelectionLength} fragBlocks={fragment.Blocks.Count}");
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var insertedText = DocumentEditing.GetText(fragment);
        var afterDocument = BuildDocumentWithFragment(Document, fragment, selectionStart, selectionLength);
        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = selectionStart + insertedText.Length;

        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                selectionStart,
                selectionLength,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
            session.CommitTransaction();
        });

        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
    }

    private static FlowDocument BuildDocumentWithFragment(FlowDocument current, FlowDocument fragment, int selectionStart, int selectionLength)
    {
        var text = DocumentEditing.GetText(current);
        var clampedStart = Math.Clamp(selectionStart, 0, text.Length);
        var clampedLength = Math.Clamp(selectionLength, 0, text.Length - clampedStart);
        var clampedEnd = clampedStart + clampedLength;
        var prefix = SliceDocumentRange(current, 0, clampedStart);
        var suffix = SliceDocumentRange(current, clampedEnd, text.Length);
        var fragmentClone = DocumentEditing.CloneDocument(fragment);
        var isSingleParagraphInsert = clampedLength == 0 &&
                                      fragmentClone.Blocks.Count == 1 &&
                                      fragmentClone.Blocks[0] is Paragraph;

        var result = new FlowDocument();
        AppendDocumentBlocks(result, prefix, mergeParagraphBoundary: false);
        AppendDocumentBlocks(
            result,
            fragmentClone,
            mergeParagraphBoundary: !IsAtParagraphStart(current, clampedStart));
        AppendDocumentBlocks(
            result,
            suffix,
            mergeParagraphBoundary: (!IsAtParagraphStart(current, clampedEnd) &&
                                     !IsAtParagraphSeparator(current, clampedEnd)) ||
                                    isSingleParagraphInsert);

        if (result.Blocks.Count == 0)
        {
            result.Blocks.Add(CreateParagraph(string.Empty));
        }

        return result;
    }

    private static FlowDocument SliceDocumentRange(FlowDocument source, int startOffset, int endOffset)
    {
        if (endOffset <= startOffset)
        {
            return new FlowDocument();
        }

        var xml = FlowDocumentSerializer.SerializeRange(source, startOffset, endOffset);
        return FlowDocumentSerializer.DeserializeFragment(xml);
    }

    private static void AppendDocumentBlocks(FlowDocument target, FlowDocument source, bool mergeParagraphBoundary)
    {
        if (mergeParagraphBoundary &&
            target.Blocks.Count > 0 &&
            source.Blocks.Count > 0 &&
            target.Blocks[target.Blocks.Count - 1] is Paragraph destinationParagraph &&
            source.Blocks[0] is Paragraph sourceParagraph)
        {
            while (sourceParagraph.Inlines.Count > 0)
            {
                var inline = sourceParagraph.Inlines[0];
                sourceParagraph.Inlines.RemoveAt(0);
                destinationParagraph.Inlines.Add(inline);
            }

            if (destinationParagraph.Inlines.Count == 0)
            {
                destinationParagraph.Inlines.Add(new Run(string.Empty));
            }

            source.Blocks.RemoveAt(0);
        }

        while (source.Blocks.Count > 0)
        {
            var block = source.Blocks[0];
            source.Blocks.RemoveAt(0);
            target.Blocks.Add(block);
        }
    }

    private static bool IsAtParagraphStart(FlowDocument document, int offset)
    {
        if (offset <= 0)
        {
            return true;
        }

        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].StartOffset == offset)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAtParagraphSeparator(FlowDocument document, int offset)
    {
        if (offset < 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count - 1; i++)
        {
            if (entries[i].EndOffset == offset)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRichStructuredDocument()
    {
        return DocumentContainsRichInlineFormatting(Document);
    }

    private bool ShouldGuardRichFallback(string commandType)
    {
        return IsRichStructuredDocument() && RichGuardedReplaceSelectionCommands.Contains(commandType);
    }

    private bool IsFullDocumentSelection()
    {
        var textLength = GetText().Length;
        return SelectionStart == 0 && SelectionLength >= textLength;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text ?? string.Empty));
        return paragraph;
    }

    private void ApplyStructuralEdit(
        string reason,
        GroupingPolicy policy,
        Func<FlowDocument, int, int, int, bool> applyMutation,
        Action<FlowDocument, int, int, int>? postApply = null)
    {
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        if (!applyMutation(afterDocument, selectionStart, selectionLength, _caretIndex))
        {
            return;
        }

        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            reason,
            policy,
            new DocumentEditContext(
                _caretIndex,
                _caretIndex,
                selectionStart,
                selectionLength,
                selectionStart,
                0,
                reason));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });

        postApply?.Invoke(Document, selectionStart, selectionLength, _caretIndex);
        UpdateSelectionState(_caretIndex, _caretIndex, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(reason);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(reason);
    }

    private static List<ParagraphSelectionEntry> ResolveSelectedParagraphs(FlowDocument document, int selectionStart, int selectionLength, int caretOffset)
    {
        var entries = CollectParagraphEntries(document);
        if (entries.Count == 0)
        {
            return entries;
        }

        if (selectionLength <= 0)
        {
            var point = Math.Clamp(caretOffset, 0, Math.Max(0, entries[entries.Count - 1].EndOffset));
            for (var i = 0; i < entries.Count; i++)
            {
                if (point >= entries[i].StartOffset && point <= entries[i].EndOffset)
                {
                    return [entries[i]];
                }
            }

            return [entries[entries.Count - 1]];
        }

        var start = Math.Clamp(selectionStart, 0, int.MaxValue);
        var end = start + Math.Max(0, selectionLength);
        var selected = new List<ParagraphSelectionEntry>();
        for (var i = 0; i < entries.Count; i++)
        {
            var overlaps = entries[i].EndOffset > start && entries[i].StartOffset < end;
            if (overlaps)
            {
                selected.Add(entries[i]);
            }
        }

        if (selected.Count == 0)
        {
            selected.Add(entries[0]);
        }

        return selected;
    }

    private static List<ParagraphSelectionEntry> CollectParagraphEntries(FlowDocument document)
    {
        var entries = new List<ParagraphSelectionEntry>();
        var runningOffset = 0;
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(document));
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var length = GetParagraphLogicalLength(paragraph);
            entries.Add(new ParagraphSelectionEntry(paragraph, runningOffset, runningOffset + length));
            runningOffset += length;
            if (i < paragraphs.Count - 1)
            {
                runningOffset += 1;
            }
        }

        return entries;
    }

    private static int GetParagraphLogicalLength(Paragraph paragraph)
    {
        var length = 0;
        foreach (var inline in paragraph.Inlines)
        {
            length += GetInlineLogicalLength(inline);
        }

        return length;
    }

    private static int GetInlineLogicalLength(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                return run.Text.Length;
            case LineBreak:
                return 1;
            case Span span:
            {
                var total = 0;
                foreach (var nested in span.Inlines)
                {
                    total += GetInlineLogicalLength(nested);
                }

                return total;
            }
            case InlineUIContainer:
                return 1;
            default:
                return 0;
        }
    }

    private static FlowDocument BuildDocumentWithFragment(FlowDocument current, Table table, int selectionStart, int selectionLength)
    {
        var fragment = new FlowDocument();
        fragment.Blocks.Add(table);
        return BuildDocumentWithFragment(current, fragment, selectionStart, selectionLength);
    }

    private static bool TryGetAncestor<T>(TextElement element, out T ancestor)
        where T : TextElement
    {
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (current is T typed)
            {
                ancestor = typed;
                return true;
            }
        }

        ancestor = null!;
        return false;
    }

    private readonly record struct ParagraphSelectionEntry(Paragraph Paragraph, int StartOffset, int EndOffset);
}
