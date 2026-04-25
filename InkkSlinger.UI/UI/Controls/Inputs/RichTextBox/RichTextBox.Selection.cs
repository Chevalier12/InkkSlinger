using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void UpdateSelectionState(int selectionAnchor, int caretIndex, bool ensureCaretVisible)
    {
        var previousStart = SelectionStart;
        var previousLength = SelectionLength;
        _selectionAnchor = selectionAnchor;
        _caretIndex = caretIndex;
        if (ensureCaretVisible)
        {
            EnsureCaretVisible();
        }

        RaiseSelectionChangedEventIfNeeded(previousStart, previousLength);
    }

    public int GetNextSpellingErrorCharacterIndex(int charIndex, LogicalDirection direction)
    {
        ValidateSpellCheckCharacterIndex(charIndex);
        ValidateLogicalDirection(direction, nameof(direction));

        return IsSpellCheckEnabled ? -1 : -1;
    }

    public TextPointer? GetNextSpellingErrorPosition(TextPointer position, LogicalDirection direction)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        ValidateLogicalDirection(direction, nameof(direction));

        return null;
    }

    public SpellingError? GetSpellingError(TextPointer position)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        return null;
    }

    public TextRange? GetSpellingErrorRange(TextPointer position)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        return null;
    }

    public void SelectAll()
    {
        ExecuteSelectAllCore();
    }

    public void Select(int start, int length)
    {
        var textLength = GetText().Length;
        if (start < 0 || start > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0 || start + length > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        UpdateSelectionState(start, start + length, ensureCaretVisible: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("Select");
    }

    public void Select(TextPointer anchorPosition, TextPointer movingPosition)
    {
        var anchorOffset = ResolveDocumentOffset(anchorPosition, nameof(anchorPosition));
        var movingOffset = ResolveDocumentOffset(movingPosition, nameof(movingPosition));
        UpdateSelectionState(anchorOffset, movingOffset, ensureCaretVisible: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("Select");
    }

    public TextPointer? GetPositionFromPoint(Vector2 point, bool snapToText)
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return null;
        }

        if (!snapToText &&
            (point.X < textRect.X ||
             point.Y < textRect.Y ||
             point.X > textRect.X + textRect.Width ||
             point.Y > textRect.Y + textRect.Height))
        {
            return null;
        }

        var offset = GetTextIndexFromPoint(point);
        return CreateTextPointer(offset);
    }

    public bool TryGetCaretBounds(out LayoutRect bounds)
    {
        bounds = default;

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        if (!TryGetCaretRenderRect(textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset(), out var caretRect))
        {
            return false;
        }

        if (TryProjectRectToRootSpace(caretRect, out var rootSpaceBounds))
        {
            caretRect = rootSpaceBounds;
        }

        bounds = NormalizeRect(caretRect);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    public void LineUp()
    {
        ScrollBy(0f, -UiTextRenderer.GetLineHeight(this, FontSize), "LineUp");
    }

    public void LineDown()
    {
        ScrollBy(0f, UiTextRenderer.GetLineHeight(this, FontSize), "LineDown");
    }

    public void LineLeft()
    {
        ScrollBy(-16f, 0f, "LineLeft");
    }

    public void LineRight()
    {
        ScrollBy(16f, 0f, "LineRight");
    }

    public void PageUp()
    {
        ScrollBy(0f, -Math.Max(0f, ViewportHeight), "PageUp");
    }

    public void PageDown()
    {
        ScrollBy(0f, Math.Max(0f, ViewportHeight), "PageDown");
    }

    public void PageLeft()
    {
        ScrollBy(-Math.Max(0f, ViewportWidth), 0f, "PageLeft");
    }

    public void PageRight()
    {
        ScrollBy(Math.Max(0f, ViewportWidth), 0f, "PageRight");
    }

    public void ScrollToHome()
    {
        SetScrollOffsets(0f, 0f, "ScrollToHome");
    }

    public void ScrollToEnd()
    {
        var metrics = GetScrollMetrics();
        SetScrollOffsets(metrics.ScrollableWidth, metrics.ScrollableHeight, "ScrollToEnd");
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        SetScrollOffsets(offset, GetEffectiveVerticalOffset(), "ScrollToHorizontalOffset");
    }

    public void ScrollToVerticalOffset(float offset)
    {
        SetScrollOffsets(GetEffectiveHorizontalOffset(), offset, "ScrollToVerticalOffset");
    }

    public void PreserveCurrentScrollOffsetsOnNextLayout()
    {
        _horizontalOffset = GetEffectiveHorizontalOffset();
        _verticalOffset = GetEffectiveVerticalOffset();
        _hasPendingContentHostScrollOffsets = _contentHost != null;
    }
}