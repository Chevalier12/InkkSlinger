using System;
using System.Xml.Linq;

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
                return new XElement(nameof(Run), new XAttribute(nameof(Run.Text), run.Text));
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
        var list = new List();
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
                return new Run((string?)element.Attribute(nameof(Run.Text)) ?? string.Empty);
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
}
