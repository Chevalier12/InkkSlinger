using Xunit;

namespace InkkSlinger.Tests;

public class RichTextDocumentTests
{
    [Fact]
    public void FlowDocument_Collections_AssignAndClearParentLinks()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        var run = new Run("abc");

        paragraph.Inlines.Add(run);
        document.Blocks.Add(paragraph);

        Assert.Same(paragraph, run.Parent);
        Assert.Same(document, paragraph.Parent);

        paragraph.Inlines.Clear();
        Assert.Null(run.Parent);
    }

    [Fact]
    public void FlowDocumentSerializer_RoundTripsCoreStructure()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        var bold = new Bold();
        bold.Inlines.Add(new Run("Hello"));
        paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Run("World"));
        document.Blocks.Add(paragraph);

        var xml = FlowDocumentSerializer.Serialize(document);
        var restored = FlowDocumentSerializer.Deserialize(xml);
        var text = FlowDocumentPlainText.GetText(restored);

        Assert.Contains("Hello", text);
        Assert.Contains("World", text);
    }

    [Fact]
    public void DocumentUndoManager_UndoRedo_TracksTextReplacement()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "alpha");
        var undo = new DocumentUndoManager();

        DocumentEditing.ReplaceAllText(document, "beta", undo);
        Assert.Equal("beta", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal("alpha", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Redo());
        Assert.Equal("beta", FlowDocumentPlainText.GetText(document));
    }

    [Fact]
    public void DocumentUndoManager_UndoRestoresRichStructure()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        var bold = new Bold();
        bold.Inlines.Add(new Run("Hello"));
        paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new Run(" world"));
        document.Blocks.Add(paragraph);
        var undo = new DocumentUndoManager();

        DocumentEditing.ReplaceAllText(document, "flattened", undo);

        Assert.True(undo.Undo());
        var restoredParagraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
        Assert.IsType<Bold>(restoredParagraph.Inlines[0]);
        Assert.Equal("Hello world", FlowDocumentPlainText.GetText(document));
    }

    [Fact]
    public void FlowDocumentPlainText_GetText_IncludesNestedBlocks()
    {
        var document = new FlowDocument();
        var section = new Section();
        var sectionParagraph = new Paragraph();
        sectionParagraph.Inlines.Add(new Run("Section text"));
        section.Blocks.Add(sectionParagraph);
        document.Blocks.Add(section);

        var list = new List();
        var item = new ListItem();
        var itemParagraph = new Paragraph();
        itemParagraph.Inlines.Add(new Run("List text"));
        item.Blocks.Add(itemParagraph);
        list.Items.Add(item);
        document.Blocks.Add(list);

        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        var cell = new TableCell();
        var cellParagraph = new Paragraph();
        cellParagraph.Inlines.Add(new Run("Cell text"));
        cell.Blocks.Add(cellParagraph);
        row.Cells.Add(cell);
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        document.Blocks.Add(table);

        var plainText = FlowDocumentPlainText.GetText(document);

        Assert.Equal(
            string.Join(System.Environment.NewLine, "Section text", "List text", "Cell text"),
            plainText);
    }

    [Fact]
    public void RichTextBox_TextInput_AndBackspace_EditDocumentText()
    {
        var richTextBox = new RichTextBox();
        richTextBox.SetFocusedFromInput(true);

        Assert.True(richTextBox.HandleTextInputFromInput('a'));
        Assert.True(richTextBox.HandleTextInputFromInput('b'));
        Assert.True(richTextBox.HandleTextInputFromInput('c'));
        Assert.Equal("abc", FlowDocumentPlainText.GetText(richTextBox.Document));

        Assert.True(richTextBox.HandleKeyDownFromInput(Microsoft.Xna.Framework.Input.Keys.Back, ModifierKeys.None));
        Assert.Equal("ab", FlowDocumentPlainText.GetText(richTextBox.Document));
    }

    [Fact]
    public void XamlLoader_CanBuildRichTextBoxWithDocument()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RichTextBox x:Name="Editor">
      <RichTextBox.Document>
        <FlowDocument>
          <FlowDocument.Blocks>
            <Paragraph>
              <Paragraph.Inlines>
                <Run Text="Hello" />
                <Bold>
                  <Span.Inlines>
                    <Run Text=" Rich" />
                  </Span.Inlines>
                </Bold>
              </Paragraph.Inlines>
            </Paragraph>
          </FlowDocument.Blocks>
        </FlowDocument>
      </RichTextBox.Document>
    </RichTextBox>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xml);
        var editor = (RichTextBox?)root.FindName("Editor");

        Assert.NotNull(editor);
        Assert.Equal("Hello Rich", FlowDocumentPlainText.GetText(editor!.Document));
    }
}
