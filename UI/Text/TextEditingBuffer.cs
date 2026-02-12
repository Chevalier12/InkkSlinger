using System;
using System.Collections.Generic;
using System.Text;

namespace InkkSlinger;

public sealed class TextEditingBuffer
{
    private string _originalText = string.Empty;
    private readonly StringBuilder _addBuffer = new();
    private readonly List<Piece> _pieces = [];
    private int _length;
    private int _lineBreakCount;
    private int _caretIndex;
    private int _selectionAnchor;
    private string? _textCache;
    private TextEditDelta _lastEditDelta = TextEditDelta.None;
    private int _textMaterializationCount;

    public string Text
    {
        get
        {
            if (_textCache != null)
            {
                return _textCache;
            }

            if (_length == 0)
            {
                _textCache = string.Empty;
                return _textCache;
            }

            _textMaterializationCount++;
            var builder = new StringBuilder(_length);
            foreach (var piece in _pieces)
            {
                if (piece.Length <= 0)
                {
                    continue;
                }

                var source = piece.Source == PieceSource.Original ? _originalText : null;
                if (source != null)
                {
                    builder.Append(source, piece.Start, piece.Length);
                    continue;
                }

                for (var i = 0; i < piece.Length; i++)
                {
                    builder.Append(_addBuffer[piece.Start + i]);
                }
            }

            _textCache = builder.ToString();
            return _textCache;
        }
    }

    public int CaretIndex => _caretIndex;

    public int Length => _length;

    public int LogicalLineCount => _length == 0 ? 0 : _lineBreakCount + 1;

    public TextSelection Selection => new(_selectionAnchor, _caretIndex);

    public bool HasSelection => _selectionAnchor != _caretIndex;

    public TextEditDelta LastEditDelta => _lastEditDelta;

    public int PieceCount => _pieces.Count;

    public string GetSelectedText()
    {
        if (!HasSelection)
        {
            return string.Empty;
        }

        var selection = Selection;
        return BuildRangeText(selection.Start, selection.Length);
    }

    public void SetText(string? text, bool preserveCaret = true)
    {
        var oldLength = _length;
        _originalText = text ?? string.Empty;
        _addBuffer.Clear();
        _pieces.Clear();
        if (_originalText.Length > 0)
        {
            _pieces.Add(new Piece(PieceSource.Original, 0, _originalText.Length));
        }

        _length = _originalText.Length;
        _lineBreakCount = CountLineBreaks(_originalText.AsSpan());
        InvalidateTextCache();
        _lastEditDelta = new TextEditDelta(0, oldLength, _length, _originalText, _originalText, IsValid: true);

        if (!preserveCaret)
        {
            _caretIndex = _length;
            _selectionAnchor = _caretIndex;
            return;
        }

        _caretIndex = ClampIndex(_caretIndex);
        _selectionAnchor = ClampIndex(_selectionAnchor);
    }

    public void SetCaret(int index, bool extendSelection)
    {
        var clampedIndex = ClampIndex(index);
        _caretIndex = clampedIndex;

        if (!extendSelection)
        {
            _selectionAnchor = clampedIndex;
        }
    }

    public void SelectAll()
    {
        _selectionAnchor = 0;
        _caretIndex = _length;
    }

    public bool MoveCaretLeft(bool extendSelection, bool byWord)
    {
        if (_caretIndex == 0 && (!HasSelection || extendSelection))
        {
            return false;
        }

        var next = byWord
            ? FindPreviousWordBoundary(_caretIndex)
            : _caretIndex - 1;

        if (!extendSelection && HasSelection)
        {
            next = Selection.Start;
        }

        SetCaret(next, extendSelection);
        return true;
    }

    public bool MoveCaretRight(bool extendSelection, bool byWord)
    {
        if (_caretIndex == _length && (!HasSelection || extendSelection))
        {
            return false;
        }

        var next = byWord
            ? FindNextWordBoundary(_caretIndex)
            : _caretIndex + 1;

        if (!extendSelection && HasSelection)
        {
            next = Selection.End;
        }

        SetCaret(next, extendSelection);
        return true;
    }

    public bool MoveCaretHome(bool extendSelection)
    {
        if (_caretIndex == 0 && (!HasSelection || extendSelection))
        {
            return false;
        }

        SetCaret(0, extendSelection);
        return true;
    }

    public bool MoveCaretEnd(bool extendSelection)
    {
        if (_caretIndex == _length && (!HasSelection || extendSelection))
        {
            return false;
        }

        SetCaret(_length, extendSelection);
        return true;
    }

    public bool InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var selection = Selection;
        var insertAt = _caretIndex;
        var replacedLength = 0;
        var removedText = string.Empty;
        if (!selection.IsEmpty)
        {
            replacedLength = selection.Length;
            removedText = BuildRangeText(selection.Start, selection.Length);
            DeleteRange(selection.Start, selection.Length);
            insertAt = selection.Start;
        }

