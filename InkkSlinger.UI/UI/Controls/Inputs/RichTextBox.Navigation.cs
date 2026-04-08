using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void ExecuteSelectAllCore()
    {
        UpdateSelectionState(0, GetText().Length, ensureCaretVisible: true);
        InvalidateVisual();
    }

    private void ExecuteMoveLeftByCharacter()
    {
        MoveCaret(-1, ExtendSelectionModifierActive());
    }

    private void ExecuteMoveRightByCharacter()
    {
        MoveCaret(1, ExtendSelectionModifierActive());
    }

    private void ExecuteMoveLeftByWord()
    {
        MoveCaretByWord(moveLeft: true, extendSelection: ExtendSelectionModifierActive());
    }

    private void ExecuteMoveRightByWord()
    {
        MoveCaretByWord(moveLeft: false, extendSelection: ExtendSelectionModifierActive());
    }

    private void ExecuteSelectLeftByCharacter()
    {
        MoveCaret(-1, extendSelection: true);
    }

    private void ExecuteSelectRightByCharacter()
    {
        MoveCaret(1, extendSelection: true);
    }

    private void ExecuteSelectLeftByWord()
    {
        MoveCaretByWord(moveLeft: true, extendSelection: true);
    }

    private void ExecuteSelectRightByWord()
    {
        MoveCaretByWord(moveLeft: false, extendSelection: true);
    }

    private void ExecuteDeletePreviousWord()
    {
        if (SelectionLength > 0)
        {
            ExecuteDeleteRange(SelectionStart, SelectionStart + SelectionLength, "DeletePreviousWord");
            return;
        }

        if (_caretIndex <= 0)
        {
            return;
        }

        var text = GetText();
        var start = GetDeletePreviousWordBoundary(text, _caretIndex);
        ExecuteDeleteRange(start, _caretIndex, "DeletePreviousWord");
    }

    private void ExecuteDeleteNextWord()
    {
        if (SelectionLength > 0)
        {
            ExecuteDeleteRange(SelectionStart, SelectionStart + SelectionLength, "DeleteNextWord");
            return;
        }

        var text = GetText();
        if (_caretIndex >= text.Length)
        {
            return;
        }

        var end = GetDeleteNextWordBoundary(text, _caretIndex);
        ExecuteDeleteRange(_caretIndex, end, "DeleteNextWord");
    }

    private void ExecuteMoveUpByLine()
    {
        MoveCaretByLine(moveUp: true, extendSelection: false);
    }

    private void ExecuteMoveDownByLine()
    {
        MoveCaretByLine(moveUp: false, extendSelection: false);
    }

    private void ExecuteMoveUpByPage()
    {
        MoveCaretByPage(moveUp: true, extendSelection: false);
    }

    private void ExecuteMoveDownByPage()
    {
        MoveCaretByPage(moveUp: false, extendSelection: false);
    }

    private void ExecuteMoveUpByParagraph()
    {
        MoveCaretByParagraph(moveUp: true, extendSelection: false);
    }

    private void ExecuteMoveDownByParagraph()
    {
        MoveCaretByParagraph(moveUp: false, extendSelection: false);
    }

    private void ExecuteMoveToLineStart()
    {
        MoveCaretToLineBoundary(moveToLineStart: true, extendSelection: false);
    }

    private void ExecuteMoveToLineEnd()
    {
        MoveCaretToLineBoundary(moveToLineStart: false, extendSelection: false);
    }

    private void ExecuteMoveToParagraphStart()
    {
        MoveCaretToParagraphBoundary(moveToParagraphStart: true, extendSelection: false);
    }

    private void ExecuteMoveToParagraphEnd()
    {
        MoveCaretToParagraphBoundary(moveToParagraphStart: false, extendSelection: false);
    }

    private void ExecuteMoveToDocumentStart()
    {
        SetCaret(0, extendSelection: false);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ExecuteMoveToDocumentEnd()
    {
        SetCaret(GetText().Length, extendSelection: false);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ExecuteSelectUpByLine()
    {
        MoveCaretByLine(moveUp: true, extendSelection: true);
    }

    private void ExecuteSelectDownByLine()
    {
        MoveCaretByLine(moveUp: false, extendSelection: true);
    }

    private void ExecuteSelectUpByPage()
    {
        MoveCaretByPage(moveUp: true, extendSelection: true);
    }

    private void ExecuteSelectDownByPage()
    {
        MoveCaretByPage(moveUp: false, extendSelection: true);
    }

    private void ExecuteSelectUpByParagraph()
    {
        MoveCaretByParagraph(moveUp: true, extendSelection: true);
    }

    private void ExecuteSelectDownByParagraph()
    {
        MoveCaretByParagraph(moveUp: false, extendSelection: true);
    }

    private void ExecuteSelectToLineStart()
    {
        MoveCaretToLineBoundary(moveToLineStart: true, extendSelection: true);
    }

    private void ExecuteSelectToLineEnd()
    {
        MoveCaretToLineBoundary(moveToLineStart: false, extendSelection: true);
    }

    private void ExecuteSelectToParagraphStart()
    {
        MoveCaretToParagraphBoundary(moveToParagraphStart: true, extendSelection: true);
    }

    private void ExecuteSelectToParagraphEnd()
    {
        MoveCaretToParagraphBoundary(moveToParagraphStart: false, extendSelection: true);
    }

    private void ExecuteSelectToDocumentStart()
    {
        SetCaret(0, extendSelection: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ExecuteSelectToDocumentEnd()
    {
        SetCaret(GetText().Length, extendSelection: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void SetCaret(int index, bool extendSelection)
    {
        var nextCaret = Math.Clamp(index, 0, GetText().Length);
        var nextAnchor = extendSelection ? _selectionAnchor : nextCaret;
        UpdateSelectionState(nextAnchor, nextCaret, ensureCaretVisible: true);
    }

    private void MoveCaret(int delta, bool extendSelection)
    {
        SetCaret(Math.Clamp(_caretIndex + delta, 0, GetText().Length), extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ClampSelectionToTextLength()
    {
        var length = GetText().Length;
        UpdateSelectionState(
            Math.Clamp(_selectionAnchor, 0, length),
            Math.Clamp(_caretIndex, 0, length),
            ensureCaretVisible: true);
    }

    private void MoveCaretByWord(bool moveLeft, bool extendSelection)
    {
        var text = GetText();
        var target = GetWordBoundary(text, _caretIndex, moveLeft);
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ExecuteDeleteRange(int start, int end, string commandType)
    {
        var textLength = GetText().Length;
        var clampedStart = Math.Clamp(start, 0, textLength);
        var clampedEnd = Math.Clamp(end, clampedStart, textLength);
        if (clampedEnd <= clampedStart)
        {
            return;
        }

        RecordOperation("Command", commandType);
        _selectionAnchor = clampedStart;
        _caretIndex = clampedEnd;
        if (TryDeleteSelectionPreservingStructure(commandType, GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", $"{commandType}->DeleteSelectionStructured");
            return;
        }

        RecordOperation("Branch", $"{commandType}->DeleteSelection");
        ReplaceSelection(string.Empty, commandType, GroupingPolicy.DeletionBurst);
    }

    private void MoveCaretToLineBoundary(bool moveToLineStart, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        var line = ResolveLineForOffset(layout, _caretIndex);
        var target = moveToLineStart ? line.StartOffset : line.StartOffset + line.Length;
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void MoveCaretByLine(bool moveUp, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        if (layout.Lines.Count == 0)
        {
            return;
        }

        var currentLine = ResolveLineForOffset(layout, _caretIndex);
        var targetLineIndex = moveUp
            ? Math.Max(0, currentLine.Index - 1)
            : Math.Min(layout.Lines.Count - 1, currentLine.Index + 1);
        if (targetLineIndex == currentLine.Index)
        {
            return;
        }

        var currentColumn = Math.Clamp(_caretIndex - currentLine.StartOffset, 0, currentLine.PrefixWidths.Length - 1);
        var desiredX = currentLine.TextStartX + currentLine.PrefixWidths[currentColumn];
        var targetLine = layout.Lines[targetLineIndex];
        var targetColumn = ResolveClosestColumnForX(targetLine, desiredX);
        var targetOffset = Math.Clamp(targetLine.StartOffset + targetColumn, 0, GetText().Length);
        SetCaret(targetOffset, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private void MoveCaretByPage(bool moveUp, bool extendSelection)
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        if (layout.Lines.Count == 0 || !layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return;
        }

        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(this, FontSize));
        var pageDelta = Math.Max(lineHeight, textRect.Height - lineHeight);
        var targetY = moveUp
            ? Math.Max(0f, caret.Y - pageDelta)
            : Math.Min(Math.Max(0f, layout.ContentHeight), caret.Y + pageDelta);
        var target = layout.HitTestOffset(new Vector2(caret.X, targetY));
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private void MoveCaretByParagraph(bool moveUp, bool extendSelection)
    {
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return;
        }

        var currentIndex = ResolveParagraphIndexForOffset(entries, _caretIndex);
        var targetIndex = moveUp
            ? Math.Max(0, currentIndex - 1)
            : Math.Min(entries.Count - 1, currentIndex + 1);
        if (targetIndex == currentIndex)
        {
            return;
        }

        SetCaret(entries[targetIndex].StartOffset, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private void MoveCaretToParagraphBoundary(bool moveToParagraphStart, bool extendSelection)
    {
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            SetCaret(0, extendSelection);
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateVisual();
            return;
        }

        var entry = entries[ResolveParagraphIndexForOffset(entries, _caretIndex)];
        var target = moveToParagraphStart ? entry.StartOffset : entry.EndOffset;
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private static int ResolveParagraphIndexForOffset(IReadOnlyList<ParagraphSelectionEntry> entries, int offset)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (offset >= entries[i].StartOffset && offset <= entries[i].EndOffset)
            {
                return i;
            }
        }

        return entries.Count - 1;
    }

    private static int ResolveClosestColumnForX(DocumentLayoutLine line, float desiredX)
    {
        if (line.PrefixWidths.Length == 0)
        {
            return 0;
        }

        var bestColumn = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < line.PrefixWidths.Length; i++)
        {
            var x = line.TextStartX + line.PrefixWidths[i];
            var distance = MathF.Abs(desiredX - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestColumn = i;
            }
        }

        return bestColumn;
    }

    private static int GetWordBoundary(string text, int index, bool moveLeft)
    {
        var length = text.Length;
        var clamped = Math.Clamp(index, 0, length);
        if (moveLeft)
        {
            if (clamped <= 0)
            {
                return 0;
            }

            var i = clamped;
            if (char.IsWhiteSpace(text[i - 1]))
            {
                while (i > 0 && char.IsWhiteSpace(text[i - 1]))
                {
                    i--;
                }
            }
            else if (IsWordChar(text[i - 1]))
            {
                while (i > 0 && IsWordChar(text[i - 1]))
                {
                    i--;
                }
            }
            else
            {
                while (i > 0 && IsPunctuationChar(text[i - 1]))
                {
                    i--;
                }

                while (i > 0 && IsWordChar(text[i - 1]))
                {
                    i--;
                }
            }

            return i;
        }

        if (clamped >= length)
        {
            return length;
        }

        var j = clamped;
        if (char.IsWhiteSpace(text[j]))
        {
            while (j < length && char.IsWhiteSpace(text[j]))
            {
                j++;
            }
        }
        else if (IsWordChar(text[j]))
        {
            while (j < length && IsWordChar(text[j]))
            {
                j++;
            }

            while (j < length && IsPunctuationChar(text[j]))
            {
                j++;
            }
        }
        else
        {
            while (j < length && IsPunctuationChar(text[j]))
            {
                j++;
            }
        }

        return j;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private static bool IsPunctuationChar(char c)
    {
        return !char.IsWhiteSpace(c) && !IsWordChar(c);
    }

    private static int GetDeletePreviousWordBoundary(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        while (i > 0 && IsWordChar(text[i - 1]))
        {
            i--;
        }

        return i;
    }

    private static int GetDeleteNextWordBoundary(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        while (i < text.Length && IsWordChar(text[i]))
        {
            i++;
        }

        return i;
    }

    private DocumentLayoutLine ResolveLineForOffset(DocumentLayoutResult layout, int offset)
    {
        if (layout.Lines.Count == 0)
        {
            return new DocumentLayoutLine
            {
                Index = 0,
                StartOffset = 0,
                Length = 0,
                Text = string.Empty,
                TextStartX = 0f,
                Bounds = new LayoutRect(0f, 0f, 0f, UiTextRenderer.GetLineHeight(this, FontSize)),
                Runs = Array.Empty<DocumentLayoutRun>(),
                PrefixWidths = [0f]
            };
        }

        var clamped = Math.Clamp(offset, 0, layout.TextLength);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var end = line.StartOffset + line.Length;
            if (clamped <= end)
            {
                return line;
            }
        }

        return layout.Lines[layout.Lines.Count - 1];
    }

    private void EnsureCaretVisible()
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return;
        }

        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(this, FontSize));
        var changed = false;
        var visibleX = caret.X - _horizontalOffset;
        if (visibleX < 0f)
        {
            _horizontalOffset = caret.X;
            changed = true;
        }
        else if (visibleX > Math.Max(0f, textRect.Width - 2f))
        {
            _horizontalOffset = Math.Max(0f, caret.X - textRect.Width + 2f);
            changed = true;
        }

        var visibleY = caret.Y - _verticalOffset;
        if (visibleY < 0f)
        {
            _verticalOffset = caret.Y;
            changed = true;
        }
        else if (visibleY + lineHeight > textRect.Height)
        {
            _verticalOffset = Math.Max(0f, caret.Y + lineHeight - textRect.Height);
            changed = true;
        }

        ClampScrollOffsets(layout, textRect);
        if (changed)
        {
            InvalidateVisualWithReason("CaretBlink");
        }
    }

    private void ClampScrollOffsets(DocumentLayoutResult layout, LayoutRect textRect)
    {
        var maxX = Math.Max(0f, layout.ContentWidth - textRect.Width);
        var maxY = Math.Max(0f, layout.ContentHeight - textRect.Height);
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0f, maxX);
        _verticalOffset = Math.Clamp(_verticalOffset, 0f, maxY);
    }

    private void SelectWordAt(int index)
    {
        var text = GetText();
        if (text.Length == 0)
        {
            UpdateSelectionState(0, 0, ensureCaretVisible: false);
            return;
        }

        var clamped = Math.Clamp(index, 0, Math.Max(0, text.Length - 1));
        if (char.IsWhiteSpace(text[clamped]))
        {
            if (text[clamped] == '\n')
            {
                UpdateSelectionState(clamped, clamped, ensureCaretVisible: false);
                return;
            }
            
            UpdateSelectionState(clamped, Math.Min(text.Length, clamped + 1), ensureCaretVisible: false);
            return;
        }

        var start = clamped;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var end = clamped;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        UpdateSelectionState(start, end, ensureCaretVisible: true);
    }

    private void SelectParagraphAt(int index)
    {
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            UpdateSelectionState(0, 0, ensureCaretVisible: false);
            return;
        }

        var maxOffset = Math.Max(0, entries[entries.Count - 1].EndOffset);
        var clamped = Math.Clamp(index, 0, maxOffset);
        for (var i = 0; i < entries.Count; i++)
        {
            if (clamped < entries[i].StartOffset || clamped > entries[i].EndOffset)
            {
                continue;
            }

            UpdateSelectionState(entries[i].StartOffset, entries[i].EndOffset, ensureCaretVisible: true);
            return;
        }

        UpdateSelectionState(entries[entries.Count - 1].StartOffset, entries[entries.Count - 1].EndOffset, ensureCaretVisible: true);
    }

    private void UpdatePointerClickCount(Vector2 pointerPosition)
    {
        var now = DateTime.UtcNow;
        var index = GetTextIndexFromPoint(pointerPosition);
        var withinWindow = (now - _lastPointerDownUtc).TotalMilliseconds <= MultiClickWindowMs;
        if (withinWindow && Math.Abs(index - _lastPointerDownIndex) <= 1)
        {
            _pointerClickCount = Math.Min(3, _pointerClickCount + 1);
        }
        else
        {
            _pointerClickCount = 1;
        }

        _lastPointerDownUtc = now;
        _lastPointerDownIndex = index;
    }

    private void AutoScrollForPointer(ref Vector2 pointer)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        var changed = false;
        if (pointer.Y < textRect.Y)
        {
            _verticalOffset = Math.Max(0f, _verticalOffset - PointerAutoScrollStep);
            changed = true;
        }
        else if (pointer.Y > textRect.Y + textRect.Height)
        {
            _verticalOffset += PointerAutoScrollStep;
            changed = true;
        }

        if (pointer.X < textRect.X)
        {
            _horizontalOffset = Math.Max(0f, _horizontalOffset - PointerAutoScrollStep);
            changed = true;
        }
        else if (pointer.X > textRect.X + textRect.Width)
        {
            _horizontalOffset += PointerAutoScrollStep;
            changed = true;
        }

        ClampScrollOffsets(layout, textRect);
        pointer = new Vector2(
            Math.Clamp(pointer.X, textRect.X, textRect.X + textRect.Width),
            Math.Clamp(pointer.Y, textRect.Y, textRect.Y + textRect.Height));
        if (changed)
        {
            InvalidateVisual();
        }
    }
}
