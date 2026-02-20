using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private bool CanApplyInlineFormat()
    {
        return !IsReadOnly && SelectionLength > 0;
    }

    private void ExecuteToggleBold()
    {
        RecordOperation("Command", "ToggleBold");
        if (SelectionLength <= 0)
        {
            _typingBoldActive = !_typingBoldActive;
            RecordOperation("Branch", $"ToggleBold->TypingMode:{_typingBoldActive}");
            return;
        }

        RecordOperation("Branch", "ToggleBold->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Bold>(static () => new Bold(), "ToggleBold");
    }

    private void ExecuteToggleItalic()
    {
        RecordOperation("Command", "ToggleItalic");
        if (SelectionLength <= 0)
        {
            _typingItalicActive = !_typingItalicActive;
            RecordOperation("Branch", $"ToggleItalic->TypingMode:{_typingItalicActive}");
            return;
        }

        RecordOperation("Branch", "ToggleItalic->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Italic>(static () => new Italic(), "ToggleItalic");
    }

    private void ExecuteToggleUnderline()
    {
        RecordOperation("Command", "ToggleUnderline");
        if (SelectionLength <= 0)
        {
            _typingUnderlineActive = !_typingUnderlineActive;
            RecordOperation("Branch", $"ToggleUnderline->TypingMode:{_typingUnderlineActive}");
            return;
        }

        RecordOperation("Branch", "ToggleUnderline->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Underline>(static () => new Underline(), "ToggleUnderline");
    }

    private void ApplyInlineFormatToSelection<TSpan>(Func<Span> spanFactory, string commandType)
        where TSpan : Span
    {
        if (!CanApplyInlineFormat())
        {
            RecordOperation("FormatSelection", $"{commandType}->CannotApply");
            return;
        }

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var start = SelectionStart;
        var length = SelectionLength;
        var originalAnchor = _selectionAnchor;
        var originalCaret = _caretIndex;
        var beforeDoc = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var fragmentXml = FlowDocumentSerializer.SerializeRange(Document, start, start + length);
        var formattedFragment = FlowDocumentSerializer.DeserializeFragment(fragmentXml);
        var removeExistingFormat = SelectionIsFullyStyledBy<TSpan>(formattedFragment);
        RecordOperation("FormatSelection", $"{commandType}->Begin removeExisting={removeExistingFormat} sel=({start},{length})");
        FlowDocument afterDocument;
        if (start == 0 &&
            length == GetText().Length &&
            TryBuildDocumentWithWholeDocumentInlineFormat<TSpan>(
                Document,
                removeExistingFormat,
                spanFactory,
                out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->WholeDocumentStructuredPath");
        }
        else if (TryBuildDocumentWithWholeParagraphInlineFormat<TSpan>(
                Document,
                start,
                length,
                removeExistingFormat,
                spanFactory,
                out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->WholeParagraphPath");
            // afterDocument produced by whole-paragraph in-place transform on clone.
        }
        else if (TryBuildDocumentWithInlineFormatWithinParagraph<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->SingleParagraphStructuredPath");
        }
        else if (TryBuildDocumentWithInlineFormatAcrossParagraphs<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->MultiParagraphStructuredPath");
        }
        else if (DocumentContainsRichInlineFormatting(Document) &&
                 TryBuildDocumentWithSelectedParagraphStructuredFallback<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->RichDocumentParagraphFallbackPath");
        }
        else if (removeExistingFormat)
        {
            RecordOperation("FormatSelection", $"{commandType}->FragmentRemovePath");
            RemoveInlineSpanFromParagraphs<TSpan>(formattedFragment);
            afterDocument = BuildDocumentWithFragment(Document, formattedFragment, start, length);
        }
        else
        {
            RecordOperation("FormatSelection", $"{commandType}->FragmentApplyPath");
            ApplyInlineSpanToParagraphs(formattedFragment, spanFactory);
            afterDocument = BuildDocumentWithFragment(Document, formattedFragment, start, length);
        }
        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            GroupingPolicy.FormatBurst,
            new DocumentEditContext(
                _caretIndex,
                _caretIndex,
                start,
                length,
                start,
                length,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDoc, afterDocument));
        session.CommitTransaction();
        var maxCaret = GetText().Length;
        _selectionAnchor = Math.Clamp(originalAnchor, 0, maxCaret);
        _caretIndex = Math.Clamp(originalCaret, 0, maxCaret);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
    }

    private static bool TryBuildDocumentWithWholeDocumentInlineFormat<TSpan>(
        FlowDocument source,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = DocumentEditing.CloneDocument(source);
        if (afterDocument.Blocks.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < afterDocument.Blocks.Count; i++)
        {
            if (removeExistingFormat)
            {
                RemoveInlineSpanFromBlock<TSpan>(afterDocument.Blocks[i]);
            }
            else
            {
                ApplyInlineSpanToBlock(afterDocument.Blocks[i], spanFactory);
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithSelectedParagraphStructuredFallback<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var selected = ResolveSelectedParagraphs(source, selectionStart, selectionLength, selectionStart + selectionLength);
        if (selected.Count == 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectedStarts = new HashSet<int>();
        for (var i = 0; i < selected.Count; i++)
        {
            selectedStarts.Add(selected[i].StartOffset);
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var cloneEntries = CollectParagraphEntries(afterDocument);
        if (cloneEntries.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < cloneEntries.Count; i++)
        {
            if (!selectedStarts.Contains(cloneEntries[i].StartOffset))
            {
                continue;
            }

            var paragraph = cloneEntries[i].Paragraph;
            if (removeExistingFormat)
            {
                RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
            }
            else
            {
                WrapParagraphInlines(paragraph, spanFactory);
            }

            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithInlineFormatAcrossParagraphs<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var first = -1;
        var last = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].EndOffset <= selectionStart || entries[i].StartOffset >= selectionEnd)
            {
                continue;
            }

            first = first < 0 ? i : first;
            last = i;
        }

        if (first < 0 || last < 0 || first == last)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (last >= paragraphs.Count)
        {
            return false;
        }

        for (var index = first; index <= last; index++)
        {
            var paragraph = paragraphs[index];
            var paragraphText = FlowDocumentPlainText.GetInlineText(paragraph.Inlines);
            var localStart = Math.Clamp(selectionStart - entries[index].StartOffset, 0, paragraphText.Length);
            var localEnd = Math.Clamp(selectionEnd - entries[index].StartOffset, 0, paragraphText.Length);
            if (localEnd < localStart)
            {
                continue;
            }

            var formatLength = localEnd - localStart;
            if (formatLength <= 0)
            {
                continue;
            }

            var before = paragraphText[..localStart];
            var middle = paragraphText.Substring(localStart, formatLength);
            var after = paragraphText[localEnd..];
            paragraph.Inlines.Clear();

            if (before.Length > 0)
            {
                paragraph.Inlines.Add(new Run(before));
            }

            if (middle.Length > 0)
            {
                if (removeExistingFormat)
                {
                    paragraph.Inlines.Add(new Run(middle));
                }
                else
                {
                    var span = spanFactory();
                    span.Inlines.Add(new Run(middle));
                    paragraph.Inlines.Add(span);
                }
            }

            if (after.Length > 0)
            {
                paragraph.Inlines.Add(new Run(after));
            }

            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithInlineFormatWithinParagraph<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (selectionStart >= entries[i].StartOffset &&
                selectionEnd <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        var localStart = selectionStart - entries[paragraphIndex].StartOffset;
        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(selectionLength, 0, paragraphText.Length - localStart);
        if (localLength <= 0)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[paragraphIndex];
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var localEnd = localStart + localLength;
        var selectedText = paragraphText.Substring(localStart, localLength);

        paragraph.Inlines.Clear();
        AppendStyledInlineRangeFromParagraph(sourceParagraph, 0, localStart, paragraph);

        if (selectedText.Length > 0)
        {
            if (removeExistingFormat)
            {
                paragraph.Inlines.Add(new Run(selectedText));
            }
            else
            {
                var span = spanFactory();
                span.Inlines.Add(new Run(selectedText));
                paragraph.Inlines.Add(span);
            }
        }

        AppendStyledInlineRangeFromParagraph(sourceParagraph, localEnd, paragraphText.Length, paragraph);

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return true;
    }

    private static bool TryBuildDocumentWithWholeParagraphInlineFormat<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var targetParagraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (selectionStart == entries[i].StartOffset &&
                selectionEnd == entries[i].EndOffset)
            {
                targetParagraphIndex = i;
                break;
            }
        }

        if (targetParagraphIndex < 0)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (targetParagraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[targetParagraphIndex];
        if (removeExistingFormat)
        {
            RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
        }
        else
        {
            WrapParagraphInlines(paragraph, spanFactory);
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return true;
    }

    private static bool SelectionIsFullyStyledBy<TSpan>(FlowDocument document)
        where TSpan : Span
    {
        var hasContent = false;
        var allStyled = true;
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            EvaluateBlockStyleState<TSpan>(document.Blocks[i], inheritedStyled: false, ref hasContent, ref allStyled);
            if (!allStyled)
            {
                return false;
            }
        }

        return hasContent && allStyled;
    }

    private static void EvaluateBlockStyleState<TSpan>(Block block, bool inheritedStyled, ref bool hasContent, ref bool allStyled)
        where TSpan : Span
    {
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    EvaluateInlineStyleState<TSpan>(paragraph.Inlines[i], inheritedStyled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    EvaluateBlockStyleState<TSpan>(section.Blocks[i], inheritedStyled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        EvaluateBlockStyleState<TSpan>(item.Blocks[j], inheritedStyled, ref hasContent, ref allStyled);
                        if (!allStyled)
                        {
                            return;
                        }
                    }
                }

                break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var group = table.RowGroups[i];
                    for (var j = 0; j < group.Rows.Count; j++)
                    {
                        var row = group.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                EvaluateBlockStyleState<TSpan>(cell.Blocks[m], inheritedStyled, ref hasContent, ref allStyled);
                                if (!allStyled)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void EvaluateInlineStyleState<TSpan>(Inline inline, bool inheritedStyled, ref bool hasContent, ref bool allStyled)
        where TSpan : Span
    {
        var styled = inheritedStyled || inline is TSpan;
        switch (inline)
        {
            case Run run when run.Text.Length > 0:
                hasContent = true;
                if (!styled)
                {
                    allStyled = false;
                }

                break;
            case LineBreak:
            case InlineUIContainer:
                hasContent = true;
                if (!styled)
                {
                    allStyled = false;
                }

                break;
            case Span span:
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    EvaluateInlineStyleState<TSpan>(span.Inlines[i], styled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
        }
    }

    private static void RemoveInlineSpanFromParagraphs<TSpan>(FlowDocument document)
        where TSpan : Span
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            RemoveInlineSpanFromBlock<TSpan>(document.Blocks[i]);
        }
    }

    private static void RemoveInlineSpanFromBlock<TSpan>(Block block)
        where TSpan : Span
    {
        switch (block)
        {
            case Paragraph paragraph:
                RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    RemoveInlineSpanFromBlock<TSpan>(section.Blocks[i]);
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        RemoveInlineSpanFromBlock<TSpan>(item.Blocks[j]);
                    }
                }

                break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var group = table.RowGroups[i];
                    for (var j = 0; j < group.Rows.Count; j++)
                    {
                        var row = group.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                RemoveInlineSpanFromBlock<TSpan>(cell.Blocks[m]);
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void RemoveInlineSpanFromCollection<TSpan>(IList<Inline> inlines)
        where TSpan : Span
    {
        for (var i = 0; i < inlines.Count;)
        {
            var inline = inlines[i];
            if (inline is TSpan target)
            {
                inlines.RemoveAt(i);
                var movedChildren = new List<Inline>();
                while (target.Inlines.Count > 0)
                {
                    var child = target.Inlines[0];
                    target.Inlines.RemoveAt(0);
                    movedChildren.Add(child);
                }

                RemoveInlineSpanFromCollection<TSpan>(movedChildren);
                for (var j = 0; j < movedChildren.Count; j++)
                {
                    inlines.Insert(i + j, movedChildren[j]);
                }

                i += movedChildren.Count;
                continue;
            }

            if (inline is Span span)
            {
                RemoveInlineSpanFromCollection<TSpan>(span.Inlines);
            }

            i++;
        }
    }

    private static void ApplyInlineSpanToParagraphs(FlowDocument document, Func<Span> spanFactory)
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            ApplyInlineSpanToBlock(document.Blocks[i], spanFactory);
        }
    }

    private static void ApplyInlineSpanToBlock(Block block, Func<Span> spanFactory)
    {
        switch (block)
        {
            case Paragraph paragraph:
                WrapParagraphInlines(paragraph, spanFactory);
                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    ApplyInlineSpanToBlock(section.Blocks[i], spanFactory);
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        ApplyInlineSpanToBlock(item.Blocks[j], spanFactory);
                    }
                }

                break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var group = table.RowGroups[i];
                    for (var j = 0; j < group.Rows.Count; j++)
                    {
                        var row = group.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                ApplyInlineSpanToBlock(cell.Blocks[m], spanFactory);
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void WrapParagraphInlines(Paragraph paragraph, Func<Span> spanFactory)
    {
        if (paragraph.Inlines.Count == 0)
        {
            return;
        }

        var span = spanFactory();
        while (paragraph.Inlines.Count > 0)
        {
            var inline = paragraph.Inlines[0];
            paragraph.Inlines.RemoveAt(0);
            span.Inlines.Add(inline);
        }

        paragraph.Inlines.Add(span);
    }
}
