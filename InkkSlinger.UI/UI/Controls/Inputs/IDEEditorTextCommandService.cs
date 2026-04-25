using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public readonly record struct IDEEditorTextEditResult(string Text, int SelectionStart, int SelectionLength)
{
    public static IDEEditorTextEditResult Unchanged(string text, int selectionStart, int selectionLength)
    {
        return new IDEEditorTextEditResult(text ?? string.Empty, Math.Max(0, selectionStart), Math.Max(0, selectionLength));
    }
}

public static class IDEEditorTextCommandService
{
    public const string DefaultIndent = "  ";

    public static IDEEditorTextEditResult ReplaceSelection(
        string text,
        int selectionStart,
        int selectionLength,
        string replacement)
    {
        var normalized = Normalize(text);
        var range = NormalizeSelection(normalized, selectionStart, selectionLength);
        var insert = replacement ?? string.Empty;
        var updated = normalized.Remove(range.Start, range.Length).Insert(range.Start, insert);
        return new IDEEditorTextEditResult(updated, range.Start + insert.Length, 0);
    }

    public static IDEEditorTextEditResult WrapSelection(
        string text,
        int selectionStart,
        int selectionLength,
        string open,
        string close)
    {
        var normalized = Normalize(text);
        var range = NormalizeSelection(normalized, selectionStart, selectionLength);
        if (range.Length == 0)
        {
            return new IDEEditorTextEditResult(
                normalized.Insert(range.Start, open + close),
                range.Start + open.Length,
                0);
        }

        var selected = normalized.Substring(range.Start, range.Length);
        var replacement = open + selected + close;
        return new IDEEditorTextEditResult(
            normalized.Remove(range.Start, range.Length).Insert(range.Start, replacement),
            range.Start + open.Length,
            range.Length);
    }

    public static IDEEditorTextEditResult IndentSelectedLines(
        string text,
        int selectionStart,
        int selectionLength,
        string indent = DefaultIndent)
    {
        var normalized = Normalize(text);
        var lineRange = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        var lines = GetLines(normalized);
        var deltaBeforeSelection = 0;
        var deltaInSelection = 0;

        for (var lineIndex = lineRange.StartLine; lineIndex <= lineRange.EndLine; lineIndex++)
        {
            lines[lineIndex] = indent + lines[lineIndex];
            if (GetLineStartOffset(lines, lineIndex) <= selectionStart)
            {
                deltaBeforeSelection += indent.Length;
            }

            deltaInSelection += indent.Length;
        }

        var updated = JoinLines(lines, normalized.EndsWith('\n'));
        var nextSelectionStart = selectionLength == 0
            ? selectionStart + deltaBeforeSelection
            : selectionStart + (lineRange.StartOffset < selectionStart ? indent.Length : 0);
        var nextSelectionLength = selectionLength == 0 ? 0 : selectionLength + deltaInSelection;
        return new IDEEditorTextEditResult(updated, nextSelectionStart, nextSelectionLength);
    }

    public static IDEEditorTextEditResult OutdentSelectedLines(
        string text,
        int selectionStart,
        int selectionLength,
        string indent = DefaultIndent)
    {
        var normalized = Normalize(text);
        var lineRange = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        var lines = GetLines(normalized);
        var removedBeforeSelection = 0;
        var removedInSelection = 0;

        for (var lineIndex = lineRange.StartLine; lineIndex <= lineRange.EndLine; lineIndex++)
        {
            var line = lines[lineIndex];
            var removed = RemoveLineIndent(ref line, indent);
            lines[lineIndex] = line;
            if (removed == 0)
            {
                continue;
            }

            var lineStart = GetLineStartOffset(lines, lineIndex) + removedInSelection;
            if (lineStart < selectionStart)
            {
                removedBeforeSelection += Math.Min(removed, Math.Max(0, selectionStart - lineStart));
            }

            removedInSelection += removed;
        }

        var updated = JoinLines(lines, normalized.EndsWith('\n'));
        var nextSelectionStart = Math.Max(lineRange.StartOffset, selectionStart - removedBeforeSelection);
        var nextSelectionLength = selectionLength == 0 ? 0 : Math.Max(0, selectionLength - removedInSelection);
        return new IDEEditorTextEditResult(updated, nextSelectionStart, nextSelectionLength);
    }

