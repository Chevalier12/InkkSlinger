using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal readonly record struct DocumentPageEntry(
    int PageNumber,
    int StartOffset,
    int EndOffset,
    float Top,
    float Bottom);

internal sealed class DocumentPageMap
{
    public static readonly DocumentPageMap Empty = new(Array.Empty<DocumentPageEntry>());

    public DocumentPageMap(IReadOnlyList<DocumentPageEntry> pages)
    {
        Pages = pages;
    }

    public IReadOnlyList<DocumentPageEntry> Pages { get; }

    public int PageCount => Pages.Count;

    public int ResolveCurrentPageNumber(float verticalOffset)
    {
        if (Pages.Count == 0)
        {
            return 0;
        }

        var y = Math.Max(0f, verticalOffset);
        for (var i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            if (y >= page.Top && y < page.Bottom)
            {
                return page.PageNumber;
            }
        }

        return Pages[Pages.Count - 1].PageNumber;
    }

    public bool TryGetPage(int pageNumber, out DocumentPageEntry page)
    {
        if (pageNumber <= 0 || pageNumber > Pages.Count)
        {
            page = default;
            return false;
        }

        page = Pages[pageNumber - 1];
        return true;
    }

    public static DocumentPageMap Build(DocumentLayoutResult layout, float viewportHeight)
    {
        if (layout.Lines.Count == 0 || viewportHeight <= 0f || float.IsNaN(viewportHeight) || float.IsInfinity(viewportHeight))
        {
            return Empty;
        }

        var pages = new List<DocumentPageEntry>();
        var pageNumber = 1;
        var pageStartY = 0f;
        var pageEndY = viewportHeight;
        var pageStartOffset = 0;
        var lastLineEndOffset = 0;

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var lineTop = line.Bounds.Y;
            var lineBottom = line.Bounds.Y + line.Bounds.Height;
            var lineEndOffset = line.StartOffset + line.Length;

            // Start a new page if the line starts after the current page frame and
            // we already have at least one line committed for the current page.
            if (lineTop >= pageEndY && lastLineEndOffset > pageStartOffset)
            {
                pages.Add(new DocumentPageEntry(
                    pageNumber,
                    pageStartOffset,
                    lastLineEndOffset,
                    pageStartY,
                    pageEndY));

                pageNumber++;
                pageStartY = pageEndY;
                pageEndY = pageStartY + viewportHeight;
                pageStartOffset = line.StartOffset;
            }

            // Ensure very tall lines still advance page buckets.
            while (lineBottom > pageEndY && lastLineEndOffset > pageStartOffset)
            {
                pages.Add(new DocumentPageEntry(
                    pageNumber,
                    pageStartOffset,
                    lastLineEndOffset,
                    pageStartY,
                    pageEndY));

                pageNumber++;
                pageStartY = pageEndY;
                pageEndY = pageStartY + viewportHeight;
                pageStartOffset = lastLineEndOffset;
            }

            lastLineEndOffset = Math.Max(lastLineEndOffset, lineEndOffset);
        }

        pages.Add(new DocumentPageEntry(
            pageNumber,
            pageStartOffset,
            Math.Max(pageStartOffset, lastLineEndOffset),
            pageStartY,
            pageEndY));

        return new DocumentPageMap(pages);
    }
}
