using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private enum XmlHighlightLineState
    {
        Normal,
        InsideTag,
        InsideSingleQuotedValue,
        InsideDoubleQuotedValue,
        InsideComment,
        InsideCData,
        InsideProcessingInstruction,
        InsideDeclaration
    }

    private static readonly IReadOnlyDictionary<string, Type> KnownControlTypesByName = XamlLoader.GetKnownTypes()
        .GroupBy(static knownType => knownType.Name, StringComparer.Ordinal)
        .ToDictionary(static group => group.Key, static group => group.First().Type, StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, DesignerXmlSyntaxTokenKind?> TagNameTokenKinds = new(StringComparer.Ordinal);

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
            AddTagNameToken(tokens, source, tagNameStart, index - tagNameStart);

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

    public static bool TryPopulateDocumentIncrementally(
        FlowDocument document,
        string? previousText,
        string? currentText,
        DesignerXmlSyntaxColors? colors = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var previousNormalized = NormalizeLineEndings(previousText);
        var currentNormalized = NormalizeLineEndings(currentText);
        if (string.Equals(previousNormalized, currentNormalized, StringComparison.Ordinal))
        {
            return true;
        }

        var previousLines = previousNormalized.Split('\n');
        var currentLines = currentNormalized.Split('\n');
        if (previousLines.Length != currentLines.Length || document.Blocks.Count != currentLines.Length)
        {
            return false;
        }

        for (var i = 0; i < document.Blocks.Count; i++)
        {
            if (document.Blocks[i] is not Paragraph)
            {
                return false;
            }
        }

        var previousLineStates = ComputeLineStartStates(previousNormalized, previousLines.Length);
        var currentLineStates = ComputeLineStartStates(currentNormalized, currentLines.Length);
        var currentTokens = Classify(currentNormalized);
        var lineStarts = ComputeLineStarts(currentLines);
        var palette = colors ?? DesignerXmlSyntaxColors.Default;
        var refreshedAnyParagraph = false;

        for (var i = 0; i < currentLines.Length; i++)
        {
            if (string.Equals(previousLines[i], currentLines[i], StringComparison.Ordinal) &&
                previousLineStates[i] == currentLineStates[i])
            {
                continue;
            }

            var existingParagraph = (Paragraph)document.Blocks[i];
            document.Blocks[i] = CreateHighlightedParagraph(
                currentNormalized,
                lineStarts[i],
                currentLines[i].Length,
                currentTokens,
                palette,
                existingParagraph);
            refreshedAnyParagraph = true;
        }

        return refreshedAnyParagraph;
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

    private static Paragraph CreateHighlightedParagraph(
        string source,
        int lineStart,
        int lineLength,
        IReadOnlyList<DesignerXmlSyntaxToken> tokens,
        DesignerXmlSyntaxColors colors,
        Paragraph? templateParagraph = null)
    {
        var paragraph = new Paragraph();
        if (templateParagraph is not null)
        {
            CopyParagraphSettings(templateParagraph, paragraph);
        }

        AddLineRuns(paragraph, source, lineStart, lineLength, tokens, colors);
        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty) { Foreground = colors.DefaultForeground });
        }

        return paragraph;
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

    private static int[] ComputeLineStarts(string[] lines)
    {
        var starts = new int[lines.Length];
        var offset = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            starts[i] = offset;
            offset += lines[i].Length + 1;
        }

        return starts;
    }

    private static XmlHighlightLineState[] ComputeLineStartStates(string source, int lineCount)
    {
        var states = new XmlHighlightLineState[Math.Max(lineCount, 1)];
        var lineIndex = 0;
        var state = XmlHighlightLineState.Normal;

        states[0] = state;

        var index = 0;
        while (index < source.Length)
        {
            if (source[index] == '\n')
            {
                lineIndex++;
                if (lineIndex < states.Length)
                {
                    states[lineIndex] = state;
                }

                index++;
                continue;
            }

            switch (state)
            {
                case XmlHighlightLineState.Normal:
                    if (source[index] != '<')
                    {
                        index++;
                        continue;
                    }

                    if (Matches(source, index, "<!--"))
                    {
                        index += 4;
                        state = XmlHighlightLineState.InsideComment;
                        continue;
                    }

                    if (Matches(source, index, "<![CDATA["))
                    {
                        index += 9;
                        state = XmlHighlightLineState.InsideCData;
                        continue;
                    }

                    if (Matches(source, index, "<?"))
                    {
                        index += 2;
                        state = XmlHighlightLineState.InsideProcessingInstruction;
                        continue;
                    }

                    if (Matches(source, index, "<!"))
                    {
                        index += 2;
                        state = XmlHighlightLineState.InsideDeclaration;
                        continue;
                    }

                    index++;
                    if (index < source.Length && source[index] == '/')
                    {
                        index++;
                    }

                    state = XmlHighlightLineState.InsideTag;
                    continue;

                case XmlHighlightLineState.InsideTag:
                    if (source[index] == '"')
                    {
                        state = XmlHighlightLineState.InsideDoubleQuotedValue;
                        index++;
                        continue;
                    }

                    if (source[index] == '\'')
                    {
                        state = XmlHighlightLineState.InsideSingleQuotedValue;
                        index++;
                        continue;
                    }

                    if (source[index] == '>')
                    {
                        state = XmlHighlightLineState.Normal;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideSingleQuotedValue:
                    if (source[index] == '\'')
                    {
                        state = XmlHighlightLineState.InsideTag;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideDoubleQuotedValue:
                    if (source[index] == '"')
                    {
                        state = XmlHighlightLineState.InsideTag;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideComment:
                    if (Matches(source, index, "-->"))
                    {
                        index += 3;
                        state = XmlHighlightLineState.Normal;
                        continue;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideCData:
                    if (Matches(source, index, "]]>") )
                    {
                        index += 3;
                        state = XmlHighlightLineState.Normal;
                        continue;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideProcessingInstruction:
                    if (Matches(source, index, "?>"))
                    {
                        index += 2;
                        state = XmlHighlightLineState.Normal;
                        continue;
                    }

                    index++;
                    continue;

                case XmlHighlightLineState.InsideDeclaration:
                    if (source[index] == '>')
                    {
                        state = XmlHighlightLineState.Normal;
                    }

                    index++;
                    continue;
            }
        }

        return states;
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

    private static void AddTagNameToken(List<DesignerXmlSyntaxToken> tokens, string source, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        var tagName = source.Substring(start, length);
        var kind = TagNameTokenKinds.GetOrAdd(tagName, static candidate => ClassifyTagName(candidate));
        if (!kind.HasValue)
        {
            return;
        }

        tokens.Add(new DesignerXmlSyntaxToken(start, length, kind.Value));
    }

    internal static bool TryClassifyTagName(string? tagName, out DesignerXmlSyntaxTokenKind kind)
    {
        var resolvedKind = ClassifyTagName(tagName ?? string.Empty);
        if (!resolvedKind.HasValue)
        {
            kind = default;
            return false;
        }

        kind = resolvedKind.Value;
        return true;
    }

    private static DesignerXmlSyntaxTokenKind? ClassifyTagName(string tagName)
    {
        var candidate = tagName.AsSpan();
        if (TryResolveKnownType(candidate, out _))
        {
            return DesignerXmlSyntaxTokenKind.ControlTypeName;
        }

        if (IsKnownPropertyElement(candidate))
        {
            return DesignerXmlSyntaxTokenKind.PropertyName;
        }

        return null;
    }

    private static bool IsKnownPropertyElement(ReadOnlySpan<char> tagName)
    {
        var separatorIndex = tagName.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= tagName.Length - 1)
        {
            return false;
        }

        if (!TryResolveKnownType(tagName[..separatorIndex], out var ownerType))
        {
            return false;
        }

        var propertyName = tagName[(separatorIndex + 1)..].ToString();
        return HasInstanceProperty(ownerType, propertyName) ||
               HasDependencyPropertyField(ownerType, propertyName) ||
               HasAttachedSetter(ownerType, propertyName);
    }

    private static bool TryResolveKnownType(ReadOnlySpan<char> typeName, out Type type)
    {
        var unqualifiedName = UnqualifyXmlName(typeName).ToString();
        return KnownControlTypesByName.TryGetValue(unqualifiedName, out type!);
    }

    private static ReadOnlySpan<char> UnqualifyXmlName(ReadOnlySpan<char> value)
    {
        var separatorIndex = value.IndexOf(':');
        return separatorIndex >= 0 ? value[(separatorIndex + 1)..] : value;
    }

    private static bool HasInstanceProperty(Type ownerType, string propertyName)
    {
        var current = ownerType;
        while (current != null)
        {
            if (current.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) != null)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasDependencyPropertyField(Type ownerType, string propertyName)
    {
        var fieldName = propertyName + "Property";
        var current = ownerType;
        while (current != null)
        {
            var field = current.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.FieldType == typeof(DependencyProperty))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasAttachedSetter(Type ownerType, string propertyName)
    {
        var setterName = "Set" + propertyName;
        var current = ownerType;
        while (current != null)
        {
            foreach (var method in current.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(method.Name, setterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (method.GetParameters().Length == 2)
                {
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
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