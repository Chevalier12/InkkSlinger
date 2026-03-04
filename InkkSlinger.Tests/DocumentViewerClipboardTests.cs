using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerClipboardTests
{
    [Fact]
    public void CopySelection_WritesPlainAndRichClipboardPayload()
    {
        TextClipboard.ResetForTests();
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 360f, 120f));

        var doc = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run("copy target"));
        doc.Blocks.Add(p);
        viewer.Document = doc;

        viewer.SelectAll();
        viewer.Copy();

        Assert.True(TextClipboard.TryGetText(out var text));
        Assert.Equal("copy target", text);
        Assert.True(TextClipboard.TryGetData<string>(FlowDocumentSerializer.ClipboardFormat, out var rich));
        Assert.Contains("FlowDocument", rich);
    }
}
