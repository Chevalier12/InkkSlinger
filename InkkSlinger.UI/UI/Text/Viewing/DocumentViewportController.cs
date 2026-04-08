using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class DocumentViewportController
{
    internal readonly record struct ParagraphSelectionEntry(Paragraph Paragraph, int StartOffset, int EndOffset);

    public static void ClampOffsets(
        ref float horizontalOffset,
        ref float verticalOffset,
        DocumentLayoutResult layout,
        float viewportWidth,
        float viewportHeight)
    {
        var maxHorizontal = Math.Max(0f, layout.ContentWidth - Math.Max(0f, viewportWidth));
        var maxVertical = Math.Max(0f, layout.ContentHeight - Math.Max(0f, viewportHeight));
        horizontalOffset = Math.Clamp(horizontalOffset, 0f, maxHorizontal);
        verticalOffset = Math.Clamp(verticalOffset, 0f, maxVertical);
    }

    public static int HitTestDocumentOffset(
        DocumentLayoutResult layout,
        Vector2 pointerPosition,
        LayoutRect textRect,
        float horizontalOffset,
        float verticalOffset,
        float zoomScale)
    {
        if (layout.Lines.Count == 0)
        {
            return 0;
        }

        var localX = ((pointerPosition.X - textRect.X) + horizontalOffset) / zoomScale;
        var localY = ((pointerPosition.Y - textRect.Y) + verticalOffset) / zoomScale;
        return layout.HitTestOffset(new Vector2(localX, localY));
    }

    public static DocumentPageMap BuildPageMap(DocumentLayoutResult layout, float viewportHeight, float zoomScale)
    {
        var unscaledViewportHeight = viewportHeight <= 0f ? 0f : viewportHeight / zoomScale;
        return DocumentPageMap.Build(layout, unscaledViewportHeight);
    }

    public static Hyperlink? ResolveHyperlinkAtOffset(FlowDocument document, int offset)
    {
        var paragraphs = CollectParagraphEntries(document);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (offset < paragraphs[i].StartOffset || offset > paragraphs[i].EndOffset)
            {
                continue;
            }

            var localOffset = Math.Clamp(offset - paragraphs[i].StartOffset, 0, Math.Max(0, paragraphs[i].EndOffset - paragraphs[i].StartOffset));
            return ResolveHyperlinkWithinInlines(paragraphs[i].Paragraph.Inlines, localOffset);
        }

        return null;
    }

    public static List<ParagraphSelectionEntry> CollectParagraphEntries(FlowDocument document)
    {
        var entries = new List<ParagraphSelectionEntry>();
        var runningOffset = 0;
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(document));
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var length = GetParagraphLogicalLength(paragraph);
            entries.Add(new ParagraphSelectionEntry(paragraph, runningOffset, runningOffset + length));
            runningOffset += length;
            if (i < paragraphs.Count - 1)
            {
                runningOffset += 1;
            }
        }

        return entries;
    }

    public static IEnumerable<Hyperlink> EnumerateHyperlinks(FlowDocument document)
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            foreach (var hyperlink in EnumerateHyperlinks(document.Blocks[i]))
            {
                yield return hyperlink;
            }
        }
    }

    private static Hyperlink? ResolveHyperlinkWithinInlines(IEnumerable<Inline> inlines, int localOffset)
    {
        var cursor = 0;
        foreach (var inline in inlines)
        {
            var length = GetInlineLogicalLength(inline);
            var end = cursor + length;
            if (localOffset < cursor || localOffset > end)
            {
                cursor = end;
                continue;
            }

            if (inline is Hyperlink hyperlink &&
                (!string.IsNullOrWhiteSpace(hyperlink.NavigateUri) || hyperlink.Command != null))
            {
                return hyperlink;
            }

            if (inline is Span span)
            {
                var nested = ResolveHyperlinkWithinInlines(span.Inlines, Math.Max(0, localOffset - cursor));
                if (nested != null)
                {
                    return nested;
                }
            }

            cursor = end;
        }

        return null;
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
            case Span span:
            {
                var total = 0;
                foreach (var nested in span.Inlines)
                {
                    total += GetInlineLogicalLength(nested);
                }

                return total;
            }
            case InlineUIContainer:
                return 1;
            default:
                return 0;
        }
    }

    private static IEnumerable<Hyperlink> EnumerateHyperlinks(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    foreach (var hyperlink in EnumerateHyperlinks(paragraph.Inlines[i]))
                    {
                        yield return hyperlink;
                    }
                }

                yield break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    foreach (var hyperlink in EnumerateHyperlinks(section.Blocks[i]))
                    {
                        yield return hyperlink;
                    }
                }

                yield break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    for (var j = 0; j < list.Items[i].Blocks.Count; j++)
                    {
                        foreach (var hyperlink in EnumerateHyperlinks(list.Items[i].Blocks[j]))
                        {
                            yield return hyperlink;
                        }
                    }
                }

                yield break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var rowGroup = table.RowGroups[i];
                    for (var j = 0; j < rowGroup.Rows.Count; j++)
                    {
                        var row = rowGroup.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                foreach (var hyperlink in EnumerateHyperlinks(cell.Blocks[m]))
                                {
                                    yield return hyperlink;
                                }
                            }
                        }
                    }
                }

                yield break;
        }
    }

    private static IEnumerable<Hyperlink> EnumerateHyperlinks(Inline inline)
    {
        if (inline is Hyperlink hyperlink)
        {
            yield return hyperlink;
        }

        if (inline is not Span span)
        {
            yield break;
        }

        for (var i = 0; i < span.Inlines.Count; i++)
        {
            foreach (var nested in EnumerateHyperlinks(span.Inlines[i]))
            {
                yield return nested;
            }
        }
    }
}
