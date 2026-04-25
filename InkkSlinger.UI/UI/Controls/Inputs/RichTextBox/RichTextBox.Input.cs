using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class RichTextBox
{
    public bool HandleTextInputFromInput(char character)
    {
        if (character == ' ' && ShouldSuppressDuplicateSpaceTextInput())
        {
            RecordOperation("TextInputSuppressed", "space-duplicate");
            return true;
        }

        return HandleTextCompositionFromInput(character.ToString());
    }

    public bool HandleTextCompositionFromInput(string? text)
    {
        if (!IsEnabled || !IsFocused || IsReadOnly || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var normalized = FilterCompositionText(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        RecordOperation("TextComposition", $"text=\"{SanitizeForLog(normalized)}\"");

        if (_typingBoldActive || _typingItalicActive || _typingUnderlineActive)
        {
            if (!TryInsertTypingFormattedTextWithinParagraph(normalized, "InsertTextStyled", GroupingPolicy.TypingBurst))
            {
                InsertTypingFormattedText(normalized, "InsertTextStyled");
            }
        }
        else if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(normalized, "InsertText", GroupingPolicy.TypingBurst))
        {
            // Handled by style-preserving paragraph edit path for rich documents.
        }
        else if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(normalized, "InsertText", GroupingPolicy.TypingBurst))
        {
            // Handled by structure-preserving plain paragraph edit path.
        }
        else if (DocumentContainsRichInlineFormatting(Document))
        {
            InsertTypingFormattedText(normalized, "InsertText");
        }
        else
        {
            ReplaceSelection(normalized, "InsertText", GroupingPolicy.TypingBurst);
        }

        return true;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        RecordOperation("KeyDown", $"key={key} mods={modifiers}");
        var inputDescriptor = $"Key:{key}+{modifiers}";
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        _activeKeyModifiers = modifiers;
        try
        {
            var ctrl = (modifiers & ModifierKeys.Control) != 0;
            if (key == Keys.Space && modifiers == ModifierKeys.None)
            {
                var handled = TryInsertSpaceAtListTableBoundary() || HandleTextCompositionFromInput(" ");
                if (handled)
                {
                    ArmDuplicateSpaceTextInputSuppression();
                }

                return handled;
            }

            if (key == Keys.Enter && (ctrl || IsReadOnly) && TryActivateHyperlinkAtSelection())
            {
                return true;
            }

            if (ctrl && key == Keys.Z)
            {
                var handled = ExecuteUndo();
                return true;
            }

            if (ctrl && key == Keys.Y)
            {
                var handled = ExecuteRedo();
                return true;
            }

            if (TryExecuteEditingCommandFromKey(key, modifiers))
            {
                return true;
            }

            return false;
        }
        finally
        {
            _activeKeyModifiers = ModifierKeys.None;
        }
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        UpdatePointerClickCount(pointerPosition);
        var index = GetTextIndexFromPoint(pointerPosition);
        _lastSelectionHitTestOffset = index;
        _pendingPointerHyperlink = ResolveHyperlinkAtOffset(index);
        _pointerSelectionMoved = false;
        if (_pointerClickCount >= 3)
        {
            SelectParagraphAt(index);
        }
        else if (_pointerClickCount == 2)
        {
            SelectWordAt(index);
        }
        else
        {
            SetCaret(index, extendSelection);
        }

        _isSelectingWithPointer = true;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("PointerDownSelection");
        return true;
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled || !IsFocused || !_isSelectingWithPointer)
        {
            return false;
        }

        var adjustedPoint = pointerPosition;
        AutoScrollForPointer(ref adjustedPoint);
        var index = GetTextIndexFromPoint(adjustedPoint);
        _lastSelectionHitTestOffset = index;
        _pointerSelectionMoved = true;
        UpdateSelectionState(_selectionAnchor, index, ensureCaretVisible: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("PointerDragSelection");
        return true;
    }

    public bool HandlePointerUpFromInput()
    {
        if (!_isSelectingWithPointer)
        {
            return false;
        }

        _isSelectingWithPointer = false;
        if (IsReadOnly &&
            !_pointerSelectionMoved &&
            SelectionLength == 0 &&
            _pendingPointerHyperlink != null)
        {
            TryActivateHyperlink(_pendingPointerHyperlink);
        }

        _pendingPointerHyperlink = null;
        return true;
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        if (!IsEnabled || delta == 0)
        {
            return false;
        }

        if (_contentHost != null)
        {
            return _contentHost.HandleMouseWheelFromInput(delta);
        }

        return ScrollBy(0f, -MathF.Sign(delta) * (UiTextRenderer.GetLineHeight(this, FontSize) * 3f), "MouseWheelScroll");
    }

    public void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
        if (!isMouseOver)
        {
            SetHoveredHyperlink(null);
        }
    }

    public void UpdateHoveredHyperlinkFromPointer(Vector2 pointerPosition)
    {
        if (!IsEnabled)
        {
            SetHoveredHyperlink(null);
            return;
        }

        var textRect = GetTextRect();
        if (pointerPosition.X < textRect.X ||
            pointerPosition.Y < textRect.Y ||
            pointerPosition.X > textRect.X + textRect.Width ||
            pointerPosition.Y > textRect.Y + textRect.Height)
        {
            SetHoveredHyperlink(null);
            return;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        SetHoveredHyperlink(ResolveHyperlinkAtOffset(index));
    }

    public void SetFocusedFromInput(bool isFocused)
    {
        if (IsFocused == isFocused)
        {
            return;
        }

        IsFocused = isFocused;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = isFocused;
        _isSelectingWithPointer = false;
        if (isFocused)
        {
            EnsureCaretVisible();
        }

        InvalidateVisual();
    }

    private void ArmDuplicateSpaceTextInputSuppression()
    {
        _suppressSpaceTextInputUntilTicks = Stopwatch.GetTimestamp() + (Stopwatch.Frequency / 4);
    }

    private bool ShouldSuppressDuplicateSpaceTextInput()
    {
        var until = _suppressSpaceTextInputUntilTicks;
        if (until <= 0)
        {
            return false;
        }

        var now = Stopwatch.GetTimestamp();
        if (now <= until)
        {
            _suppressSpaceTextInputUntilTicks = 0;
            return true;
        }

        _suppressSpaceTextInputUntilTicks = 0;
        return false;
    }
}