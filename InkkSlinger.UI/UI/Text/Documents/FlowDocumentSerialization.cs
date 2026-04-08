using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static class FlowDocumentSerializer
{
    public const string ClipboardFormat = "application/x-inkkslinger-flowdocument+xml";

    public static string Serialize(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = new XElement(nameof(FlowDocument));
        foreach (var block in document.Blocks)
        {
            root.Add(SerializeBlock(block));
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    public static FlowDocument Deserialize(string? xml)
    {
        var document = new FlowDocument();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return document;
        }

        var root = XElement.Parse(xml);
        if (!string.Equals(root.Name.LocalName, nameof(FlowDocument), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Root element must be FlowDocument.");
        }

        foreach (var child in root.Elements())
        {
            document.Blocks.Add(DeserializeBlock(child));
        }

        return document;
    }

    public static string SerializeRange(FlowDocument document, DocumentTextSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        var range = selection.ToRange();
        var startOffset = DocumentPointers.GetDocumentOffset(range.Start);
        var endOffset = DocumentPointers.GetDocumentOffset(range.End);
        return SerializeRange(document, startOffset, endOffset);
    }

    public static string SerializeRange(FlowDocument document, int startOffset, int endOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (endOffset <= startOffset)
        {
            return Serialize(CreateDefaultFlowDocument());
        }

        var fragment = BuildDocumentFragment(document, startOffset, endOffset);
        return Serialize(fragment);
    }

    public static FlowDocument DeserializeFragment(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return CreateDefaultFlowDocument();
        }

        var root = XElement.Parse(xml);
        if (string.Equals(root.Name.LocalName, nameof(FlowDocument), StringComparison.Ordinal))
        {
            var document = Deserialize(xml);
            EnsureDocumentHasParagraph(document);
            return document;
        }

        if (string.Equals(root.Name.LocalName, "FlowDocumentFragment", StringComparison.Ordinal))
        {
            var document = new FlowDocument();
            foreach (var child in root.Elements())
            {
                document.Blocks.Add(DeserializeBlock(child));
            }

            EnsureDocumentHasParagraph(document);
            return document;
        }

        throw new InvalidOperationException(
            $"Root element must be '{nameof(FlowDocument)}' or 'FlowDocumentFragment'.");
    }

    private static XElement SerializeBlock(Block block)
    {
        return block switch
        {
            Paragraph paragraph => SerializeParagraph(paragraph),
            Section section => SerializeSection(section),
            BlockUIContainer => new XElement(nameof(BlockUIContainer)),
            List list => SerializeList(list),
            Table table => SerializeTable(table),
            _ => new XElement(block.GetType().Name)
        };
    }

    private static XElement SerializeSection(Section section)
    {
        var element = new XElement(nameof(Section));
        foreach (var block in section.Blocks)
        {
            element.Add(SerializeBlock(block));
        }

        return element;
    }

    private static XElement SerializeParagraph(Paragraph paragraph)
    {
        var element = new XElement(nameof(Paragraph));
        foreach (var inline in paragraph.Inlines)
        {
            element.Add(SerializeInline(inline));
        }

        return element;
    }

    private static XElement SerializeList(List list)
    {
        var element = new XElement(nameof(List));
        if (list.IsOrdered)
        {
            element.SetAttributeValue(nameof(List.IsOrdered), true);
        }

        foreach (var item in list.Items)
        {
            var itemElement = new XElement(nameof(ListItem));
            foreach (var block in item.Blocks)
            {
                itemElement.Add(SerializeBlock(block));
            }

            element.Add(itemElement);
        }

        return element;
    }

    private static XElement SerializeTable(Table table)
    {
        var element = new XElement(nameof(Table));
        foreach (var group in table.RowGroups)
        {
            var groupElement = new XElement(nameof(TableRowGroup));
            foreach (var row in group.Rows)
            {
                var rowElement = new XElement(nameof(TableRow));
                foreach (var cell in row.Cells)
                {
                    var cellElement = new XElement(nameof(TableCell));
                    if (cell.RowSpan > 1)
                    {
                        cellElement.SetAttributeValue(nameof(TableCell.RowSpan), cell.RowSpan);
                    }

                    if (cell.ColumnSpan > 1)
                    {
                        cellElement.SetAttributeValue(nameof(TableCell.ColumnSpan), cell.ColumnSpan);
                    }

                    foreach (var block in cell.Blocks)
                    {
                        cellElement.Add(SerializeBlock(block));
                    }

                    rowElement.Add(cellElement);
                }

                groupElement.Add(rowElement);
            }

            element.Add(groupElement);
        }

        return element;
    }

    private static XElement SerializeInline(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                var runElement = new XElement(nameof(Run), new XAttribute(nameof(Run.Text), run.Text));
                if (run.Foreground.HasValue)
                {
                    runElement.SetAttributeValue(nameof(Run.Foreground), SerializeColor(run.Foreground.Value));
                }

                return runElement;
            case LineBreak:
                return new XElement(nameof(LineBreak));
            case Hyperlink hyperlink:
                var hyperlinkElement = new XElement(nameof(Hyperlink));
                if (!string.IsNullOrWhiteSpace(hyperlink.NavigateUri))
                {
                    hyperlinkElement.SetAttributeValue(nameof(Hyperlink.NavigateUri), hyperlink.NavigateUri);
                }

                foreach (var nested in hyperlink.Inlines)
                {
                    hyperlinkElement.Add(SerializeInline(nested));
                }

                return hyperlinkElement;
            case Bold bold:
                var boldElement = new XElement(nameof(Bold));
                foreach (var nested in bold.Inlines)
                {
                    boldElement.Add(SerializeInline(nested));
                }

                return boldElement;
            case Italic italic:
                var italicElement = new XElement(nameof(Italic));
                foreach (var nested in italic.Inlines)
                {
                    italicElement.Add(SerializeInline(nested));
                }

                return italicElement;
            case Underline underline:
                var underlineElement = new XElement(nameof(Underline));
                foreach (var nested in underline.Inlines)
                {
                    underlineElement.Add(SerializeInline(nested));
                }

                return underlineElement;
            case Span span:
                var spanElement = new XElement(nameof(Span));
                foreach (var nested in span.Inlines)
                {
                    spanElement.Add(SerializeInline(nested));
                }

                return spanElement;
            case InlineUIContainer:
                return new XElement(nameof(InlineUIContainer));
            default:
                return new XElement(inline.GetType().Name);
        }
    }

    private static Block DeserializeBlock(XElement element)
    {
        return element.Name.LocalName switch
        {
            nameof(Paragraph) => DeserializeParagraph(element),
            nameof(Section) => DeserializeSection(element),
            nameof(BlockUIContainer) => new BlockUIContainer(),
            nameof(List) => DeserializeList(element),
            nameof(Table) => DeserializeTable(element),
            _ => throw new InvalidOperationException($"Unsupported block element '{element.Name.LocalName}'.")
        };
    }

    private static Section DeserializeSection(XElement element)
    {
        var section = new Section();
        foreach (var child in element.Elements())
        {
            section.Blocks.Add(DeserializeBlock(child));
        }

        return section;
    }

    private static Paragraph DeserializeParagraph(XElement element)
    {
        var paragraph = new Paragraph();
        foreach (var child in element.Elements())
        {
            paragraph.Inlines.Add(DeserializeInline(child));
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return paragraph;
    }

    private static List DeserializeList(XElement element)
    {
        var list = new List
        {
            IsOrdered = bool.TryParse((string?)element.Attribute(nameof(List.IsOrdered)), out var ordered) && ordered
        };
        foreach (var itemElement in element.Elements(nameof(ListItem)))
        {
            var item = new ListItem();
            foreach (var block in itemElement.Elements())
            {
                item.Blocks.Add(DeserializeBlock(block));
            }

            list.Items.Add(item);
        }

        return list;
    }

    private static Table DeserializeTable(XElement element)
    {
        var table = new Table();
        foreach (var groupElement in element.Elements(nameof(TableRowGroup)))
        {
            var group = new TableRowGroup();
            foreach (var rowElement in groupElement.Elements(nameof(TableRow)))
            {
                var row = new TableRow();
                foreach (var cellElement in rowElement.Elements(nameof(TableCell)))
                {
                    var cell = new TableCell();
                    if (int.TryParse((string?)cellElement.Attribute(nameof(TableCell.RowSpan)), out var rowSpan))
                    {
                        cell.RowSpan = rowSpan;
                    }

                    if (int.TryParse((string?)cellElement.Attribute(nameof(TableCell.ColumnSpan)), out var columnSpan))
                    {
                        cell.ColumnSpan = columnSpan;
                    }

                    foreach (var block in cellElement.Elements())
                    {
                        cell.Blocks.Add(DeserializeBlock(block));
                    }

                    row.Cells.Add(cell);
                }

                group.Rows.Add(row);
            }

            table.RowGroups.Add(group);
        }

        return table;
    }

    private static Inline DeserializeInline(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case nameof(Run):
                var run = new Run((string?)element.Attribute(nameof(Run.Text)) ?? string.Empty);
                if (TryParseColor((string?)element.Attribute(nameof(Run.Foreground)), out var runForeground))
                {
                    run.Foreground = runForeground;
                }

                return run;
            case nameof(LineBreak):
                return new LineBreak();
            case nameof(Bold):
                return DeserializeSpan(element, new Bold());
            case nameof(Italic):
                return DeserializeSpan(element, new Italic());
            case nameof(Underline):
                return DeserializeSpan(element, new Underline());
            case nameof(Hyperlink):
                var hyperlink = new Hyperlink
                {
                    NavigateUri = (string?)element.Attribute(nameof(Hyperlink.NavigateUri))
                };
                return DeserializeSpan(element, hyperlink);
            case nameof(Span):
                return DeserializeSpan(element, new Span());
            case nameof(InlineUIContainer):
                return new InlineUIContainer();
            default:
                throw new InvalidOperationException($"Unsupported inline element '{element.Name.LocalName}'.");
        }
    }

    private static T DeserializeSpan<T>(XElement element, T span)
        where T : Span
    {
        foreach (var child in element.Elements())
        {
            span.Inlines.Add(DeserializeInline(child));
        }

        return span;
    }

    private static FlowDocument BuildDocumentFragment(FlowDocument source, int startOffset, int endOffset)
    {
        var fragment = new FlowDocument();
        var paragraphs = FlowDocumentPlainText.EnumerateParagraphs(source).ToList();
        var offset = 0;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var paragraphLength = GetParagraphLogicalLength(paragraph);
            var paragraphStart = offset;
            var paragraphEnd = paragraphStart + paragraphLength;

            if (startOffset < paragraphEnd && endOffset > paragraphStart)
            {
                var localStart = Math.Max(0, startOffset - paragraphStart);
                var localEnd = Math.Min(paragraphLength, endOffset - paragraphStart);
                fragment.Blocks.Add(SliceParagraph(paragraph, localStart, localEnd));
            }

            offset = paragraphEnd;
            if (i < paragraphs.Count - 1)
            {
                offset += 1;
            }
        }

        EnsureDocumentHasParagraph(fragment);
        return fragment;
    }

    private static Paragraph SliceParagraph(Paragraph source, int start, int end)
    {
        var paragraph = new Paragraph();
        var offset = 0;
        foreach (var inline in source.Inlines)
        {
            var length = GetInlineLogicalLength(inline);
            var inlineStart = offset;
            var inlineEnd = inlineStart + length;
            var overlapStart = Math.Max(start, inlineStart);
            var overlapEnd = Math.Min(end, inlineEnd);
            if (overlapStart < overlapEnd)
            {
                var sliced = SliceInline(inline, overlapStart - inlineStart, overlapEnd - inlineStart);
                if (sliced != null)
                {
                    paragraph.Inlines.Add(sliced);
                }
            }

            offset = inlineEnd;
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return paragraph;
    }

    private static Inline? SliceInline(Inline inline, int localStart, int localEnd)
    {
        switch (inline)
        {
            case Run run:
                if (localEnd <= localStart)
                {
                    return null;
                }

                var clampedStart = Math.Clamp(localStart, 0, run.Text.Length);
                var clampedEnd = Math.Clamp(localEnd, clampedStart, run.Text.Length);
                var text = run.Text.Substring(clampedStart, clampedEnd - clampedStart);
                return new Run(text);
            case LineBreak:
                return localStart < 1 && localEnd > 0 ? new LineBreak() : null;
            case Hyperlink hyperlink:
                return SliceSpan(
                    hyperlink,
                    new Hyperlink { NavigateUri = hyperlink.NavigateUri },
                    localStart,
                    localEnd);
            case Bold bold:
                return SliceSpan(bold, new Bold(), localStart, localEnd);
            case Italic italic:
                return SliceSpan(italic, new Italic(), localStart, localEnd);
            case Underline underline:
                return SliceSpan(underline, new Underline(), localStart, localEnd);
            case Span span:
                return SliceSpan(span, new Span(), localStart, localEnd);
            case InlineUIContainer:
                return localStart < 1 && localEnd > 0 ? new InlineUIContainer() : null;
            default:
                return null;
        }
    }

    private static TSpan? SliceSpan<TSpan>(Span source, TSpan destination, int start, int end)
        where TSpan : Span
    {
        var offset = 0;
        foreach (var child in source.Inlines)
        {
            var length = GetInlineLogicalLength(child);
            var childStart = offset;
            var childEnd = childStart + length;
            var overlapStart = Math.Max(start, childStart);
            var overlapEnd = Math.Min(end, childEnd);
            if (overlapStart < overlapEnd)
            {
                var sliced = SliceInline(child, overlapStart - childStart, overlapEnd - childStart);
                if (sliced != null)
                {
                    destination.Inlines.Add(sliced);
                }
            }

            offset = childEnd;
        }

        return destination.Inlines.Count == 0 ? null : destination;
    }

    private static int GetParagraphLogicalLength(Paragraph paragraph)
    {
        var length = 0;
        foreach (var inline in paragraph.Inlines)
        {
            length += GetInlineLogicalLength(inline);
        }

        return length;
    }

    private static int GetInlineLogicalLength(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                return run.Text.Length;
            case LineBreak:
                return 1;
            case InlineUIContainer:
                return 1;
            case Span span:
                return span.Inlines.Sum(GetInlineLogicalLength);
            default:
                return 0;
        }
    }

    private static FlowDocument CreateDefaultFlowDocument()
    {
        var document = new FlowDocument();
        EnsureDocumentHasParagraph(document);
        return document;
    }

    private static void EnsureDocumentHasParagraph(FlowDocument document)
    {
        if (document.Blocks.Count > 0)
        {
            return;
        }

        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(string.Empty));
        document.Blocks.Add(paragraph);
    }

    private static string SerializeColor(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 6 &&
            byte.TryParse(trimmed[..2], System.Globalization.NumberStyles.HexNumber, null, out var r6) &&
            byte.TryParse(trimmed.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g6) &&
            byte.TryParse(trimmed.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b6))
        {
            color = new Color(r6, g6, b6, (byte)255);
            return true;
        }

        if (trimmed.Length == 8 &&
            byte.TryParse(trimmed[..2], System.Globalization.NumberStyles.HexNumber, null, out var a8) &&
            byte.TryParse(trimmed.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var r8) &&
            byte.TryParse(trimmed.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var g8) &&
            byte.TryParse(trimmed.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out var b8))
        {
            color = new Color(r8, g8, b8, a8);
            return true;
        }

        return false;
    }
}