    public static IDEEditorTextEditResult DuplicateSelectedLines(string text, int selectionStart, int selectionLength)
    {
        var normalized = Normalize(text);
        var lineRange = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        var segment = normalized.Substring(lineRange.StartOffset, lineRange.EndOffset - lineRange.StartOffset);
        if (segment.Length == 0)
        {
            return IDEEditorTextEditResult.Unchanged(normalized, selectionStart, selectionLength);
        }

        var insertOffset = lineRange.EndOffset;
        var insert = segment;
        if (insertOffset < normalized.Length && normalized[insertOffset] == '\n')
        {
            insertOffset++;
            insert += "\n";
        }
        else
        {
            insert = "\n" + insert;
        }

        var updated = normalized.Insert(insertOffset, insert);
        return new IDEEditorTextEditResult(updated, insertOffset, insert.Length);
    }

    public static IDEEditorTextEditResult DeleteSelectedLines(string text, int selectionStart, int selectionLength)
    {
        var normalized = Normalize(text);
        var lineRange = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        var deleteEnd = lineRange.EndOffset;
        if (deleteEnd < normalized.Length && normalized[deleteEnd] == '\n')
        {
            deleteEnd++;
        }
        else if (lineRange.StartOffset > 0)
        {
            lineRange = lineRange with { StartOffset = lineRange.StartOffset - 1 };
        }

        var updated = normalized.Remove(lineRange.StartOffset, deleteEnd - lineRange.StartOffset);
        return new IDEEditorTextEditResult(updated, Math.Min(lineRange.StartOffset, updated.Length), 0);
    }

    public static IDEEditorTextEditResult MoveSelectedLines(string text, int selectionStart, int selectionLength, int direction)
    {
        var normalized = Normalize(text);
        if (direction == 0)
        {
            return IDEEditorTextEditResult.Unchanged(normalized, selectionStart, selectionLength);
        }

        var lines = GetLines(normalized);
        if (lines.Count <= 1)
        {
            return IDEEditorTextEditResult.Unchanged(normalized, selectionStart, selectionLength);
        }

        var range = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        if (direction < 0 && range.StartLine == 0)
        {
            return IDEEditorTextEditResult.Unchanged(normalized, selectionStart, selectionLength);
        }

        if (direction > 0 && range.EndLine >= lines.Count - 1)
        {
            return IDEEditorTextEditResult.Unchanged(normalized, selectionStart, selectionLength);
        }

        var selected = lines.GetRange(range.StartLine, range.EndLine - range.StartLine + 1);
        lines.RemoveRange(range.StartLine, selected.Count);
        var insertIndex = direction < 0 ? range.StartLine - 1 : range.StartLine + 1;
        lines.InsertRange(insertIndex, selected);

        var updated = JoinLines(lines, normalized.EndsWith('\n'));
        var movedTextLength = selected.Sum(static line => line.Length) + Math.Max(0, selected.Count - 1);
        var nextStart = GetLineStartOffset(lines, insertIndex);
        return new IDEEditorTextEditResult(updated, nextStart, selectionLength == 0 ? 0 : movedTextLength);
    }

    public static IDEEditorTextEditResult ToggleLineComment(
        string text,
        int selectionStart,
        int selectionLength,
        string openComment,
        string closeComment)
    {
        var normalized = Normalize(text);
        var range = GetSelectedLineRange(normalized, selectionStart, selectionLength);
        var selectedText = normalized.Substring(range.StartOffset, range.EndOffset - range.StartOffset);
        var lines = GetLines(selectedText);
        var nonBlankLines = lines.Where(static line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var allCommented = nonBlankLines.Length > 0 &&
            nonBlankLines.All(line => line.TrimStart().StartsWith(openComment, StringComparison.Ordinal) &&
                                      line.TrimEnd().EndsWith(closeComment, StringComparison.Ordinal));

        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            lines[i] = allCommented
                ? UncommentLine(lines[i], openComment, closeComment)
                : CommentLine(lines[i], openComment, closeComment);
        }

        var replacement = string.Join('\n', lines);
        var updated = normalized.Remove(range.StartOffset, range.EndOffset - range.StartOffset).Insert(range.StartOffset, replacement);
        return new IDEEditorTextEditResult(updated, range.StartOffset, replacement.Length);
    }

