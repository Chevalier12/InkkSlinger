using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextAdvancedStructureTests
{
    [Fact]
    public void NestedListTransforms_AreStableAcrossIndentAndOutdent()
    {
        var editor = CreateEditor("one\ntwo");

        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.IncreaseListLevel, null, editor);

        var list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, list.Items.Count);

        Assert.True(editor.HandlePointerDownFromInput(new Vector2(12f, 42f), extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        CommandManager.Execute(EditingCommands.IncreaseListLevel, null, editor);
        list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        var nested = Assert.IsType<InkkSlinger.List>(list.Items[0].Blocks[1]);
        Assert.Single(nested.Items);

        CommandManager.Execute(EditingCommands.DecreaseListLevel, null, editor);
        Assert.Equal(2, editor.Document.Blocks.Count);
        list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Single(list.Items);
        Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        var text = DocumentEditing.GetText(editor.Document);
        Assert.Contains("one", text);
        Assert.Contains("two", text);
    }

    [Fact]
    public void TableSplitMergeAndTabNavigation_WorkDeterministically()
    {
        var editor = CreateEditor(string.Empty);
        CommandManager.Execute(EditingCommands.InsertTable, null, editor);

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, Assert.Single(table.RowGroups).Rows[0].Cells.Count);

        var initialCaret = editor.CaretIndex;
        CommandManager.Execute(EditingCommands.TabForward, null, editor);
        Assert.True(editor.CaretIndex > initialCaret);
        CommandManager.Execute(EditingCommands.TabBackward, null, editor);
        Assert.Equal(initialCaret, editor.CaretIndex);

        CommandManager.Execute(EditingCommands.SplitCell, null, editor);
        table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, Assert.Single(table.RowGroups).Rows[0].Cells.Count);
        CommandManager.Execute(EditingCommands.MergeCells, null, editor);
        table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, Assert.Single(table.RowGroups).Rows[0].Cells.Count);
    }

    [Fact]
    public void HyperlinkActivation_RoutesForKeyboardAndPointer()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildHyperlinkDocument("https://example.com", "site");

        var hitCount = 0;
        var lastUri = string.Empty;
        editor.HyperlinkNavigate += (_, args) =>
        {
            hitCount++;
            lastUri = args.NavigateUri;
        };

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.Control));

        Assert.True(editor.HandlePointerDownFromInput(new Vector2(10f, 10f), extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());

        Assert.True(hitCount >= 2);
        Assert.Equal("https://example.com", lastUri);
    }

    [Fact]
    public void InlineContainer_BoundariesArePreservedInCopySelection()
    {
        TextClipboard.ResetForTests();
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildInlineContainerDocument();

        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, editor);

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal($"A\uFFFCB", copied);
        Assert.True(TextClipboard.TryGetData<string>(FlowDocumentSerializer.ClipboardFormat, out var richXml));
        Assert.Contains(nameof(FlowDocument), richXml);
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 280f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static FlowDocument BuildHyperlinkDocument(string uri, string text)
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        var hyperlink = new Hyperlink
        {
            NavigateUri = uri
        };
        hyperlink.Inlines.Add(new Run(text));
        paragraph.Inlines.Add(hyperlink);
        document.Blocks.Add(paragraph);
        return document;
    }

    private static FlowDocument BuildInlineContainerDocument()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("A"));
        paragraph.Inlines.Add(new InlineUIContainer());
        paragraph.Inlines.Add(new Run("B"));
        document.Blocks.Add(paragraph);
        return document;
    }
}
