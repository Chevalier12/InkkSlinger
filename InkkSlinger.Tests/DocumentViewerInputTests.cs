using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerInputTests
{
    [Fact]
    public void HandleTextInputFromInput_IsAlwaysFalse()
    {
        var viewer = CreateViewer();
        viewer.SetFocusedFromInput(true);

        Assert.False(viewer.HandleTextInputFromInput('x'));
    }

    [Fact]
    public void PointerDrag_ProducesSelection()
    {
        var viewer = CreateViewer();
        viewer.Document = BuildSingleParagraph("alpha beta gamma");
        viewer.SetFocusedFromInput(true);

        var from = new Vector2(24f, 16f);
        var to = new Vector2(120f, 16f);

        Assert.True(viewer.HandlePointerDownFromInput(from, extendSelection: false));
        Assert.True(viewer.HandlePointerMoveFromInput(to));
        Assert.True(viewer.HandlePointerUpFromInput());
        Assert.True(viewer.SelectionLength > 0);
    }

    [Fact]
    public void CtrlA_SelectAll_Works()
    {
        var viewer = CreateViewer();
        viewer.Document = BuildSingleParagraph("alpha beta gamma");
        viewer.SetFocusedFromInput(true);

        Assert.True(viewer.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        Assert.Equal(DocumentEditing.GetText(viewer.Document).Length, viewer.SelectionLength);
    }

    [Fact]
    public void MouseWheel_ScrollsContent()
    {
        var viewer = CreateViewer();
        viewer.Document = BuildLongDocument(30);

        var before = viewer.VerticalOffset;
        Assert.True(viewer.HandleMouseWheelFromInput(-120));
        Assert.True(viewer.VerticalOffset >= before);
    }

    private static DocumentViewer CreateViewer()
    {
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 90f));
        viewer.Measure(new Vector2(320f, 90f));
        return viewer;
    }

    private static FlowDocument BuildSingleParagraph(string text)
    {
        var doc = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run(text));
        doc.Blocks.Add(p);
        return doc;
    }

    private static FlowDocument BuildLongDocument(int lines)
    {
        var doc = new FlowDocument();
        for (var i = 0; i < lines; i++)
        {
            var p = new Paragraph();
            p.Inlines.Add(new Run($"Line {i:D2} long text for scrolling"));
            doc.Blocks.Add(p);
        }

        return doc;
    }
}