    public static IDEEditorTextEditResult FormatAll(
        string text,
        Func<string, string> formatter,
        int selectionStart,
        int selectionLength)
    {
        var normalized = Normalize(text);
        var formatted = formatter(normalized) ?? normalized;
        return new IDEEditorTextEditResult(formatted, Math.Min(selectionStart, formatted.Length), Math.Min(selectionLength, Math.Max(0, formatted.Length - Math.Min(selectionStart, formatted.Length))));
    }

    public static IDEEditorTextSelectionRange NormalizeSelection(string text, int selectionStart, int selectionLength)
    {
        var normalized = Normalize(text);
        var start = Math.Clamp(selectionStart, 0, normalized.Length);
        var length = Math.Clamp(selectionLength, 0, normalized.Length - start);
        return new IDEEditorTextSelectionRange(start, length);
    }

    public static IDEEditorLineRange GetSelectedLineRange(string text, int selectionStart, int selectionLength)
    {
        var normalized = Normalize(text);
        var range = NormalizeSelection(normalized, selectionStart, selectionLength);
        var start = GetLineStart(normalized, range.Start);
        var endAnchor = range.Length == 0 ? range.Start : Math.Max(range.Start, range.Start + range.Length - 1);
        var end = GetLineEnd(normalized, endAnchor);
        var startLine = CountLineBreaks(normalized, 0, start);
        var endLine = CountLineBreaks(normalized, 0, endAnchor);
        return new IDEEditorLineRange(startLine, endLine, start, end);
    }

    public static int GetLineStart(string text, int index)
    {
        var normalized = Normalize(text);
        var cursor = Math.Clamp(index, 0, normalized.Length);
        while (cursor > 0 && normalized[cursor - 1] != '\n')
        {
            cursor--;
        }

        return cursor;
    }

    public static int GetLineEnd(string text, int index)
    {
        var normalized = Normalize(text);
        var cursor = Math.Clamp(index, 0, normalized.Length);
        while (cursor < normalized.Length && normalized[cursor] != '\n')
        {
            cursor++;
        }

        return cursor;
    }

    public static string Normalize(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static int RemoveLineIndent(ref string line, string indent)
    {
        if (line.StartsWith(indent, StringComparison.Ordinal))
        {
            line = line[indent.Length..];
            return indent.Length;
        }

        if (line.Length > 0 && line[0] == '\t')
        {
            line = line[1..];
            return 1;
        }

        var spaces = 0;
        while (spaces < line.Length && spaces < indent.Length && line[spaces] == ' ')
        {
            spaces++;
        }

        if (spaces == 0)
        {
            return 0;
        }

        line = line[spaces..];
        return spaces;
    }

    private static string CommentLine(string line, string openComment, string closeComment)
    {
        var indentLength = 0;
        while (indentLength < line.Length && char.IsWhiteSpace(line[indentLength]))
        {
            indentLength++;
        }

        return line.Insert(indentLength, openComment + " ") + " " + closeComment;
    }

    private static string UncommentLine(string line, string openComment, string closeComment)
    {
        var openIndex = line.IndexOf(openComment, StringComparison.Ordinal);
        var closeIndex = line.LastIndexOf(closeComment, StringComparison.Ordinal);
        if (openIndex < 0 || closeIndex < openIndex)
        {
            return line;
        }

        var updated = line.Remove(closeIndex, closeComment.Length).Remove(openIndex, openComment.Length);
        return updated.Replace("  ", " ", StringComparison.Ordinal).TrimEnd();
    }

    private static List<string> GetLines(string text)
    {
        return Normalize(text).Split('\n').ToList();
    }

    private static string JoinLines(IReadOnlyList<string> lines, bool preserveTrailingNewline)
    {
        var joined = string.Join('\n', lines);
        if (!preserveTrailingNewline || joined.EndsWith('\n'))
        {
            return joined;
        }

        return joined + "\n";
    }

    private static int GetLineStartOffset(IReadOnlyList<string> lines, int targetLine)
    {
        var offset = 0;
        for (var i = 0; i < targetLine && i < lines.Count; i++)
        {
            offset += lines[i].Length + 1;
        }

        return offset;
    }

    private static int CountLineBreaks(string text, int start, int end)
    {
        var count = 0;
        for (var i = Math.Max(0, start); i < end && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }
}

public readonly record struct IDEEditorTextSelectionRange(int Start, int Length);

public readonly record struct IDEEditorLineRange(int StartLine, int EndLine, int StartOffset, int EndOffset);
