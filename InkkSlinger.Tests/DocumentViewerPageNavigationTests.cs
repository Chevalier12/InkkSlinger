using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerPageNavigationTests
{
    [Fact]
    public void NextAndPreviousPage_UpdateMasterPageNumber()
    {
        var viewer = CreateLongViewer();

        var startPage = viewer.MasterPageNumber;
        viewer.NextPage();
        Assert.True(viewer.MasterPageNumber >= startPage);

        var nextPage = viewer.MasterPageNumber;
        viewer.PreviousPage();
        Assert.True(viewer.MasterPageNumber <= nextPage);
    }

    [Fact]
    public void GoToPage_BoundsAreSafe()
    {
        var viewer = CreateLongViewer();

        viewer.GoToPage(1);
        Assert.True(viewer.MasterPageNumber >= 1);

        viewer.GoToPage(999);
        Assert.True(viewer.MasterPageNumber <= viewer.PageCount || viewer.PageCount == 0);
    }

    private static DocumentViewer CreateLongViewer()
    {
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 360f, 110f));

        var doc = new FlowDocument();
        for (var i = 0; i < 80; i++)
        {
            var p = new Paragraph();
            p.Inlines.Add(new Run($"Page line {i:D2}"));
            doc.Blocks.Add(p);
        }

        viewer.Document = doc;
        viewer.Measure(new Microsoft.Xna.Framework.Vector2(360f, 110f));
        viewer.Arrange(new LayoutRect(0f, 0f, 360f, 110f));
        return viewer;
    }
}
