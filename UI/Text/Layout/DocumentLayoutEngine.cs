using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public readonly record struct DocumentLayoutStyle(bool IsBold, bool IsItalic, bool IsUnderline, bool IsHyperlink)
{
    public static readonly DocumentLayoutStyle Default = new(false, false, false, false);
}

public sealed class DocumentLayoutRun
{
    public required string Text { get; init; }

    public required LayoutRect Bounds { get; init; }

    public required int StartOffset { get; init; }

    public required int Length { get; init; }

    public required DocumentLayoutStyle Style { get; init; }

    public bool IsListMarker { get; init; }

    public bool IsTableContent { get; init; }
}

public sealed class DocumentLayoutLine
{
    public required int Index { get; init; }

    public required int StartOffset { get; init; }

    public required int Length { get; init; }

    public required string Text { get; init; }

    public required float TextStartX { get; init; }

    public required LayoutRect Bounds { get; init; }

    public required IReadOnlyList<DocumentLayoutRun> Runs { get; init; }

    public required float[] PrefixWidths { get; init; }
}

public sealed class DocumentLayoutBlockMetrics
{
    public required string BlockPath { get; init; }

    public required LayoutRect Bounds { get; init; }

    public bool IsListItem { get; init; }

    public bool IsTableCell { get; init; }
}

public sealed class DocumentLayoutResult
{
    private readonly Dictionary<int, Vector2> _caretPositions;

    public DocumentLayoutResult(
        IReadOnlyList<DocumentLayoutLine> lines,
        IReadOnlyList<DocumentLayoutRun> runs,
        IReadOnlyList<DocumentLayoutBlockMetrics> blocks,
        IReadOnlyList<LayoutRect> tableCellBounds,
        Dictionary<int, Vector2> caretPositions,
        float contentWidth,
        float contentHeight,
        int textLength)
    {
        Lines = lines;
        Runs = runs;
        Blocks = blocks;
        TableCellBounds = tableCellBounds;
        _caretPositions = caretPositions;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        TextLength = textLength;
    }

    public static DocumentLayoutResult Empty { get; } = new(
        Array.Empty<DocumentLayoutLine>(),
        Array.Empty<DocumentLayoutRun>(),
        Array.Empty<DocumentLayoutBlockMetrics>(),
        Array.Empty<LayoutRect>(),
        new Dictionary<int, Vector2> { [0] = Vector2.Zero },
        0f,
        0f,
        0);

    public IReadOnlyList<DocumentLayoutLine> Lines { get; }

    public IReadOnlyList<DocumentLayoutRun> Runs { get; }

    public IReadOnlyList<DocumentLayoutBlockMetrics> Blocks { get; }

    public IReadOnlyList<LayoutRect> TableCellBounds { get; }

    public float ContentWidth { get; }

    public float ContentHeight { get; }

    public int TextLength { get; }

    public bool TryGetCaretPosition(int offset, out Vector2 position)
    {
        var clamped = Math.Clamp(offset, 0, TextLength);
        if (_caretPositions.TryGetValue(clamped, out position))
        {
            return true;
        }

        if (_caretPositions.Count == 0)
        {
            position = Vector2.Zero;
            return false;
        }

        var nearestDistance = int.MaxValue;
        var nearest = Vector2.Zero;
        foreach (var entry in _caretPositions)
        {
            var delta = Math.Abs(entry.Key - clamped);
            if (delta >= nearestDistance)
            {
                continue;
            }

            nearestDistance = delta;
            nearest = entry.Value;
        }

        position = nearest;
        return true;
    }

    public int HitTestOffset(Vector2 point)
    {
        if (Lines.Count == 0)
        {
            return 0;
        }

        var line = ResolveLine(point.Y);
        var localX = Math.Max(0f, point.X - line.TextStartX);
        var column = ResolveColumn(line, localX);
        return Math.Clamp(line.StartOffset + column, 0, TextLength);
    }

