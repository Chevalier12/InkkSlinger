using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextLayoutTests
{
    [Fact]
    public void StyleRunSegmentation_IsCorrect()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("A"));
        var bold = new Bold();
        bold.Inlines.Add(new Run("B"));
        paragraph.Inlines.Add(bold);
        var underline = new Underline();
        underline.Inlines.Add(new Run("C"));
        paragraph.Inlines.Add(underline);
        document.Blocks.Add(paragraph);

        var layout = Layout(document, 800f);
        var textRuns = layout.Runs.Where(r => !r.IsListMarker && r.Length > 0).ToArray();

        Assert.Contains(textRuns, r => !r.Style.IsBold && !r.Style.IsUnderline);
        Assert.Contains(textRuns, r => r.Style.IsBold);
        Assert.Contains(textRuns, r => r.Style.IsUnderline);
    }

    [Fact]
    public void ListMarkerPositions_AreStable()
    {
        var document = new FlowDocument();
        var list = new InkkSlinger.List { IsOrdered = true };
        list.Items.Add(BuildItem("One"));
        list.Items.Add(BuildItem("Two"));
        document.Blocks.Add(list);

        var first = Layout(document, 800f);
        var second = Layout(document, 800f);

        var firstMarkers = first.Runs.Where(r => r.IsListMarker).ToArray();
        var secondMarkers = second.Runs.Where(r => r.IsListMarker).ToArray();

        Assert.Equal(firstMarkers.Length, secondMarkers.Length);
        Assert.True(firstMarkers.Length >= 2);
        for (var i = 0; i < firstMarkers.Length; i++)
        {
            Assert.Equal(firstMarkers[i].Text, secondMarkers[i].Text);
            Assert.Equal(firstMarkers[i].Bounds.X, secondMarkers[i].Bounds.X);
            Assert.Equal(firstMarkers[i].Bounds.Y, secondMarkers[i].Bounds.Y);
        }
    }

    [Fact]
    public void TableSpanLayout_IsDeterministic()
    {
        var document = new FlowDocument();
        var table = new Table();
        var group = new TableRowGroup();

        var row1 = new TableRow();
        var wide = new TableCell { ColumnSpan = 2 };
        var p1 = new Paragraph();
        p1.Inlines.Add(new Run("Wide"));
        wide.Blocks.Add(p1);
        row1.Cells.Add(wide);
        group.Rows.Add(row1);

        var row2 = new TableRow();
        var left = new TableCell();
        var leftP = new Paragraph();
        leftP.Inlines.Add(new Run("L"));
        left.Blocks.Add(leftP);
        var right = new TableCell();
        var rightP = new Paragraph();
        rightP.Inlines.Add(new Run("R"));
        right.Blocks.Add(rightP);
        row2.Cells.Add(left);
        row2.Cells.Add(right);
        group.Rows.Add(row2);

        table.RowGroups.Add(group);
        document.Blocks.Add(table);

        var first = Layout(document, 400f);
        var second = Layout(document, 400f);

        Assert.Equal(first.TableCellBounds.Count, second.TableCellBounds.Count);
        Assert.True(first.TableCellBounds.Count >= 3);
        for (var i = 0; i < first.TableCellBounds.Count; i++)
        {
            Assert.Equal(first.TableCellBounds[i].Width, second.TableCellBounds[i].Width);
            Assert.Equal(first.TableCellBounds[i].Height, second.TableCellBounds[i].Height);
        }
    }

    [Fact]
    public void CaretGeometry_MapsOffsets()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("abc"));
        document.Blocks.Add(paragraph);

        var layout = Layout(document, 800f);
        Assert.True(layout.TryGetCaretPosition(0, out var start));
        Assert.True(layout.TryGetCaretPosition(3, out var end));
        Assert.True(end.X >= start.X);
        Assert.Equal(start.Y, end.Y);
    }

    [Fact]
    public void SelectionRects_SpanMultipleLinesAndRuns()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("hello "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("world"));
        paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new Run(" again"));
        document.Blocks.Add(paragraph);

        var layout = Layout(document, 45f);
        var rects = layout.BuildSelectionRects(1, 10);

        Assert.True(rects.Count >= 2);
        Assert.All(rects, rect => Assert.True(rect.Width > 0f));
    }

    private static ListItem BuildItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static DocumentLayoutResult Layout(FlowDocument document, float width)
    {
        var engine = new DocumentLayoutEngine();
        var settings = new DocumentLayoutSettings(
            AvailableWidth: width,
            Font: null,
            Wrapping: TextWrapping.Wrap,
            Foreground: Color.White,
            LineHeight: Math.Max(1f, FontStashTextRenderer.GetLineHeight(null)),
            ListIndent: 16f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f);
        return engine.Layout(document, settings);
    }
}
