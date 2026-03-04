using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerXamlParserTests
{
    [Fact]
    public void DocumentViewer_WithFlowDocumentPropertyElement_Loads()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <DocumentViewer x:Name="Viewer" Zoom="120">
    <DocumentViewer.Document>
      <FlowDocument>
        <Paragraph>
          <Run Text="Hello Viewer" />
        </Paragraph>
      </FlowDocument>
    </DocumentViewer.Document>
  </DocumentViewer>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xml);
        var viewer = Assert.IsType<DocumentViewer>(root.FindName("Viewer"));

        Assert.Equal(120f, viewer.Zoom);
        Assert.Equal("Hello Viewer", DocumentEditing.GetText(viewer.Document));
    }
}