    public IReadOnlyList<LayoutRect> BuildSelectionRects(int start, int length)
    {
        if (length <= 0 || Lines.Count == 0)
        {
            return Array.Empty<LayoutRect>();
        }

        var selectionStart = Math.Max(0, start);
        var selectionEnd = Math.Min(TextLength, selectionStart + length);
        if (selectionEnd <= selectionStart)
        {
            return Array.Empty<LayoutRect>();
        }

        var rects = new List<LayoutRect>();
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var lineStart = line.StartOffset;
            var lineEnd = lineStart + line.Length;
            if (selectionEnd <= lineStart || selectionStart >= lineEnd)
            {
                continue;
            }

            var localStart = Math.Max(0, selectionStart - lineStart);
            var localEnd = Math.Min(line.Length, selectionEnd - lineStart);
            var left = line.TextStartX + line.PrefixWidths[localStart];
            var right = line.TextStartX + line.PrefixWidths[localEnd];
            rects.Add(new LayoutRect(left, line.Bounds.Y, Math.Max(1f, right - left), line.Bounds.Height));
        }

        return rects;
    }

    private DocumentLayoutLine ResolveLine(float y)
    {
        if (y <= Lines[0].Bounds.Y)
        {
            return Lines[0];
        }

        var last = Lines[Lines.Count - 1];
        if (y >= last.Bounds.Y + last.Bounds.Height)
        {
            return last;
        }

        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var bottom = line.Bounds.Y + line.Bounds.Height;
            if (y >= line.Bounds.Y && y < bottom)
            {
                return line;
            }
        }

        return last;
    }

    private static int ResolveColumn(DocumentLayoutLine line, float localX)
    {
        for (var i = 0; i < line.PrefixWidths.Length - 1; i++)
        {
            var left = line.PrefixWidths[i];
            var right = line.PrefixWidths[i + 1];
            var mid = left + ((right - left) * 0.5f);
            if (localX <= mid)
            {
                return i;
            }
        }

        return line.Length;
    }
}

public readonly record struct DocumentLayoutSettings(
    float AvailableWidth,
    SpriteFont? Font,
    TextWrapping Wrapping,
    Color Foreground,
    float LineHeight,
    float ListIndent,
    float ListMarkerGap,
    float TableCellPadding,
    float TableBorderThickness);
public sealed class DocumentLayoutEngine
{
    public DocumentLayoutResult Layout(FlowDocument document, in DocumentLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new Builder(document, settings);
        return builder.Build();
    }

    private sealed class Builder
    {
        private readonly FlowDocument _document;
        private readonly DocumentLayoutSettings _settings;
        private readonly List<DocumentLayoutLine> _lines = [];
        private readonly List<DocumentLayoutRun> _runs = [];
        private readonly List<DocumentLayoutBlockMetrics> _blocks = [];
        private readonly List<LayoutRect> _tableCellBounds = [];
        private readonly Dictionary<int, Vector2> _caretPositions = [];
        private readonly int _paragraphCount;
        private int _paragraphIndex;
        private int _lineIndex;
        private int _offset;
        private float _cursorY;
        private float _contentWidth;

        public Builder(FlowDocument document, in DocumentLayoutSettings settings)
        {
            _document = document;
            _settings = settings;
            _paragraphCount = CountParagraphs(document);
            _caretPositions[0] = Vector2.Zero;
        }

        public DocumentLayoutResult Build()
        {
            LayoutBlocks(_document.Blocks, 0f, 0, "FlowDocument", isTableCell: false, isListItem: false);
            var contentHeight = _lines.Count == 0 ? _settings.LineHeight : _cursorY;
            return new DocumentLayoutResult(
                _lines,
                _runs,
                _blocks,
                _tableCellBounds,
                _caretPositions,
                _contentWidth,
                contentHeight,
                _offset);
        }

        private void LayoutBlocks(
            IEnumerable<Block> blocks,
            float baseX,
            int listDepth,
            string path,
            bool isTableCell,
            bool isListItem)
        {
            var index = 0;
            foreach (var block in blocks)
            {
                var blockPath = $"{path}/{block.GetType().Name}[{index}]";
                switch (block)
                {
                    case Paragraph paragraph:
                        LayoutParagraph(paragraph, blockPath, baseX, listDepth, markerText: null, isTableCell, isListItem, fixedY: null, advanceCursor: true);
                        break;
                    case Section section:
                        LayoutBlocks(section.Blocks, baseX, listDepth, blockPath, isTableCell, isListItem);
                        break;
                    case List list:
                        LayoutList(list, blockPath, baseX, listDepth);
                        break;
                    case Table table:
                        LayoutTable(table, blockPath, baseX, listDepth);
                        break;
                    case BlockUIContainer:
                        var placeholder = new Paragraph();
                        placeholder.Inlines.Add(new Run("\uFFFC"));
                        LayoutParagraph(placeholder, blockPath, baseX, listDepth, markerText: null, isTableCell, isListItem, fixedY: null, advanceCursor: true);
                        break;
                }

                index++;
            }
        }

