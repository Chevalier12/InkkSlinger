using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextXamlParserTests
{
    [Fact]
    public void RichDocumentXaml_WithNestedFormattingListsAndTables_Loads()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox x:Name="Editor">
    <RichTextBox.Document>
      <FlowDocument>
        <Paragraph>
          <Run Text="Intro " />
          <Bold>
            <Run Text="bold" />
          </Bold>
          <Hyperlink NavigateUri="https://example.com">
            <Run Text=" link" />
          </Hyperlink>
        </Paragraph>
        <List IsOrdered="True">
          <ListItem>
            <Paragraph>
              <Run Text="Item 1" />
            </Paragraph>
          </ListItem>
        </List>
        <Table>
          <TableRowGroup>
            <TableRow>
              <TableCell ColumnSpan="2">
                <Paragraph>
                  <Run Text="Cell A" />
                </Paragraph>
              </TableCell>
            </TableRow>
          </TableRowGroup>
        </Table>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xml);
        var editor = (RichTextBox?)root.FindName("Editor");
        Assert.NotNull(editor);

        Assert.Equal(
            string.Join(System.Environment.NewLine, "Intro bold link", "Item 1", "Cell A"),
            FlowDocumentPlainText.GetText(editor!.Document));
    }

    [Fact]
    public void InvalidParagraphChildStructure_ThrowsWithElementAndLineInfo()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox>
    <RichTextBox.Document>
      <FlowDocument>
        <Paragraph>
          <Section />
        </Paragraph>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xml));
        Assert.Contains("Section", ex.Message);
        Assert.Contains("Paragraph", ex.Message);
        Assert.Contains("Line", ex.Message);
        Assert.Contains("Position", ex.Message);
    }

    [Fact]
    public void LineBreakWithUnsupportedAttribute_ThrowsWithAttributeAndLineInfo()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox>
    <RichTextBox.Document>
      <FlowDocument>
        <Paragraph>
          <LineBreak Text="bad" />
        </Paragraph>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xml));
        Assert.Contains("LineBreak", ex.Message);
        Assert.Contains("Text", ex.Message);
        Assert.Contains("Line", ex.Message);
        Assert.Contains("Position", ex.Message);
    }

    [Fact]
    public void TableCellWithNonPositiveSpan_ThrowsWithAttributeAndLineInfo()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox>
    <RichTextBox.Document>
      <FlowDocument>
        <Table>
          <TableRowGroup>
            <TableRow>
              <TableCell ColumnSpan="0">
                <Paragraph><Run Text="x" /></Paragraph>
              </TableCell>
            </TableRow>
          </TableRowGroup>
        </Table>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xml));
        Assert.Contains("ColumnSpan", ex.Message);
        Assert.Contains("greater than zero", ex.Message);
        Assert.Contains("Line", ex.Message);
        Assert.Contains("Position", ex.Message);
    }

    [Fact]
    public void HyperlinkWithWhitespaceNavigateUri_ThrowsWithAttributeAndLineInfo()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox>
    <RichTextBox.Document>
      <FlowDocument>
        <Paragraph>
          <Hyperlink NavigateUri="   ">
            <Run Text="bad uri" />
          </Hyperlink>
        </Paragraph>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xml));
        Assert.Contains("Hyperlink", ex.Message);
        Assert.Contains("NavigateUri", ex.Message);
        Assert.Contains("Line", ex.Message);
        Assert.Contains("Position", ex.Message);
    }
}
