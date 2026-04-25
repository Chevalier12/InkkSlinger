using System;
using System.Collections.Generic;
using System.Linq;
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

public readonly record struct DesignerXmlSyntaxColors
{
    public DesignerXmlSyntaxColors(Color defaultForeground, Color controlTypeForeground, Color propertyForeground)
        : this(
            defaultForeground,
            controlTypeForeground,
            propertyForeground,
            new Color(102, 161, 218),
            new Color(183, 221, 151),
            new Color(106, 153, 85),
            new Color(146, 169, 191))
    {
    }

    public DesignerXmlSyntaxColors(
        Color defaultForeground,
        Color controlTypeForeground,
        Color propertyForeground,
        Color delimiterForeground,
        Color stringForeground,
        Color commentForeground,
        Color namespaceForeground)
    {
        DefaultForeground = defaultForeground;
        ControlTypeForeground = controlTypeForeground;
        PropertyForeground = propertyForeground;
        DelimiterForeground = delimiterForeground;
        StringForeground = stringForeground;
        CommentForeground = commentForeground;
        NamespaceForeground = namespaceForeground;
    }

    public Color DefaultForeground { get; }

    public Color ControlTypeForeground { get; }

    public Color PropertyForeground { get; }

    public Color DelimiterForeground { get; }

    public Color StringForeground { get; }

    public Color CommentForeground { get; }

    public Color NamespaceForeground { get; }

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

    public static IReadOnlyList<DesignerXmlSyntaxToken> Classify(string? text)
    {
        return IDEEditorXmlSyntaxClassifier.Classify(text)
            .Where(static token => token.Kind is IDEEditorXmlSyntaxTokenKind.ControlTypeName or IDEEditorXmlSyntaxTokenKind.PropertyName)
            .Select(static token => new DesignerXmlSyntaxToken(
                token.Start,
                token.Length,
                token.Kind == IDEEditorXmlSyntaxTokenKind.ControlTypeName
                    ? DesignerXmlSyntaxTokenKind.ControlTypeName
                    : DesignerXmlSyntaxTokenKind.PropertyName))
            .ToArray();
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
        var tokens = IDEEditorXmlSyntaxClassifier.Classify(normalized);

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
        if (document.Blocks.Count != previousLines.Length)
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
        var currentTokens = IDEEditorXmlSyntaxClassifier.Classify(currentNormalized);
        var lineStarts = ComputeLineStarts(currentLines);
        var palette = colors ?? DesignerXmlSyntaxColors.Default;
        var sharedPrefixCount = 0;
        var sharedPrefixLimit = Math.Min(previousLines.Length, currentLines.Length);
        while (sharedPrefixCount < sharedPrefixLimit &&
               string.Equals(previousLines[sharedPrefixCount], currentLines[sharedPrefixCount], StringComparison.Ordinal) &&
               previousLineStates[sharedPrefixCount] == currentLineStates[sharedPrefixCount])
        {
            sharedPrefixCount++;
        }

        var sharedSuffixCount = 0;
        while (sharedSuffixCount < previousLines.Length - sharedPrefixCount &&
               sharedSuffixCount < currentLines.Length - sharedPrefixCount)
        {
            var previousIndex = previousLines.Length - 1 - sharedSuffixCount;
            var currentIndex = currentLines.Length - 1 - sharedSuffixCount;
            if (!string.Equals(previousLines[previousIndex], currentLines[currentIndex], StringComparison.Ordinal) ||
                previousLineStates[previousIndex] != currentLineStates[currentIndex])
            {
                break;
            }

            sharedSuffixCount++;
        }

        var previousChangedCount = previousLines.Length - sharedPrefixCount - sharedSuffixCount;
        var currentChangedCount = currentLines.Length - sharedPrefixCount - sharedSuffixCount;
        if (previousChangedCount == 0 && currentChangedCount == 0)
        {
            return true;
        }

        for (var i = previousChangedCount - 1; i >= 0; i--)
        {
            document.Blocks.RemoveAt(sharedPrefixCount + i);
        }

        for (var i = 0; i < currentChangedCount; i++)
        {
            var lineIndex = sharedPrefixCount + i;
            var paragraph = CreateHighlightedParagraph(
                currentNormalized,
                lineStarts[lineIndex],
                currentLines[lineIndex].Length,
                currentTokens,
                palette);
            document.Blocks.Insert(sharedPrefixCount + i, paragraph);
        }

        return true;
    }

    private static void AddLineRuns(
        Paragraph paragraph,
        string source,
        int lineStart,
        int lineLength,
        IReadOnlyList<IDEEditorXmlSyntaxToken> tokens,
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
                    ResolveColor(token.Kind, colors));
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
        IReadOnlyList<IDEEditorXmlSyntaxToken> tokens,
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

    private static Color ResolveColor(IDEEditorXmlSyntaxTokenKind kind, DesignerXmlSyntaxColors colors)
    {
        return kind switch
        {
            IDEEditorXmlSyntaxTokenKind.ControlTypeName => colors.ControlTypeForeground,
            IDEEditorXmlSyntaxTokenKind.PropertyName => colors.PropertyForeground,
            IDEEditorXmlSyntaxTokenKind.ElementName => colors.DefaultForeground,
            IDEEditorXmlSyntaxTokenKind.Delimiter or IDEEditorXmlSyntaxTokenKind.Equals => colors.DelimiterForeground,
            IDEEditorXmlSyntaxTokenKind.String => colors.StringForeground,
            IDEEditorXmlSyntaxTokenKind.Comment or IDEEditorXmlSyntaxTokenKind.CData => colors.CommentForeground,
            IDEEditorXmlSyntaxTokenKind.NamespaceDeclaration => colors.NamespaceForeground,
            IDEEditorXmlSyntaxTokenKind.ProcessingInstruction or IDEEditorXmlSyntaxTokenKind.Declaration => colors.NamespaceForeground,
            _ => colors.DefaultForeground
        };
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

    private static bool Matches(string source, int index, string value)
    {
        return index >= 0 &&
               index + value.Length <= source.Length &&
               string.Compare(source, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
    }

    internal static bool TryClassifyTagName(string? tagName, out DesignerXmlSyntaxTokenKind kind)
    {
        if (!IDEEditorXmlSyntaxClassifier.TryClassifyTagName(tagName, out var resolvedKind))
        {
            kind = default;
            return false;
        }

        kind = resolvedKind == IDEEditorXmlSyntaxTokenKind.ControlTypeName
            ? DesignerXmlSyntaxTokenKind.ControlTypeName
            : DesignerXmlSyntaxTokenKind.PropertyName;
        return true;
    }

    private static string NormalizeLineEndings(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
