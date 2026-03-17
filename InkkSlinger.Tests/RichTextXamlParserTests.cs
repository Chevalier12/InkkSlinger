using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextXamlParserTests
{
    [Fact]
    public void RichTextBoxXaml_WithParityProperties_LoadsConfiguredSurface()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox x:Name="Editor"
               TextWrapping="NoWrap"
               IsReadOnly="True"
               IsReadOnlyCaretVisible="True"
               IsSpellCheckEnabled="True"
               AcceptsReturn="False"
               AcceptsTab="False"
               IsUndoEnabled="False"
               UndoLimit="5"
               SelectionTextBrush="#101820"
               SelectionOpacity="0.25"
               IsInactiveSelectionHighlightEnabled="False"
               HorizontalScrollBarVisibility="Visible"
               VerticalScrollBarVisibility="Hidden">
    <RichTextBox.Document>
      <FlowDocument>
        <Paragraph>
          <Run Text="Configured editor" />
        </Paragraph>
      </FlowDocument>
    </RichTextBox.Document>
  </RichTextBox>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xml);
        var editor = (RichTextBox?)root.FindName("Editor");

        Assert.NotNull(editor);
        Assert.Equal(TextWrapping.NoWrap, editor!.TextWrapping);
        Assert.True(editor.IsReadOnly);
        Assert.True(editor.IsReadOnlyCaretVisible);
        Assert.True(editor.IsSpellCheckEnabled);
        Assert.False(editor.AcceptsReturn);
        Assert.False(editor.AcceptsTab);
        Assert.False(editor.IsUndoEnabled);
        Assert.Equal(5, editor.UndoLimit);
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x10, 0x18, 0x20), editor.SelectionTextBrush);
        Assert.Equal(0.25f, editor.SelectionOpacity);
        Assert.False(editor.IsInactiveSelectionHighlightEnabled);
        Assert.Equal(ScrollBarVisibility.Visible, editor.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Hidden, editor.VerticalScrollBarVisibility);
        Assert.Equal("Configured editor", FlowDocumentPlainText.GetText(editor.Document));
    }

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