        private void LayoutList(List list, string path, float baseX, int listDepth)
        {
            var itemIndex = 0;
            foreach (var item in list.Items)
            {
                var marker = list.IsOrdered ? $"{itemIndex + 1}." : "•";
                var itemPath = $"{path}/ListItem[{itemIndex}]";
                var firstParagraph = true;
                var blockIndex = 0;
                foreach (var block in item.Blocks)
                {
                    var blockPath = $"{itemPath}/{block.GetType().Name}[{blockIndex}]";
                    if (block is Paragraph paragraph)
                    {
                        LayoutParagraph(
                            paragraph,
                            blockPath,
                            baseX,
                            listDepth + 1,
                            firstParagraph ? marker : null,
                            isTableCell: false,
                            isListItem: true,
                            fixedY: null,
                            advanceCursor: true);
                        firstParagraph = false;
                    }
                    else if (block is List nested)
                    {
                        LayoutList(nested, blockPath, baseX, listDepth + 1);
                    }
                    else if (block is Section section)
                    {
                        LayoutBlocks(section.Blocks, baseX, listDepth + 1, blockPath, isTableCell: false, isListItem: true);
                    }
                    else if (block is Table table)
                    {
                        LayoutTable(table, blockPath, baseX + ((listDepth + 1) * _settings.ListIndent), listDepth + 1);
                    }

                    blockIndex++;
                }

                itemIndex++;
            }
        }

        private void LayoutTable(Table table, string path, float baseX, int listDepth)
        {
            var placements = BuildTablePlacements(table);
            if (placements.Count == 0)
            {
                return;
            }

            var availableWidth = ResolveMaxWidth(baseX, listDepth);
            var columnCount = 0;
            foreach (var placement in placements)
            {
                columnCount = Math.Max(columnCount, placement.StartColumn + placement.ColumnSpan);
            }

            if (columnCount <= 0)
            {
                return;
            }

            var columnWidths = BuildColumnWidths(placements, columnCount, availableWidth);
            var rowHeights = BuildRowHeights(placements, columnWidths);
            var rowY = BuildRowOffsets(rowHeights, _cursorY);
            var columnX = BuildColumnOffsets(columnWidths, baseX + (listDepth * _settings.ListIndent));

            foreach (var placement in placements)
            {
                var cellX = columnX[placement.StartColumn];
                var cellY = rowY[placement.RowIndex];
                var cellWidth = GetSpanWidth(columnWidths, placement.StartColumn, placement.ColumnSpan);
                var cellHeight = GetSpanHeight(rowHeights, placement.RowIndex, placement.RowSpan);
                var cellRect = new LayoutRect(cellX, cellY, cellWidth, cellHeight);
                _tableCellBounds.Add(cellRect);

                var cellPath = $"{path}/Row[{placement.RowIndex}]/Cell[{placement.CellIndex}]";
                var paragraphOffsetY = cellY + _settings.TableCellPadding;
                var paragraphIndex = 0;
                foreach (var block in placement.Cell.Blocks)
                {
                    if (block is not Paragraph paragraph)
                    {
                        paragraphIndex++;
                        continue;
                    }

                    var paragraphPath = $"{cellPath}/Paragraph[{paragraphIndex}]";
                    var used = LayoutParagraph(
                        paragraph,
                        paragraphPath,
                        cellX + _settings.TableCellPadding,
                        listDepth,
                        markerText: null,
                        isTableCell: true,
                        isListItem: false,
                        fixedY: paragraphOffsetY,
                        advanceCursor: false,
                        maxWidthOverride: Math.Max(1f, cellWidth - (_settings.TableCellPadding * 2f)));
                    paragraphOffsetY += used;
                    paragraphIndex++;
                }

                _blocks.Add(
                    new DocumentLayoutBlockMetrics
                    {
                        BlockPath = cellPath,
                        Bounds = cellRect,
                        IsListItem = false,
                        IsTableCell = true
                    });
            }

            var tableHeight = 0f;
            for (var i = 0; i < rowHeights.Length; i++)
            {
                tableHeight += rowHeights[i];
            }

            _cursorY += tableHeight + (_settings.LineHeight * 0.2f);
            _contentWidth = Math.Max(_contentWidth, (columnX[columnX.Length - 1] - columnX[0]));
        }

