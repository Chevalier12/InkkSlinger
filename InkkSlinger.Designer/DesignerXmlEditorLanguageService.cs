using System;
using System.Collections.Generic;
using System.Linq;
using InkkSlinger;

namespace InkkSlinger.Designer;

internal static class DesignerXmlEditorLanguageService
{
    public const string Indent = IDEEditorTextCommandService.DefaultIndent;

    private static readonly IReadOnlyDictionary<char, char> PairedCharacters = new Dictionary<char, char>
    {
        ['"'] = '"',
        ['\''] = '\'',
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}'
    };

    public static bool TryHandlePairedCharacter(
        string text,
        int insertedIndex,
        char insertedCharacter,
        out IDEEditorTextEditResult edit)
    {
        var normalized = IDEEditorTextCommandService.Normalize(text);
        edit = IDEEditorTextEditResult.Unchanged(normalized, insertedIndex + 1, 0);

        if (!PairedCharacters.TryGetValue(insertedCharacter, out var close))
        {
            return false;
        }

        if (insertedIndex + 1 < normalized.Length && normalized[insertedIndex + 1] == close)
        {
            var updatedSkipOver = normalized.Remove(insertedIndex, 1);
            edit = new IDEEditorTextEditResult(updatedSkipOver, insertedIndex + 1, 0);
            return true;
        }

        if (!ShouldAutoPair(normalized, insertedIndex, insertedCharacter))
        {
            return false;
        }

        edit = new IDEEditorTextEditResult(normalized.Insert(insertedIndex + 1, close.ToString()), insertedIndex + 1, 0);
        return true;
    }

    public static bool TryWrapSelectionWithPair(
        string text,
        int selectionStart,
        int selectionLength,
        char typedCharacter,
        out IDEEditorTextEditResult edit)
    {
        edit = IDEEditorTextEditResult.Unchanged(text, selectionStart, selectionLength);
        if (selectionLength <= 0 || !PairedCharacters.TryGetValue(typedCharacter, out var close))
        {
            return false;
        }

        edit = IDEEditorTextCommandService.WrapSelection(text, selectionStart, selectionLength, typedCharacter.ToString(), close.ToString());
        return true;
    }

    public static IDEEditorTextEditResult ApplySmartEnter(string previousText, string currentText, int insertedIndex, string? indent = null)
    {
        var indentText = NormalizeIndent(indent);
        var previous = IDEEditorTextCommandService.Normalize(previousText);
        var current = IDEEditorTextCommandService.Normalize(currentText);
        var previousLineStart = IDEEditorTextCommandService.GetLineStart(previous, insertedIndex);
        var previousLineEnd = IDEEditorTextCommandService.GetLineEnd(previous, insertedIndex);
        var previousLine = previous.Substring(previousLineStart, previousLineEnd - previousLineStart);
        var beforeCaretOnPreviousLine = previous.Substring(previousLineStart, Math.Clamp(insertedIndex - previousLineStart, 0, previousLine.Length));
        var baseIndent = GetLeadingIndent(previousLine);
        var trimmedPreviousLine = previousLine.Trim();
        var nextChar = insertedIndex + 1 < current.Length ? current[insertedIndex + 1] : '\0';
        var nextText = insertedIndex + 1 < current.Length ? current[(insertedIndex + 1)..] : string.Empty;

        var desiredIndent = beforeCaretOnPreviousLine.Trim().Length == 0
            ? beforeCaretOnPreviousLine
            : baseIndent;
        if (beforeCaretOnPreviousLine.Trim().Length > 0 &&
            IsOpeningElementLine(trimmedPreviousLine) &&
            !IsSelfClosingElementLine(trimmedPreviousLine))
        {
            desiredIndent += indentText;
        }

        if (nextChar == '<' && nextText.StartsWith("</", StringComparison.Ordinal))
        {
            return new IDEEditorTextEditResult(current.Insert(insertedIndex + 1, desiredIndent), insertedIndex + 1 + desiredIndent.Length, 0);
        }

        if (desiredIndent.Length == 0)
        {
            return IDEEditorTextEditResult.Unchanged(current, insertedIndex + 1, 0);
        }

        return new IDEEditorTextEditResult(current.Insert(insertedIndex + 1, desiredIndent), insertedIndex + 1 + desiredIndent.Length, 0);
    }

    public static IDEEditorTextEditResult ToggleXmlComment(string text, int selectionStart, int selectionLength)
    {
        return IDEEditorTextCommandService.ToggleLineComment(text, selectionStart, selectionLength, "<!--", "-->");
    }

    public static string FormatDocument(string text, string? indent = null)
    {
        var indentText = NormalizeIndent(indent);
        var normalized = IDEEditorTextCommandService.Normalize(text);
        var lines = normalized.Split('\n');
        var formatted = new List<string>(lines.Length);
        var depth = 0;
        var inComment = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                formatted.Add(string.Empty);
                continue;
            }

            var startsClosing = StartsWithClosingStructuralToken(trimmed);
            if (startsClosing && depth > 0)
            {
                depth--;
            }

            formatted.Add(string.Concat(Enumerable.Repeat(indentText, depth)) + trimmed);

            var delta = ComputeDepthDelta(trimmed, ref inComment);
            depth = Math.Max(0, depth + delta);
        }

        return string.Join('\n', formatted);
    }

    public static bool TryFindMatchingTag(string text, int caretIndex, out IDEEditorTextSelectionRange match)
    {
        match = default;
        var normalized = IDEEditorTextCommandService.Normalize(text);
        if (!TryFindTagAtOrNearCaret(normalized, caretIndex, out var tag))
        {
            return false;
        }

        var tags = EnumerateTags(normalized).ToArray();
        var currentIndex = Array.FindIndex(tags, candidate => candidate.Start == tag.Start);
        if (currentIndex < 0)
        {
            return false;
        }

        if (tag.IsClosing)
        {
            var depth = 0;
            for (var i = currentIndex - 1; i >= 0; i--)
            {
                if (!string.Equals(tags[i].Name, tag.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (tags[i].IsClosing)
                {
                    depth++;
                }
                else if (!tags[i].IsSelfClosing)
                {
                    if (depth == 0)
                    {
                        match = new IDEEditorTextSelectionRange(tags[i].Start, tags[i].End - tags[i].Start);
                        return true;
                    }

                    depth--;
                }
            }
        }
        else if (!tag.IsSelfClosing)
        {
            var depth = 0;
            for (var i = currentIndex + 1; i < tags.Length; i++)
            {
                if (!string.Equals(tags[i].Name, tag.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!tags[i].IsClosing && !tags[i].IsSelfClosing)
                {
                    depth++;
                }
                else if (tags[i].IsClosing)
                {
                    if (depth == 0)
                    {
                        match = new IDEEditorTextSelectionRange(tags[i].Start, tags[i].End - tags[i].Start);
                        return true;
                    }

                    depth--;
                }
            }
        }

        return false;
    }

    public static bool TrySynchronizePairedTagRename(
        string previousText,
        string currentText,
        int selectionStart,
        out IDEEditorTextEditResult edit)
    {
        var previous = IDEEditorTextCommandService.Normalize(previousText);
        var current = IDEEditorTextCommandService.Normalize(currentText);
        edit = IDEEditorTextEditResult.Unchanged(current, selectionStart, 0);
        if (string.Equals(previous, current, StringComparison.Ordinal) ||
            !TryGetSingleChangedSpan(previous, current, out var previousStart, out var previousLength, out var currentStart, out var currentLength))
        {
            return false;
        }

        if (!TryFindTagNameAtChangedSpan(previous, previousStart, Math.Max(1, previousLength), out var previousTag) ||
            !TryFindTagNameAtChangedSpan(current, currentStart, Math.Max(1, currentLength), out var currentTag) ||
            previousTag.IsSelfClosing ||
            currentTag.IsSelfClosing ||
            previousTag.IsDeclaration ||
            currentTag.IsDeclaration ||
            previousTag.IsClosing != currentTag.IsClosing ||
            !IsValidXmlName(currentTag.Name))
        {
            return false;
        }

        if (!TryFindMatchingTagSpan(previous, previousTag, out var previousPair))
        {
            return false;
        }

        var pairNameStart = TranslateOffset(previousPair.NameStart, previousStart, previousLength, currentLength);
        var pairNameEnd = TranslateOffset(previousPair.NameEnd, previousStart, previousLength, currentLength);
        if (pairNameStart < 0 ||
            pairNameEnd < pairNameStart ||
            pairNameEnd > current.Length ||
            string.Equals(current[pairNameStart..pairNameEnd], currentTag.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var updated = current.Remove(pairNameStart, pairNameEnd - pairNameStart).Insert(pairNameStart, currentTag.Name);
        var nextSelectionStart = selectionStart;
        if (pairNameStart < selectionStart)
        {
            nextSelectionStart += currentTag.Name.Length - (pairNameEnd - pairNameStart);
        }

        edit = new IDEEditorTextEditResult(updated, Math.Clamp(nextSelectionStart, 0, updated.Length), 0);
        return true;
    }

    public static IReadOnlyList<DesignerXmlFoldRange> GetFoldRanges(string text)
    {
        var normalized = IDEEditorTextCommandService.Normalize(text);
        var ranges = new List<DesignerXmlFoldRange>();
        AddElementFoldRanges(normalized, ranges);
        AddRegionCommentFoldRanges(normalized, ranges);
        return ranges
            .Where(static range => range.EndLine > range.StartLine)
            .OrderBy(static range => range.StartLine)
            .ThenByDescending(static range => range.EndLine)
            .ToArray();
    }

    public static IReadOnlyList<DesignerXmlDocumentOverviewItem> GetDocumentOverview(string text)
    {
        var normalized = IDEEditorTextCommandService.Normalize(text);
        var items = new List<DesignerXmlDocumentOverviewItem>();
        foreach (var tag in EnumerateTags(normalized))
        {
            if (tag.IsClosing || tag.IsDeclaration)
            {
                continue;
            }

            var line = GetLineNumber(normalized, tag.Start);
            var indentLevel = CountLeadingWhitespace(GetLineTextAtOffset(normalized, tag.Start)) / Math.Max(1, Indent.Length);
            items.Add(new DesignerXmlDocumentOverviewItem(tag.Name, line, tag.Start, indentLevel));
        }

        return items;
    }

    public static bool TryCreateFoldedProjection(
        string text,
        IReadOnlyCollection<DesignerXmlFoldRange> collapsedRanges,
        out string projection)
    {
        var normalized = IDEEditorTextCommandService.Normalize(text);
        projection = normalized;
        if (collapsedRanges.Count == 0)
        {
            return false;
        }

        var ordered = collapsedRanges
            .Where(static range => range.HiddenStartOffset < range.HiddenEndOffset)
            .OrderByDescending(static range => range.HiddenStartOffset)
            .ToArray();
        if (ordered.Length == 0)
        {
            return false;
        }

        foreach (var range in ordered)
        {
            var indent = GetLeadingIndent(GetLineTextAtOffset(normalized, range.StartOffset));
            var placeholder = "\n" + indent + Indent + "...\n";
            projection = projection.Remove(range.HiddenStartOffset, range.HiddenEndOffset - range.HiddenStartOffset)
                .Insert(range.HiddenStartOffset, placeholder);
        }

        return true;
    }

    public static bool TryFindFoldRangeAtOrNearCaret(string text, int caretIndex, out DesignerXmlFoldRange range)
    {
        var normalized = IDEEditorTextCommandService.Normalize(text);
        var caretLine = GetLineNumber(normalized, Math.Clamp(caretIndex, 0, normalized.Length));
        range = GetFoldRanges(normalized)
            .Where(candidate => caretLine >= candidate.StartLine && caretLine <= candidate.EndLine)
            .OrderBy(candidate => candidate.EndLine - candidate.StartLine)
            .FirstOrDefault();
        return range.EndLine > range.StartLine;
    }

    private static bool ShouldAutoPair(string text, int insertedIndex, char insertedCharacter)
    {
        if (insertedCharacter is '"' or '\'')
        {
            return IsInsideTag(text, insertedIndex);
        }

        return true;
    }

    private static bool IsOpeningElementLine(string trimmedLine)
    {
        return trimmedLine.StartsWith("<", StringComparison.Ordinal) &&
               !trimmedLine.StartsWith("</", StringComparison.Ordinal) &&
               !trimmedLine.StartsWith("<!--", StringComparison.Ordinal) &&
               !trimmedLine.StartsWith("<?", StringComparison.Ordinal) &&
               !trimmedLine.StartsWith("<!", StringComparison.Ordinal) &&
               trimmedLine.EndsWith(">", StringComparison.Ordinal);
    }

    private static bool IsSelfClosingElementLine(string trimmedLine)
    {
        return trimmedLine.EndsWith("/>", StringComparison.Ordinal) ||
               trimmedLine.Contains("</", StringComparison.Ordinal);
    }

    private static bool StartsWithClosingStructuralToken(string trimmed)
    {
        return trimmed.StartsWith("</", StringComparison.Ordinal) ||
               trimmed.StartsWith("}", StringComparison.Ordinal) ||
               trimmed.StartsWith("]", StringComparison.Ordinal) ||
               trimmed.StartsWith(")", StringComparison.Ordinal);
    }

    private static int ComputeDepthDelta(string trimmed, ref bool inComment)
    {
        if (inComment)
        {
            if (trimmed.Contains("-->", StringComparison.Ordinal))
            {
                inComment = false;
            }

            return 0;
        }

        if (trimmed.StartsWith("<!--", StringComparison.Ordinal) && !trimmed.Contains("-->", StringComparison.Ordinal))
        {
            inComment = true;
            return 0;
        }

        var delta = 0;
        foreach (var tag in EnumerateTags(trimmed))
        {
            if (tag.IsDeclaration || tag.IsSelfClosing)
            {
                continue;
            }

            delta += tag.IsClosing ? -1 : 1;
        }

        return delta;
    }

    private static void AddElementFoldRanges(string text, List<DesignerXmlFoldRange> ranges)
    {
        var stack = new Stack<XmlTagSpan>();
        foreach (var tag in EnumerateTags(text))
        {
            if (tag.IsDeclaration || tag.IsSelfClosing)
            {
                continue;
            }

            if (!tag.IsClosing)
            {
                stack.Push(tag);
                continue;
            }

            while (stack.Count > 0)
            {
                var open = stack.Pop();
                if (!string.Equals(open.Name, tag.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                var startLine = GetLineNumber(text, open.Start);
                var endLine = GetLineNumber(text, tag.Start);
                if (endLine > startLine)
                {
                    var hiddenStart = open.End;
                    var hiddenEnd = IDEEditorTextCommandService.GetLineStart(text, tag.Start);
                    ranges.Add(new DesignerXmlFoldRange(open.Name, startLine, endLine, open.Start, tag.End, hiddenStart, hiddenEnd));
                }

                break;
            }
        }
    }

    private static void AddRegionCommentFoldRanges(string text, List<DesignerXmlFoldRange> ranges)
    {
        var regionStack = new Stack<(string Name, int Line, int Offset, int EndOffset)>();
        var lines = text.Split('\n');
        var offset = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var trimmed = lines[lineIndex].Trim();
            if (trimmed.StartsWith("<!--", StringComparison.Ordinal) &&
                trimmed.Contains("#region", StringComparison.OrdinalIgnoreCase))
            {
                var name = trimmed
                    .Replace("<!--", string.Empty, StringComparison.Ordinal)
                    .Replace("-->", string.Empty, StringComparison.Ordinal)
                    .Replace("#region", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
                regionStack.Push((string.IsNullOrWhiteSpace(name) ? "region" : name, lineIndex + 1, offset, offset + lines[lineIndex].Length));
            }
            else if (trimmed.StartsWith("<!--", StringComparison.Ordinal) &&
                     trimmed.Contains("#endregion", StringComparison.OrdinalIgnoreCase) &&
                     regionStack.Count > 0)
            {
                var region = regionStack.Pop();
                if (lineIndex + 1 > region.Line)
                {
                    ranges.Add(new DesignerXmlFoldRange(
                        region.Name,
                        region.Line,
                        lineIndex + 1,
                        region.Offset,
                        offset + lines[lineIndex].Length,
                        region.EndOffset,
                        offset));
                }
            }

            offset += lines[lineIndex].Length + 1;
        }
    }

    private static bool IsInsideTag(string text, int index)
    {
        var open = text.LastIndexOf('<', Math.Clamp(index, 0, Math.Max(0, text.Length - 1)));
        if (open < 0)
        {
            return false;
        }

        var close = text.LastIndexOf('>', Math.Clamp(index, 0, Math.Max(0, text.Length - 1)));
        return close < open;
    }

    private static bool HasOnlyWhitespaceBetween(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetLeadingIndent(string line)
    {
        var count = 0;
        while (count < line.Length && (line[count] == ' ' || line[count] == '\t'))
        {
            count++;
        }

        return line[..count];
    }

    private static bool TryFindTagAtOrNearCaret(string text, int caretIndex, out XmlTagSpan tag)
    {
        foreach (var candidate in EnumerateTags(text))
        {
            if (caretIndex >= candidate.Start && caretIndex <= candidate.End)
            {
                tag = candidate;
                return true;
            }
        }

        tag = default;
        return false;
    }

    private static IEnumerable<XmlTagSpan> EnumerateTags(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf('<', index);
            if (start < 0)
            {
                yield break;
            }

            if (start + 1 >= text.Length)
            {
                yield break;
            }

            var next = text[start + 1];
            if (next == '!' || next == '?')
            {
                var declarationEnd = FindTagEnd(text, start);
                if (declarationEnd < 0)
                {
                    yield break;
                }

                yield return new XmlTagSpan(string.Empty, start, declarationEnd + 1, start, start, false, true, true);
                index = declarationEnd + 1;
                continue;
            }

            var end = FindTagEnd(text, start);
            if (end < 0)
            {
                yield break;
            }

            var isClosing = next == '/';
            var nameStart = start + (isClosing ? 2 : 1);
            while (nameStart < end && char.IsWhiteSpace(text[nameStart]))
            {
                nameStart++;
            }

            var nameEnd = nameStart;
            while (nameEnd < end && IsXmlNameChar(text[nameEnd]))
            {
                nameEnd++;
            }

            if (nameEnd > nameStart)
            {
                var name = text[nameStart..nameEnd];
                var selfClosing = !isClosing && IsSelfClosingTag(text, start, end);
                yield return new XmlTagSpan(name, start, end + 1, nameStart, nameEnd, isClosing, selfClosing, false);
            }

            index = end + 1;
        }
    }

    private static int FindTagEnd(string text, int tagStart)
    {
        var quote = '\0';
        for (var i = tagStart + 1; i < text.Length; i++)
        {
            var current = text[i];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }

            if (current == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsSelfClosingTag(string text, int start, int end)
    {
        var index = end - 1;
        while (index > start && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index > start && text[index] == '/';
    }

    private static bool IsXmlNameChar(char value)
    {
        return char.IsLetterOrDigit(value) || value is ':' or '_' or '-' or '.';
    }

    private static bool IsValidXmlName(string name)
    {
        return name.Length > 0 &&
               (char.IsLetter(name[0]) || name[0] is '_' or ':') &&
               name.All(IsXmlNameChar);
    }

    private static string NormalizeIndent(string? indent)
    {
        return string.IsNullOrEmpty(indent) ? Indent : indent;
    }

    private static bool TryGetSingleChangedSpan(
        string previous,
        string current,
        out int previousStart,
        out int previousLength,
        out int currentStart,
        out int currentLength)
    {
        previousStart = 0;
        previousLength = 0;
        currentStart = 0;
        currentLength = 0;

        while (previousStart < previous.Length &&
               previousStart < current.Length &&
               previous[previousStart] == current[previousStart])
        {
            previousStart++;
        }

        var previousEnd = previous.Length;
        var currentEnd = current.Length;
        while (previousEnd > previousStart &&
               currentEnd > previousStart &&
               previous[previousEnd - 1] == current[currentEnd - 1])
        {
            previousEnd--;
            currentEnd--;
        }

        previousLength = previousEnd - previousStart;
        currentStart = previousStart;
        currentLength = currentEnd - currentStart;
        return previousLength > 0 || currentLength > 0;
    }

    private static bool TryFindTagNameAtChangedSpan(string text, int start, int length, out XmlTagSpan tag)
    {
        var end = start + length;
        foreach (var candidate in EnumerateTags(text))
        {
            if (start <= candidate.NameEnd && end >= candidate.NameStart)
            {
                tag = candidate;
                return true;
            }
        }

        tag = default;
        return false;
    }

    private static bool TryFindMatchingTagSpan(string text, XmlTagSpan tag, out XmlTagSpan match)
    {
        match = default;
        var tags = EnumerateTags(text).ToArray();
        var currentIndex = Array.FindIndex(tags, candidate => candidate.Start == tag.Start);
        if (currentIndex < 0)
        {
            return false;
        }

        if (tag.IsClosing)
        {
            var depth = 0;
            for (var i = currentIndex - 1; i >= 0; i--)
            {
                if (!string.Equals(tags[i].Name, tag.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (tags[i].IsClosing)
                {
                    depth++;
                }
                else if (!tags[i].IsSelfClosing)
                {
                    if (depth == 0)
                    {
                        match = tags[i];
                        return true;
                    }

                    depth--;
                }
            }
        }
        else
        {
            var depth = 0;
            for (var i = currentIndex + 1; i < tags.Length; i++)
            {
                if (!string.Equals(tags[i].Name, tag.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!tags[i].IsClosing && !tags[i].IsSelfClosing)
                {
                    depth++;
                }
                else if (tags[i].IsClosing)
                {
                    if (depth == 0)
                    {
                        match = tags[i];
                        return true;
                    }

                    depth--;
                }
            }
        }

        return false;
    }

    private static int TranslateOffset(int offset, int previousStart, int previousLength, int currentLength)
    {
        if (offset <= previousStart)
        {
            return offset;
        }

        if (offset >= previousStart + previousLength)
        {
            return offset + currentLength - previousLength;
        }

        return previousStart + currentLength;
    }

    private static int GetLineNumber(string text, int offset)
    {
        return text.Take(Math.Clamp(offset, 0, text.Length)).Count(static character => character == '\n') + 1;
    }

    private static string GetLineTextAtOffset(string text, int offset)
    {
        var lineStart = IDEEditorTextCommandService.GetLineStart(text, offset);
        var lineEnd = IDEEditorTextCommandService.GetLineEnd(text, offset);
        return text[lineStart..lineEnd];
    }

    private static int CountLeadingWhitespace(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsWhiteSpace(text[count]))
        {
            count++;
        }

        return count;
    }

    private readonly record struct XmlTagSpan(
        string Name,
        int Start,
        int End,
        int NameStart,
        int NameEnd,
        bool IsClosing,
        bool IsSelfClosing,
        bool IsDeclaration);
}

internal readonly record struct DesignerXmlFoldRange(
    string Name,
    int StartLine,
    int EndLine,
    int StartOffset,
    int EndOffset,
    int HiddenStartOffset,
    int HiddenEndOffset);

public sealed class DesignerXmlDocumentOverviewItem
{
    public DesignerXmlDocumentOverviewItem(string name, int lineNumber, int offset, int indentLevel)
    {
        Name = name;
        LineNumber = lineNumber;
        Offset = offset;
        IndentLevel = Math.Max(0, indentLevel);
        DisplayText = new string(' ', IndentLevel * 2) + name;
        LineText = "L" + lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public string Name { get; }

    public int LineNumber { get; }

    public int Offset { get; }

    public int IndentLevel { get; }

    public string DisplayText { get; }

    public string LineText { get; }
}