        InsertRange(insertAt, text);
        _caretIndex = insertAt + text.Length;
        _selectionAnchor = _caretIndex;
        _lastEditDelta = new TextEditDelta(insertAt, replacedLength, text.Length, text, removedText, IsValid: true);
        return true;
    }

    public bool Backspace(bool byWord)
    {
        if (DeleteSelectionIfPresent())
        {
            return true;
        }

        if (_caretIndex == 0)
        {
            return false;
        }

        var start = byWord
            ? FindPreviousWordBoundary(_caretIndex)
            : _caretIndex - 1;

        var length = _caretIndex - start;
        var removedText = BuildRangeText(start, length);
        DeleteRange(start, length);
        _caretIndex = start;
        _selectionAnchor = _caretIndex;
        _lastEditDelta = new TextEditDelta(start, length, 0, string.Empty, removedText, IsValid: true);
        return true;
    }

    public bool Delete(bool byWord)
    {
        if (DeleteSelectionIfPresent())
        {
            return true;
        }

        if (_caretIndex >= _length)
        {
            return false;
        }

        var end = byWord
            ? FindNextWordBoundary(_caretIndex)
            : _caretIndex + 1;

        var removed = end - _caretIndex;
        var removedText = BuildRangeText(_caretIndex, removed);
        DeleteRange(_caretIndex, removed);
        _selectionAnchor = _caretIndex;
        _lastEditDelta = new TextEditDelta(_caretIndex, removed, 0, string.Empty, removedText, IsValid: true);
        return true;
    }

    public bool DeleteSelectionIfPresent()
    {
        if (!HasSelection)
        {
            return false;
        }

        var selection = Selection;
        var removedText = BuildRangeText(selection.Start, selection.Length);
        DeleteRange(selection.Start, selection.Length);
        _caretIndex = selection.Start;
        _selectionAnchor = _caretIndex;
        _lastEditDelta = new TextEditDelta(selection.Start, selection.Length, 0, string.Empty, removedText, IsValid: true);
        return true;
    }

    public TextEditDelta ConsumeLastEditDelta()
    {
        var delta = _lastEditDelta;
        _lastEditDelta = TextEditDelta.None;
        return delta;
    }

    public TextEditingBufferDiagnostics GetDiagnostics()
    {
        return new TextEditingBufferDiagnostics(
            _textMaterializationCount,
            _pieces.Count,
            _length,
            LogicalLineCount);
    }

    public void ResetDiagnostics()
    {
        _textMaterializationCount = 0;
    }

    private int ClampIndex(int index)
    {
        if (index < 0)
        {
            return 0;
        }

        if (index > _length)
        {
            return _length;
        }

        return index;
    }

    private int FindPreviousWordBoundary(int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        var i = index;
        while (i > 0 && char.IsWhiteSpace(GetCharAt(i - 1)))
        {
            i--;
        }

        while (i > 0 && IsWordCharacter(GetCharAt(i - 1)))
        {
            i--;
        }

        return i;
    }

    private int FindNextWordBoundary(int index)
    {
        if (index >= _length)
        {
            return _length;
        }

        var i = index;
        while (i < _length && char.IsWhiteSpace(GetCharAt(i)))
        {
            i++;
        }

        while (i < _length && IsWordCharacter(GetCharAt(i)))
        {
            i++;
        }

        return i;
    }

    private static bool IsWordCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private void InsertRange(int index, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        index = Math.Clamp(index, 0, _length);
        SplitAt(index);

        var insertPieceIndex = GetPieceIndexAtBoundary(index);
        var addStart = _addBuffer.Length;
        _addBuffer.Append(text);
        _pieces.Insert(insertPieceIndex, new Piece(PieceSource.Add, addStart, text.Length));
        CoalesceAround(insertPieceIndex);
        _length += text.Length;
        _lineBreakCount += CountLineBreaks(text.AsSpan());
        InvalidateTextCache();
    }

    private void DeleteRange(int start, int length)
    {
        if (length <= 0 || _length == 0)
        {
            return;
        }

        start = Math.Clamp(start, 0, _length);
        var end = Math.Clamp(start + length, 0, _length);
        if (end <= start)
        {
            return;
        }

        var removedLineBreaks = CountLineBreaksInRange(start, end - start);

        SplitAt(end);
        SplitAt(start);

        var startPieceIndex = GetPieceIndexAtBoundary(start);
        var endPieceIndex = GetPieceIndexAtBoundary(end);
        if (endPieceIndex > startPieceIndex)
        {
            _pieces.RemoveRange(startPieceIndex, endPieceIndex - startPieceIndex);
        }

        CoalesceAround(startPieceIndex - 1);

        _length -= (end - start);
        _lineBreakCount = Math.Max(0, _lineBreakCount - removedLineBreaks);
        InvalidateTextCache();
    }

    private void CoalesceAround(int index)
    {
        if (_pieces.Count < 2)
        {
            return;
        }

        var i = Math.Clamp(index, 0, _pieces.Count - 2);
        while (i < _pieces.Count - 1)
        {
            var left = _pieces[i];
            var right = _pieces[i + 1];
            var canMerge = left.Source == right.Source &&
                           left.Length > 0 &&
                           right.Length > 0 &&
                           (left.Start + left.Length) == right.Start;
            if (!canMerge)
            {
                i++;
                continue;
            }

            _pieces[i] = new Piece(left.Source, left.Start, left.Length + right.Length);
            _pieces.RemoveAt(i + 1);
            if (i > 0)
            {
                i--;
            }
        }
    }

    private void SplitAt(int index)
    {
        if (index <= 0 || index >= _length)
        {
            return;
        }

        var cumulative = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            var next = cumulative + piece.Length;
            if (index == cumulative || index == next)
            {
                return;
            }

            if (index > cumulative && index < next)
            {
                var leftLength = index - cumulative;
                var rightLength = piece.Length - leftLength;
                var leftPiece = new Piece(piece.Source, piece.Start, leftLength);
                var rightPiece = new Piece(piece.Source, piece.Start + leftLength, rightLength);
                _pieces[i] = leftPiece;
                _pieces.Insert(i + 1, rightPiece);
                return;
            }

            cumulative = next;
        }
    }

    private int GetPieceIndexAtBoundary(int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        if (index >= _length)
        {
            return _pieces.Count;
        }

        var cumulative = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            if (cumulative == index)
            {
                return i;
            }

            cumulative += _pieces[i].Length;
            if (cumulative == index)
            {
                return i + 1;
            }
        }

        return _pieces.Count;
    }

    private char GetCharAt(int index)
    {
        if (index < 0 || index >= _length)
        {
            return '\0';
        }

        var cumulative = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            var next = cumulative + piece.Length;
            if (index < next)
            {
                var pieceOffset = index - cumulative;
                return piece.Source == PieceSource.Original
                    ? _originalText[piece.Start + pieceOffset]
                    : _addBuffer[piece.Start + pieceOffset];
            }

            cumulative = next;
        }

        return '\0';
    }

    private string BuildRangeText(int start, int length)
    {
        if (length <= 0 || _length == 0)
        {
            return string.Empty;
        }

        start = Math.Clamp(start, 0, _length);
        var end = Math.Clamp(start + length, 0, _length);
        if (end <= start)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(end - start);
        var cumulative = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            var pieceStart = cumulative;
            var pieceEnd = cumulative + piece.Length;
            if (pieceEnd <= start)
            {
                cumulative = pieceEnd;
                continue;
            }

            if (pieceStart >= end)
            {
                break;
            }

            var localStart = Math.Max(start, pieceStart) - pieceStart;
            var localEnd = Math.Min(end, pieceEnd) - pieceStart;
            var localLength = localEnd - localStart;
            if (localLength > 0)
            {
                if (piece.Source == PieceSource.Original)
                {
                    builder.Append(_originalText, piece.Start + localStart, localLength);
                }
                else
                {
                    for (var j = 0; j < localLength; j++)
                    {
                        builder.Append(_addBuffer[piece.Start + localStart + j]);
                    }
                }
            }

            cumulative = pieceEnd;
        }

        return builder.ToString();
    }

    private int CountLineBreaksInRange(int start, int length)
    {
        if (length <= 0 || _length == 0)
        {
            return 0;
        }

        start = Math.Clamp(start, 0, _length);
        var end = Math.Clamp(start + length, 0, _length);
        if (end <= start)
        {
            return 0;
        }

        var count = 0;
        var cumulative = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            var pieceStart = cumulative;
            var pieceEnd = cumulative + piece.Length;
            if (pieceEnd <= start)
            {
                cumulative = pieceEnd;
                continue;
            }

            if (pieceStart >= end)
            {
                break;
            }

            var localStart = Math.Max(start, pieceStart) - pieceStart;
            var localEnd = Math.Min(end, pieceEnd) - pieceStart;
            var localLength = localEnd - localStart;
            if (localLength > 0)
            {
                if (piece.Source == PieceSource.Original)
                {
                    count += CountLineBreaks(_originalText.AsSpan(piece.Start + localStart, localLength));
                }
                else
                {
                    for (var j = 0; j < localLength; j++)
                    {
                        if (_addBuffer[piece.Start + localStart + j] == '\n')
                        {
                            count++;
                        }
                    }
                }
            }

            cumulative = pieceEnd;
        }

        return count;
    }

    private static int CountLineBreaks(ReadOnlySpan<char> text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private void InvalidateTextCache()
    {
        _textCache = null;
    }

    private enum PieceSource
    {
        Original,
        Add
    }

    private readonly record struct Piece(PieceSource Source, int Start, int Length);
}

public readonly record struct TextEditDelta(
    int Start,
    int OldLength,
    int NewLength,
    string InsertedText,
    string RemovedText,
    bool IsValid = true)
{
    public static TextEditDelta None => new(0, 0, 0, string.Empty, string.Empty, IsValid: false);

    public int DeltaLength => NewLength - OldLength;
}

public readonly record struct TextEditingBufferDiagnostics(
    int TextMaterializationCount,
    int PieceCount,
    int Length,
    int LogicalLineCount);