        private float LayoutParagraph(
            Paragraph paragraph,
            string path,
            float baseX,
            int listDepth,
            string? markerText,
            bool isTableCell,
            bool isListItem,
            float? fixedY,
            bool advanceCursor,
            float? maxWidthOverride = null)
        {
            var marker = string.IsNullOrWhiteSpace(markerText) ? string.Empty : markerText.TrimEnd() + " ";
            var markerX = baseX + (listDepth * _settings.ListIndent);
            var markerWidth = marker.Length == 0 ? 0f : FontStashTextRenderer.MeasureWidth(_settings.Font, marker);
            var textStartX = markerX + (markerWidth > 0f ? markerWidth + _settings.ListMarkerGap : 0f);

            var segments = new List<StyledChar>();
            foreach (var inline in paragraph.Inlines)
            {
                AppendInlineStyledText(inline, DocumentLayoutStyle.Default, segments);
            }

            if (segments.Count == 0)
            {
                segments.Add(new StyledChar('\0', DocumentLayoutStyle.Default));
            }

            var plainText = BuildPlainText(segments);
            var width = maxWidthOverride ?? ResolveMaxWidth(baseX, listDepth, markerWidth + _settings.ListMarkerGap);
            if (_settings.Wrapping == TextWrapping.NoWrap)
            {
                width = float.PositiveInfinity;
            }

            var layout = TextLayout.Layout(plainText, _settings.Font, width, _settings.Wrapping);
            var y = fixedY ?? _cursorY;
            var blockTop = y;
            var blockBottom = y;
            var scanIndex = 0;
            for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
            {
                while (scanIndex < plainText.Length && plainText[scanIndex] == '\n')
                {
                    scanIndex++;
                }

                var lineText = layout.Lines[lineIndex];
                if (scanIndex + lineText.Length > plainText.Length)
                {
                    lineText = scanIndex < plainText.Length ? plainText[scanIndex..] : string.Empty;
                }

                var globalLineStart = _offset + scanIndex;
                var lineY = y + (lineIndex * _settings.LineHeight);
                var lineRuns = BuildLineRuns(segments, scanIndex, lineText.Length, textStartX, lineY, globalLineStart, isTableCell, _settings.LineHeight, _settings.Font);
                var prefixWidths = BuildPrefixWidths(_settings.Font, lineText);
                var lineWidth = prefixWidths[prefixWidths.Length - 1];
                var lineBounds = new LayoutRect(textStartX, lineY, Math.Max(lineWidth, markerWidth), _settings.LineHeight);
                for (var runIndex = 0; runIndex < lineRuns.Count; runIndex++)
                {
                    _runs.Add(lineRuns[runIndex]);
                }

                if (lineIndex == 0 && markerWidth > 0f)
                {
                    var markerRun = new DocumentLayoutRun
                    {
                        Text = marker,
                        Bounds = new LayoutRect(markerX, lineY, markerWidth, _settings.LineHeight),
                        StartOffset = globalLineStart,
                        Length = 0,
                        Style = DocumentLayoutStyle.Default,
                        IsListMarker = true,
                        IsTableContent = isTableCell
                    };
                    _runs.Add(markerRun);
                    lineRuns.Insert(0, markerRun);
                }

                for (var column = 0; column <= lineText.Length; column++)
                {
                    var point = new Vector2(textStartX + prefixWidths[column], lineY);
                    _caretPositions[globalLineStart + column] = point;
                }

                var builtLine = new DocumentLayoutLine
                {
                    Index = _lineIndex++,
                    StartOffset = globalLineStart,
                    Length = lineText.Length,
                    Text = lineText,
                    TextStartX = textStartX,
                    Bounds = lineBounds,
                    Runs = lineRuns,
                    PrefixWidths = prefixWidths
                };

                _lines.Add(builtLine);
                blockBottom = Math.Max(blockBottom, lineY + _settings.LineHeight);
                _contentWidth = Math.Max(_contentWidth, Math.Max(lineBounds.Width + lineBounds.X, markerX + markerWidth));
                scanIndex += lineText.Length;
            }

            var blockHeight = Math.Max(_settings.LineHeight, blockBottom - blockTop);
            _blocks.Add(
                new DocumentLayoutBlockMetrics
                {
                    BlockPath = path,
                    Bounds = new LayoutRect(markerX, blockTop, Math.Max(_contentWidth - markerX, 1f), blockHeight),
                    IsListItem = isListItem,
                    IsTableCell = isTableCell
                });

            _offset += plainText.Length;
            _paragraphIndex++;
            if (_paragraphIndex < _paragraphCount)
            {
                _caretPositions[_offset] = new Vector2(textStartX, blockTop + blockHeight);
                _offset++;
            }

            if (advanceCursor)
            {
                _cursorY += blockHeight;
            }

            return blockHeight;
        }
        private float ResolveMaxWidth(float baseX, int listDepth, float extraOffset = 0f)
        {
            if (float.IsInfinity(_settings.AvailableWidth) || _settings.AvailableWidth <= 0f)
            {
                return float.PositiveInfinity;
            }

            var left = baseX + (listDepth * _settings.ListIndent) + extraOffset;
            return Math.Max(1f, _settings.AvailableWidth - left);
        }

