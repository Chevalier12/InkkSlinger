using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public enum DesignerXmlSyntaxTokenKind
{
    ControlTypeName,
    PropertyName
}

public readonly record struct DesignerXmlSyntaxToken(int Start, int Length, DesignerXmlSyntaxTokenKind Kind)
{
    public int End => Start + Length;
}

public readonly record struct DesignerXmlSyntaxColors(Color DefaultForeground, Color ControlTypeForeground, Color PropertyForeground)
{
    public static readonly DesignerXmlSyntaxColors Default = new(
        new Color(216, 227, 238),
        new Color(127, 208, 255),
        new Color(255, 203, 111));
}

public static class DesignerXmlSyntaxHighlighter
{
    public static IReadOnlyList<DesignerXmlSyntaxToken> Classify(string? text)
    {
        var source = text ?? string.Empty;
        var tokens = new List<DesignerXmlSyntaxToken>();
        var index = 0;

        while (index < source.Length)
        {
            if (source[index] != '<')
            {
                index++;
                continue;
            }

            if (TrySkip(source, ref index, "<!--", "-->") ||
                TrySkip(source, ref index, "<![CDATA[", "]]>") ||
                TrySkip(source, ref index, "<?", "?>"))
            {
                continue;
            }

            if (Matches(source, index, "<!"))
            {
                SkipUntil(source, ref index, '>');
                continue;
            }

            index++;
            if (index < source.Length && source[index] == '/')
            {
                index++;
            }

            SkipWhitespace(source, ref index);
            var tagNameStart = index;
            ReadXmlName(source, ref index);
            AddToken(tokens, tagNameStart, index - tagNameStart, DesignerXmlSyntaxTokenKind.ControlTypeName);

            while (index < source.Length)
            {
                SkipWhitespace(source, ref index);
                if (index >= source.Length)
                {
                    break;
                }

                if (source[index] == '>')
                {
                    index++;
                    break;
                }

                if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '>')
                {
                    index += 2;
                    break;
                }

                if (source[index] == '"' || source[index] == '\'')
                {
                    SkipQuotedValue(source, ref index);
                    continue;
                }

                var attributeNameStart = index;
                ReadXmlName(source, ref index);
                if (index == attributeNameStart)
                {
                    index++;
                    continue;
                }

                var attributeNameLength = index - attributeNameStart;
                if (!IsNamespaceDeclaration(source.AsSpan(attributeNameStart, attributeNameLength)))
                {
                    AddToken(tokens, attributeNameStart, attributeNameLength, DesignerXmlSyntaxTokenKind.PropertyName);
                }

                SkipWhitespace(source, ref index);
                if (index < source.Length && source[index] == '=')
                {
                    index++;
                    SkipWhitespace(source, ref index);
                    if (index < source.Length && (source[index] == '"' || source[index] == '\''))
                    {
                        SkipQuotedValue(source, ref index);
                    }
                }
            }
        }

        return tokens;
    }

    public static FlowDocument CreateHighlightedDocument(string? text, DesignerXmlSyntaxColors? colors = null)
    {
        var document = new FlowDocument();
        PopulateDocument(document, text, colors);
        return document;
    }

    public static void PopulateDocument(FlowDocument document, string? text, DesignerXmlSyntaxColors? colors = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var palette = colors ?? DesignerXmlSyntaxColors.Default;
        var normalized = NormalizeLineEndings(text);
        var tokens = Classify(normalized);

        document.Blocks.Clear();
        var lines = normalized.Split('\n');
        var lineStart = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var paragraph = new Paragraph();
            AddLineRuns(paragraph, normalized, lineStart, lines[i].Length, tokens, palette);
            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty) { Foreground = palette.DefaultForeground });
            }

            document.Blocks.Add(paragraph);
            lineStart += lines[i].Length + 1;
        }

        if (document.Blocks.Count == 0)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(string.Empty) { Foreground = palette.DefaultForeground });
            document.Blocks.Add(paragraph);
        }
    }

    private static void AddLineRuns(
        Paragraph paragraph,
        string source,
        int lineStart,
        int lineLength,
        IReadOnlyList<DesignerXmlSyntaxToken> tokens,
        DesignerXmlSyntaxColors colors)
    {
        var lineEnd = lineStart + lineLength;
        var cursor = lineStart;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.End <= lineStart)
            {
                continue;
            }

            if (token.Start >= lineEnd)
            {
                break;
            }

            if (token.Start > cursor)
            {
                AddRun(paragraph, source.Substring(cursor, token.Start - cursor), colors.DefaultForeground);
            }

            var highlightStart = Math.Max(token.Start, lineStart);
            var highlightEnd = Math.Min(token.End, lineEnd);
            if (highlightEnd > highlightStart)
            {
                AddRun(
                    paragraph,
                    source.Substring(highlightStart, highlightEnd - highlightStart),
                    token.Kind == DesignerXmlSyntaxTokenKind.ControlTypeName ? colors.ControlTypeForeground : colors.PropertyForeground);
            }

            cursor = highlightEnd;
        }

        if (cursor < lineEnd)
        {
            AddRun(paragraph, source.Substring(cursor, lineEnd - cursor), colors.DefaultForeground);
        }
    }

    private static void AddRun(Paragraph paragraph, string text, Color foreground)
    {
        if (text.Length == 0)
        {
            return;
        }

        paragraph.Inlines.Add(new Run(text) { Foreground = foreground });
    }

    private static bool TrySkip(string source, ref int index, string prefix, string suffix)
    {
        if (!Matches(source, index, prefix))
        {
            return false;
        }

        index += prefix.Length;
        var suffixIndex = source.IndexOf(suffix, index, StringComparison.Ordinal);
        index = suffixIndex >= 0 ? suffixIndex + suffix.Length : source.Length;
        return true;
    }

    private static void SkipUntil(string source, ref int index, char terminator)
    {
        while (index < source.Length && source[index] != terminator)
        {
            index++;
        }

        if (index < source.Length)
        {
            index++;
        }
    }

    private static void SkipQuotedValue(string source, ref int index)
    {
        var quote = source[index];
        index++;
        while (index < source.Length && source[index] != quote)
        {
            index++;
        }

        if (index < source.Length)
        {
            index++;
        }
    }

    private static void SkipWhitespace(string source, ref int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index]))
        {
            index++;
        }
    }

    private static void ReadXmlName(string source, ref int index)
    {
        while (index < source.Length && IsXmlNameCharacter(source[index]))
        {
            index++;
        }
    }

    private static bool IsXmlNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is ':' or '_' or '-' or '.';
    }

    private static bool Matches(string source, int index, string value)
    {
        return index >= 0 &&
               index + value.Length <= source.Length &&
               string.Compare(source, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
    }

    private static void AddToken(List<DesignerXmlSyntaxToken> tokens, int start, int length, DesignerXmlSyntaxTokenKind kind)
    {
        if (length <= 0)
        {
            return;
        }

        tokens.Add(new DesignerXmlSyntaxToken(start, length, kind));
    }

    private static bool IsNamespaceDeclaration(ReadOnlySpan<char> value)
    {
        return value.SequenceEqual("xmlns".AsSpan()) || value.StartsWith("xmlns:".AsSpan(), StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}