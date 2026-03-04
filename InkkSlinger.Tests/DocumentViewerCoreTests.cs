using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerCoreTests
{
    [Fact]
    public void DefaultDocument_IsNonNull_AndHasSingleParagraph()
    {
        var viewer = new DocumentViewer();

        Assert.NotNull(viewer.Document);
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(viewer.Document.Blocks));
        Assert.IsType<Run>(Assert.Single(paragraph.Inlines));
    }

    [Fact]
    public void Zoom_IsClampedByMinAndMax()
    {
        var viewer = CreateViewer();
        viewer.MinZoom = 50f;
        viewer.MaxZoom = 200f;

        viewer.Zoom = 10f;
        Assert.Equal(50f, viewer.Zoom);

        viewer.Zoom = 500f;
        Assert.Equal(200f, viewer.Zoom);
    }

    [Fact]
    public void MeasurePopulatesViewportAndExtent()
    {
        var viewer = CreateViewer();
        viewer.Document = BuildLongDocument(30);

        viewer.Measure(new Microsoft.Xna.Framework.Vector2(420f, 260f));

        Assert.True(viewer.ViewportWidth > 0f);
        Assert.True(viewer.ViewportHeight > 0f);
        Assert.True(viewer.ExtentHeight > 0f);
    }

    [Fact]
    public void MaxZoomChange_CoercesZoom_AndUpdatesCapabilitiesImmediately()
    {
        var viewer = CreateViewer();
        viewer.MinZoom = 50f;
        viewer.MaxZoom = 200f;
        viewer.Zoom = 150f;

        viewer.MaxZoom = 120f;

        Assert.Equal(120f, viewer.Zoom);
        Assert.False(viewer.CanIncreaseZoom);
        Assert.True(viewer.CanDecreaseZoom);
    }

    private static DocumentViewer CreateViewer()
    {
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 420f, 260f));
        return viewer;
    }

    private static FlowDocument BuildLongDocument(int lines)
    {
        var doc = new FlowDocument();
        for (var i = 0; i < lines; i++)
        {
            var p = new Paragraph();
            p.Inlines.Add(new Run($"Line {i:D2} lorem ipsum dolor sit amet"));
            doc.Blocks.Add(p);
        }

        return doc;
    }
}