        private static List<DocumentLayoutRun> BuildLineRuns(
            IReadOnlyList<StyledChar> chars,
            int start,
            int length,
            float x,
            float y,
            int globalStartOffset,
            bool isTableCell,
            float lineHeight,
            SpriteFont? font)
        {
            var runs = new List<DocumentLayoutRun>();
            if (length <= 0)
            {
                return runs;
            }

            var cursor = x;
            var index = start;
            var end = start + length;
            while (index < end)
            {
                var style = chars[index].Style;
                var chunkStart = index;
                while (index < end && chars[index].Style.Equals(style))
                {
                    index++;
                }

                var text = BuildText(chars, chunkStart, index - chunkStart);
                var width = FontStashTextRenderer.MeasureWidth(font, text);
                var run = new DocumentLayoutRun
                {
                    Text = text,
                    Bounds = new LayoutRect(cursor, y, width, lineHeight),
                    StartOffset = globalStartOffset + (chunkStart - start),
                    Length = text.Length,
                    Style = style,
                    IsListMarker = false,
                    IsTableContent = isTableCell
                };
                runs.Add(run);
                cursor += width;
            }

            return runs;
        }

        private static float[] BuildPrefixWidths(SpriteFont? font, string text)
        {
            var widths = new float[text.Length + 1];
            widths[0] = 0f;
            var current = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                current += FontStashTextRenderer.MeasureWidth(font, text[i].ToString());
                widths[i + 1] = current;
            }

            return widths;
        }

        private static string BuildText(IReadOnlyList<StyledChar> chars, int start, int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new char[length];
            for (var i = 0; i < length; i++)
            {
                var value = chars[start + i].Character;
                buffer[i] = value == '\0' ? ' ' : value;
            }

            return new string(buffer);
        }

        private static string BuildPlainText(IReadOnlyList<StyledChar> chars)
        {
            if (chars.Count == 0)
            {
                return string.Empty;
            }

            var buffer = new char[chars.Count];
            for (var i = 0; i < chars.Count; i++)
            {
                var value = chars[i].Character;
                buffer[i] = value == '\0' ? ' ' : value;
            }

            return new string(buffer);
        }

        private static int CountParagraphs(FlowDocument document)
        {
            var count = 0;
            foreach (var _ in FlowDocumentPlainText.EnumerateParagraphs(document))
            {
                count++;
            }

            return Math.Max(1, count);
        }

        private static void AppendInlineStyledText(Inline inline, DocumentLayoutStyle style, List<StyledChar> buffer)
        {
            switch (inline)
            {
                case Run run:
                    if (run.Text.Length == 0)
                    {
                        return;
                    }

                    foreach (var character in run.Text)
                    {
                        buffer.Add(new StyledChar(character, style));
                    }

                    break;
                case LineBreak:
                    buffer.Add(new StyledChar('\n', style));
                    break;
                case Bold bold:
                    AppendSpanStyledText(bold, style with { IsBold = true }, buffer);
                    break;
                case Italic italic:
                    AppendSpanStyledText(italic, style with { IsItalic = true }, buffer);
                    break;
                case Underline underline:
                    AppendSpanStyledText(underline, style with { IsUnderline = true }, buffer);
                    break;
                case Hyperlink hyperlink:
                    AppendSpanStyledText(hyperlink, style with { IsHyperlink = true, IsUnderline = true }, buffer);
                    break;
                case Span span:
                    AppendSpanStyledText(span, style, buffer);
                    break;
                case InlineUIContainer:
                    buffer.Add(new StyledChar('\uFFFC', style));
                    break;
            }
        }

