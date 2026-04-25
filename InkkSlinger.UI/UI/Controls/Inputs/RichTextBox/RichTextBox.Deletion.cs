using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private bool TryBackspaceAtStructuredParagraphStart(string commandType, GroupingPolicy policy)
    {
        RecordOperation("StructuredBackspace", "Begin");
        if (SelectionLength > 0)
        {
            RecordOperation("StructuredBackspace", "SelectionNotCollapsed");
            return false;
        }

        var caret = _caretIndex;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            RecordOperation("StructuredBackspace", "NoParagraphEntries");
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].StartOffset == caret)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            RecordOperation("StructuredBackspace", "CaretNotAtParagraphStart");
            return false;
        }

        var sourceCurrent = entries[paragraphIndex].Paragraph;
        if (TryGetImmediatePreviousParagraph(sourceCurrent, out var sourcePrevious))
        {
            var editStartLocal = Stopwatch.GetTimestamp();
            var beforePreviousParagraph = DocumentEditing.CloneParagraph(sourcePrevious);
            var beforeCurrentParagraph = DocumentEditing.CloneParagraph(sourceCurrent);
            var mergedParagraph = DocumentEditing.CloneParagraph(sourcePrevious);
            var caretAfterLocal = FindParagraphStartOffset(Document, sourcePrevious) + GetParagraphLogicalLength(sourcePrevious);
            AppendParagraphContentPreservingStyles(mergedParagraph, sourceCurrent);
            RecordOperation("StructuredBackspace", "SiblingParagraphMergeApplied");
            return CommitParagraphMerge(
                commandType,
                policy,
                Math.Max(0, caret - 1),
                1,
                caretAfterLocal,
                paragraphIndex - 1,
                beforePreviousParagraph,
                beforeCurrentParagraph,
                mergedParagraph,
                editStartLocal);
        }

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            RecordOperation("StructuredBackspace", "ParagraphIndexOutOfRangeAfterClone");
            return false;
        }

        var current = paragraphs[paragraphIndex];
        if (current.Parent is FlowDocument)
        {
            if (!TryBackspaceAtTopLevelParagraphStart(current, out var caretAfterTopLevel))
            {
                RecordOperation("StructuredBackspace", "TopLevelMergeRejected");
                return false;
            }

            RecordOperation("StructuredBackspace", "TopLevelMergeApplied");

            var afterTextTopLevel = DocumentEditing.GetText(afterDocument);
            var topLevelSession = new DocumentEditSession(Document, _undoManager);
            topLevelSession.BeginTransaction(
                commandType,
                policy,
                new DocumentEditContext(
                    _caretIndex,
                    caretAfterTopLevel,
                    Math.Max(0, caret - 1),
                    1,
                    caretAfterTopLevel,
                    0,
                    commandType));
            ExecuteTextMutationBatch(() =>
            {
                topLevelSession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextTopLevel, beforeDocument, afterDocument));
                topLevelSession.CommitTransaction();
            });
            UpdateSelectionState(caretAfterTopLevel, caretAfterTopLevel, ensureCaretVisible: false);
            var elapsedTopLevelMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            _perfTracker.RecordEdit(elapsedTopLevelMs);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateAfterTextMutation(commandType);
            return true;
        }

        if (!TryGetPreviousSiblingParagraph(current, out var previous))
        {
            if (!TryMergeListItemBoundaryAtParagraphStart(current, out previous))
            {
                RecordOperation("StructuredBackspace", "NoPreviousSiblingOrListBoundaryMerge");
                return false;
            }

            RecordOperation("StructuredBackspace", "ListBoundaryMergeApplied");

            var afterTextBoundary = DocumentEditing.GetText(afterDocument);
            var caretAfterBoundary = Math.Max(0, caret - 1);
            var boundarySession = new DocumentEditSession(Document, _undoManager);
            boundarySession.BeginTransaction(
                commandType,
                policy,
                new DocumentEditContext(
                    _caretIndex,
                    caretAfterBoundary,
                    caret - 1,
                    1,
                    caretAfterBoundary,
                    0,
                    commandType));
            ExecuteTextMutationBatch(() =>
            {
                boundarySession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextBoundary, beforeDocument, afterDocument));
                boundarySession.CommitTransaction();
            });
            UpdateSelectionState(caretAfterBoundary, caretAfterBoundary, ensureCaretVisible: false);
            var elapsedBoundaryMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            _perfTracker.RecordEdit(elapsedBoundaryMs);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateAfterTextMutation(commandType);
            return true;
        }

        AppendParagraphContentPreservingStyles(previous, current);
        if (!RemoveParagraphFromParent(current))
        {
            RecordOperation("StructuredBackspace", "RemoveParagraphFromParentFailed");
            return false;
        }

        RecordOperation("StructuredBackspace", "SiblingParagraphMergeApplied");

        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = Math.Max(0, caret - 1);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                caret - 1,
                1,
                caretAfter,
                0,
                commandType));
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
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

    private static bool TryBackspaceAtTopLevelParagraphStart(Paragraph current, out int caretAfter)
    {
        caretAfter = 0;
        if (current.Parent is not FlowDocument document)
        {
            return false;
        }

        var currentIndex = document.Blocks.IndexOf(current);
        if (currentIndex <= 0)
        {
            return false;
        }

        var previousBlock = document.Blocks[currentIndex - 1];
        if (previousBlock is Paragraph previousTopLevelParagraph)
        {
            var previousTextLength = GetParagraphLogicalLength(previousTopLevelParagraph);
            AppendParagraphContentPreservingStyles(previousTopLevelParagraph, current);
            document.Blocks.RemoveAt(currentIndex);
            caretAfter = FindParagraphStartOffset(document, previousTopLevelParagraph) + previousTextLength;
            return caretAfter >= 0;
        }

        if (previousBlock is not InkkSlinger.List list || list.Items.Count == 0)
        {
            return false;
        }

        if (!TryGetLastParagraph(list.Items[list.Items.Count - 1], out var previousParagraph))
        {
            return false;
        }

        var previousListTextLength = GetParagraphLogicalLength(previousParagraph);
        AppendParagraphContentPreservingStyles(previousParagraph, current);
        document.Blocks.RemoveAt(currentIndex);

        var owner = GetDocumentFromElement(list);
        if (owner is null)
        {
            return false;
        }

        caretAfter = FindParagraphStartOffset(owner, previousParagraph) + previousListTextLength;
        return caretAfter >= 0;
    }

    private static bool TryMergeListItemBoundaryAtParagraphStart(Paragraph current, out Paragraph previous)
    {
        previous = null!;
        if (current.Parent is not ListItem currentItem || currentItem.Parent is not InkkSlinger.List list)
        {
            return false;
        }

        if (currentItem.Blocks.Count == 0 || !ReferenceEquals(currentItem.Blocks[0], current))
        {
            return false;
        }

        var itemIndex = list.Items.IndexOf(currentItem);
        if (itemIndex <= 0)
        {
            return false;
        }

        var previousItem = list.Items[itemIndex - 1];
        if (!TryGetLastParagraph(previousItem, out previous))
        {
            return false;
        }

        AppendParagraphContentPreservingStyles(previous, current);
        list.Items.Remove(currentItem);
        return true;
    }

    private static void AppendParagraphContentPreservingStyles(Paragraph destination, Paragraph source)
    {
        if (destination.Inlines.Count == 1 &&
            destination.Inlines[0] is Run existingRun &&
            string.IsNullOrEmpty(existingRun.Text))
        {
            destination.Inlines.Clear();
        }

        AppendStyledInlineRangeFromParagraph(source, 0, GetParagraphLogicalLength(source), destination);
        if (destination.Inlines.Count == 0)
        {
            destination.Inlines.Add(new Run(string.Empty));
        }
    }

    private static bool TryGetLastParagraph(ListItem item, out Paragraph paragraph)
    {
        for (var i = item.Blocks.Count - 1; i >= 0; i--)
        {
            if (item.Blocks[i] is Paragraph p)
            {
                paragraph = p;
                return true;
            }
        }

        paragraph = null!;
        return false;
    }

    private static bool TryGetPreviousSiblingParagraph(Paragraph paragraph, out Paragraph previous)
    {
        if (paragraph.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(paragraph);
            if (index > 0 && listItem.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }
        else if (paragraph.Parent is TableCell tableCell)
        {
            var index = tableCell.Blocks.IndexOf(paragraph);
            if (index > 0 && tableCell.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }
        else if (paragraph.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(paragraph);
            if (index > 0 && section.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }

        previous = null!;
        return false;
    }

    private static bool TryGetImmediatePreviousParagraph(Paragraph paragraph, out Paragraph previous)
    {
        if (paragraph.Parent is FlowDocument document)
        {
            var index = document.Blocks.IndexOf(paragraph);
            if (index > 0 && document.Blocks[index - 1] is Paragraph previousParagraph)
            {
                previous = previousParagraph;
                return true;
            }
        }

        if (TryGetPreviousSiblingParagraph(paragraph, out previous))
        {
            return true;
        }

        previous = null!;
        return false;
    }

    private static bool RemoveParagraphFromParent(Paragraph paragraph)
    {
        if (paragraph.Parent is ListItem listItem)
        {
            return listItem.Blocks.Remove(paragraph);
        }

        if (paragraph.Parent is TableCell tableCell)
        {
            return tableCell.Blocks.Remove(paragraph);
        }

        if (paragraph.Parent is Section section)
        {
            return section.Blocks.Remove(paragraph);
        }

        return false;
    }

    private void ExecuteBackspace()
    {
        RecordOperation("Command", "Backspace");
        if (TryHandleTableBoundaryDeletion(backspace: true))
        {
            RecordOperation("Branch", "Backspace->TableBoundaryDeletion");
            return;
        }

        if (SelectionLength > 0)
        {
            if (TryDeleteSelectionPreservingStructure("DeleteSelection", GroupingPolicy.StructuralAtomic))
            {
                RecordOperation("Branch", "Backspace->DeleteSelectionStructured");
                return;
            }

            RecordOperation("Branch", "Backspace->DeleteSelection");
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        if (_caretIndex <= 0)
        {
            RecordOperation("Branch", "Backspace->NoopAtStart");
            return;
        }

        if (TryBackspaceAtStructuredParagraphStart("Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredParagraphStart");
            return;
        }

        _selectionAnchor = _caretIndex - 1;
        if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(string.Empty, "Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredCharacterDeleteStyled");
            return;
        }

        if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(string.Empty, "Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredCharacterDelete");
            return;
        }

        RecordOperation("Branch", "Backspace->FallbackReplaceSelection");
        ReplaceSelection(string.Empty, "Backspace", GroupingPolicy.DeletionBurst);
    }

    private void ExecuteDelete()
    {
        RecordOperation("Command", "Delete");
        if (TryHandleTableBoundaryDeletion(backspace: false))
        {
            RecordOperation("Branch", "Delete->TableBoundaryDeletion");
            return;
        }

        if (SelectionLength > 0)
        {
            if (TryDeleteSelectionPreservingStructure("DeleteSelection", GroupingPolicy.StructuralAtomic))
            {
                RecordOperation("Branch", "Delete->DeleteSelectionStructured");
                return;
            }

            RecordOperation("Branch", "Delete->DeleteSelection");
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = GetText();
        if (_caretIndex >= text.Length)
        {
            RecordOperation("Branch", "Delete->NoopAtEnd");
            return;
        }

        _selectionAnchor = _caretIndex + 1;
        if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Delete->StructuredCharacterDeleteStyled");
            return;
        }

        if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Delete->StructuredCharacterDelete");
            return;
        }

        if (DocumentContainsRichInlineFormatting(Document) &&
            IsAtParagraphSeparator(Document, _caretIndex))
        {
            RecordOperation("Branch", "Delete->RichParagraphBoundaryNoop");
            return;
        }

        RecordOperation("Branch", "Delete->FallbackReplaceSelection");
        ReplaceSelection(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst);
    }

    private bool TryDeleteSelectionPreservingStructure(string commandType, GroupingPolicy policy)
    {
        var start = SelectionStart;
        var length = SelectionLength;
        if (length <= 0)
        {
            return false;
        }

        if (!DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var textLength = GetText().Length;
        if (start == 0 && length >= textLength)
        {
            RecordOperation("DeleteSelectionStructured", "SkipForFullDocumentSelection");
            return false;
        }

        var end = start + length;
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var firstTableParagraphStart = int.MaxValue;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Paragraph.Parent is TableCell)
            {
                firstTableParagraphStart = Math.Min(firstTableParagraphStart, entries[i].StartOffset);
            }
        }

        var protectTableParagraphs = false;
        if (firstTableParagraphStart != int.MaxValue)
        {
            if (start < firstTableParagraphStart && end > firstTableParagraphStart)
            {
                end = firstTableParagraphStart;
                length = Math.Max(0, end - start);
                if (length <= 0)
                {
                    return false;
                }

                protectTableParagraphs = true;
                RecordOperation("DeleteSelectionStructured", $"ClampToTableBoundary start={start} end={end}");
            }
            else if (end <= firstTableParagraphStart)
            {
                protectTableParagraphs = true;
            }
        }

        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphs.Count != entries.Count)
        {
            return false;
        }

        var changed = false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (protectTableParagraphs && paragraphs[i].Parent is TableCell)
            {
                continue;
            }

            var overlapStart = Math.Max(start, entries[i].StartOffset);
            var overlapEnd = Math.Min(end, entries[i].EndOffset);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var paragraph = paragraphs[i];
            paragraph.Inlines.Clear();
            var paragraphLength = Math.Max(0, entries[i].EndOffset - entries[i].StartOffset);
            var localStart = Math.Clamp(overlapStart - entries[i].StartOffset, 0, paragraphLength);
            var localEnd = Math.Clamp(overlapEnd - entries[i].StartOffset, localStart, paragraphLength);
            AppendStyledInlineRangeFromParagraph(entries[i].Paragraph, 0, localStart, paragraph);
            AppendStyledInlineRangeFromParagraph(entries[i].Paragraph, localEnd, paragraphLength, paragraph);
            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }

            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        RemoveFullyDeletedTopLevelParagraphs(afterDocument, entries, paragraphs, start, end);
        PruneEmptyStructuralScaffolding(afterDocument);

        RecordOperation("MutationRoute", $"DeleteSelectionStructured cmd={commandType} policy={policy} start={start} len={length}");
        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = start;
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
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private static void PruneEmptyStructuralScaffolding(FlowDocument document)
    {
        for (var blockIndex = document.Blocks.Count - 1; blockIndex >= 0; blockIndex--)
        {
            if (document.Blocks[blockIndex] is not InkkSlinger.List list)
            {
                continue;
            }

            for (var itemIndex = list.Items.Count - 1; itemIndex >= 0; itemIndex--)
            {
                if (ListItemHasVisibleContent(list.Items[itemIndex]))
                {
                    continue;
                }

                list.Items.RemoveAt(itemIndex);
            }

            if (list.Items.Count == 0)
            {
                document.Blocks.RemoveAt(blockIndex);
            }
        }

        PruneRedundantEmptyTopLevelParagraphs(document);
    }

    private static void PruneRedundantEmptyTopLevelParagraphs(FlowDocument document)
    {
        var keepTrailingEmptyParagraph = true;
        for (var i = document.Blocks.Count - 1; i >= 0; i--)
        {
            if (document.Blocks[i] is not Paragraph paragraph)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(FlowDocumentPlainText.GetInlineText(paragraph.Inlines)))
            {
                break;
            }

            if (keepTrailingEmptyParagraph)
            {
                keepTrailingEmptyParagraph = false;
                continue;
            }

            if (document.Blocks.Count > 1)
            {
                document.Blocks.RemoveAt(i);
            }
        }
    }

    private static void RemoveFullyDeletedTopLevelParagraphs(
        FlowDocument document,
        IReadOnlyList<ParagraphSelectionEntry> entries,
        IReadOnlyList<Paragraph> paragraphs,
        int selectionStart,
        int selectionEnd)
    {
        for (var i = paragraphs.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(paragraphs[i].Parent, document))
            {
                continue;
            }

            if (!ShouldRemoveFullyDeletedTopLevelParagraph(entries, i, selectionStart, selectionEnd))
            {
                continue;
            }

            document.Blocks.Remove(paragraphs[i]);
        }

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph(string.Empty));
        }
    }

    private static bool ShouldRemoveFullyDeletedTopLevelParagraph(
        IReadOnlyList<ParagraphSelectionEntry> entries,
        int paragraphIndex,
        int selectionStart,
        int selectionEnd)
    {
        if (paragraphIndex < 0 || paragraphIndex >= entries.Count - 1)
        {
            return false;
        }

        var entry = entries[paragraphIndex];
        var paragraphFullySelected = selectionStart <= entry.StartOffset && selectionEnd >= entry.EndOffset;
        if (!paragraphFullySelected)
        {
            return false;
        }

        return selectionEnd > entry.EndOffset;
    }
}