        private static void AppendSpanStyledText(Span span, DocumentLayoutStyle style, List<StyledChar> buffer)
        {
            foreach (var inline in span.Inlines)
            {
                AppendInlineStyledText(inline, style, buffer);
            }
        }

        private static List<TableCellPlacement> BuildTablePlacements(Table table)
        {
            var placements = new List<TableCellPlacement>();
            var openRowSpans = new Dictionary<int, int>();
            var globalRow = 0;
            foreach (var group in table.RowGroups)
            {
                foreach (var row in group.Rows)
                {
                    var keys = new List<int>(openRowSpans.Keys);
                    for (var i = 0; i < keys.Count; i++)
                    {
                        var key = keys[i];
                        openRowSpans[key] = Math.Max(0, openRowSpans[key] - 1);
                    }

                    var cellIndex = 0;
                    var column = 0;
                    foreach (var cell in row.Cells)
                    {
                        while (openRowSpans.TryGetValue(column, out var remaining) && remaining > 0)
                        {
                            column++;
                        }

                        var rowSpan = Math.Max(1, cell.RowSpan);
                        var columnSpan = Math.Max(1, cell.ColumnSpan);
                        placements.Add(new TableCellPlacement(globalRow, cellIndex, column, rowSpan, columnSpan, cell));
                        for (var span = 0; span < columnSpan; span++)
                        {
                            openRowSpans[column + span] = Math.Max(openRowSpans.GetValueOrDefault(column + span), rowSpan - 1);
                        }

                        column += columnSpan;
                        cellIndex++;
                    }

                    globalRow++;
                }
            }

            return placements;
        }

        private float[] BuildColumnWidths(IReadOnlyList<TableCellPlacement> placements, int columnCount, float availableWidth)
        {
            var widths = new float[columnCount];
            for (var i = 0; i < widths.Length; i++)
            {
                widths[i] = 48f;
            }

            foreach (var placement in placements)
            {
                var desired = MeasureCellDesiredWidth(placement.Cell) + (_settings.TableCellPadding * 2f);
                if (placement.ColumnSpan == 1)
                {
                    widths[placement.StartColumn] = Math.Max(widths[placement.StartColumn], desired);
                    continue;
                }

                var slice = Math.Max(1f, desired / placement.ColumnSpan);
                for (var col = placement.StartColumn; col < placement.StartColumn + placement.ColumnSpan; col++)
                {
                    widths[col] = Math.Max(widths[col], slice);
                }
            }

            if (!float.IsInfinity(availableWidth) && availableWidth > 0f)
            {
                var total = 0f;
                for (var i = 0; i < widths.Length; i++)
                {
                    total += widths[i];
                }

                if (total > availableWidth)
                {
                    var scale = availableWidth / total;
                    for (var i = 0; i < widths.Length; i++)
                    {
                        widths[i] = Math.Max(20f, widths[i] * scale);
                    }
                }
            }

            return widths;
        }

        private float[] BuildRowHeights(IReadOnlyList<TableCellPlacement> placements, float[] columnWidths)
        {
            var rowCount = 0;
            foreach (var placement in placements)
            {
                rowCount = Math.Max(rowCount, placement.RowIndex + placement.RowSpan);
            }

            var heights = new float[Math.Max(1, rowCount)];
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = _settings.LineHeight + (_settings.TableCellPadding * 2f);
            }

            foreach (var placement in placements)
            {
                var spanWidth = GetSpanWidth(columnWidths, placement.StartColumn, placement.ColumnSpan);
                var textWidth = Math.Max(1f, spanWidth - (_settings.TableCellPadding * 2f));
                var desired = MeasureCellDesiredHeight(placement.Cell, textWidth) + (_settings.TableCellPadding * 2f);
                var slice = desired / placement.RowSpan;
                for (var row = placement.RowIndex; row < placement.RowIndex + placement.RowSpan && row < heights.Length; row++)
                {
                    heights[row] = Math.Max(heights[row], slice);
                }
            }

            return heights;
        }

        private static float[] BuildRowOffsets(float[] rowHeights, float startY)
        {
            var offsets = new float[rowHeights.Length + 1];
            offsets[0] = startY;
            for (var i = 0; i < rowHeights.Length; i++)
            {
                offsets[i + 1] = offsets[i] + rowHeights[i];
            }

            return offsets;
        }

        private static float[] BuildColumnOffsets(float[] columnWidths, float startX)
        {
            var offsets = new float[columnWidths.Length + 1];
            offsets[0] = startX;
            for (var i = 0; i < columnWidths.Length; i++)
            {
                offsets[i + 1] = offsets[i] + columnWidths[i];
            }

            return offsets;
        }

        private static float GetSpanWidth(float[] widths, int start, int span)
        {
            var total = 0f;
            for (var i = start; i < start + span && i < widths.Length; i++)
            {
                total += widths[i];
            }

            return total;
        }

        private static float GetSpanHeight(float[] heights, int start, int span)
        {
            var total = 0f;
            for (var i = start; i < start + span && i < heights.Length; i++)
            {
                total += heights[i];
            }

            return total;
        }

        private float MeasureCellDesiredWidth(TableCell cell)
        {
            var maxWidth = 40f;
            foreach (var paragraph in FlowDocumentPlainTextExtensions.EnumerateParagraphs(cell))
            {
                var text = FlowDocumentPlainText.GetInlineText(paragraph.Inlines);
                maxWidth = Math.Max(maxWidth, FontStashTextRenderer.MeasureWidth(_settings.Font, text));
            }

            return maxWidth;
        }

        private float MeasureCellDesiredHeight(TableCell cell, float width)
        {
            var total = 0f;
            var any = false;
            foreach (var paragraph in FlowDocumentPlainTextExtensions.EnumerateParagraphs(cell))
            {
                var text = FlowDocumentPlainText.GetInlineText(paragraph.Inlines);
                var layout = TextLayout.Layout(text, _settings.Font, width, _settings.Wrapping);
                total += Math.Max(_settings.LineHeight, layout.Lines.Count * _settings.LineHeight);
                any = true;
            }

            if (!any)
            {
                total = _settings.LineHeight;
            }

            return total;
        }

        private readonly record struct StyledChar(char Character, DocumentLayoutStyle Style);

        private readonly record struct TableCellPlacement(int RowIndex, int CellIndex, int StartColumn, int RowSpan, int ColumnSpan, TableCell Cell);
    }
}

public sealed class DocumentViewportLayoutCache
{
    private CacheKey _key;
    private DocumentLayoutResult? _result;

    public bool TryGet(in CacheKey key, out DocumentLayoutResult result)
    {
        if (_result != null && _key.Equals(key))
        {
            result = _result;
            return true;
        }

        result = DocumentLayoutResult.Empty;
        return false;
    }

    public void Store(in CacheKey key, DocumentLayoutResult result)
    {
        _key = key;
        _result = result;
    }

    public void Invalidate()
    {
        _result = null;
    }

    public readonly record struct CacheKey(
        int DocumentSignature,
        float Width,
        TextWrapping Wrapping,
        int FontIdentity,
        float LineHeight,
        Color Foreground);
}

internal static class FlowDocumentPlainTextExtensions
{
    internal static IEnumerable<Paragraph> EnumerateParagraphs(TableCell cell)
    {
        foreach (var block in cell.Blocks)
        {
            foreach (var paragraph in EnumerateParagraphs(block))
            {
                yield return paragraph;
            }
        }
    }

    private static IEnumerable<Paragraph> EnumerateParagraphs(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                yield return paragraph;
                yield break;
            case Section section:
                foreach (var nested in section.Blocks)
                {
                    foreach (var paragraph in EnumerateParagraphs(nested))
                    {
                        yield return paragraph;
                    }
                }

                yield break;
            case List list:
                foreach (var item in list.Items)
                {
                    foreach (var nested in item.Blocks)
                    {
                        foreach (var paragraph in EnumerateParagraphs(nested))
                        {
                            yield return paragraph;
                        }
                    }
                }

                yield break;
            case Table table:
                foreach (var group in table.RowGroups)
                {
                    foreach (var row in group.Rows)
                    {
                        foreach (var cell in row.Cells)
                        {
                            foreach (var paragraph in EnumerateParagraphs(cell))
                            {
                                yield return paragraph;
                            }
                        }
                    }
                }

                yield break;
        }
    }
}

