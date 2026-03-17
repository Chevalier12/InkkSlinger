using System;
using System.Linq;
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
    public void HyperlinkActivation_RoutesForKeyboardAndReadOnlyPointer()
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

        editor.IsReadOnly = true;
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

    [Fact]
    public void InlineContainerChild_ShouldAttachToVisualTree_AndReceiveLayout()
    {
        var editor = CreateEditor(string.Empty);
        var button = new Button
        {
            Content = "Embedded",
            Width = 56f,
            Height = 24f
        };

        editor.Document = BuildInlineContainerDocument(button);

        Assert.Contains(button, editor.GetVisualChildren());
        Assert.Same(editor, button.VisualParent);
        Assert.True(button.LayoutSlot.Width >= 1f);
        Assert.True(button.LayoutSlot.Height >= 1f);
    }

    [Fact]
    public void BlockContainerChild_ShouldAttachToVisualTree_AndReceiveLayout()
    {
        var editor = CreateEditor(string.Empty);
        var button = new Button
        {
            Content = "Embedded Block",
            Width = 96f,
            Height = 28f
        };

        editor.Document = BuildBlockContainerDocument(button);

        Assert.Contains(button, editor.GetVisualChildren());
        Assert.Same(editor, button.VisualParent);
        Assert.True(button.LayoutSlot.Width >= 1f);
        Assert.True(button.LayoutSlot.Height >= 1f);
    }

    [Fact]
    public void InlineContainerLayout_ShouldReserveHostedWidthBeforeFollowingText()
    {
        var editor = CreateEditor(string.Empty);
        var button = new Button
        {
            Content = "Inline",
            Width = 72f,
            Height = 18f
        };

        var layout = LayoutDocument(editor, BuildInlineContainerDocument(button));

        var placement = Assert.Single(layout.HostedElements, static item => !item.IsBlock);
        var afterRun = Assert.Single(layout.Runs, static run => string.Equals(run.Text, "B", StringComparison.Ordinal));

        Assert.True(placement.Bounds.Width >= 72f);
        Assert.True(afterRun.Bounds.X >= placement.Bounds.X + placement.Bounds.Width - 0.5f);
    }

    [Fact]
    public void BlockContainerLayout_ShouldAdvanceNextParagraphBelowHostedChild()
    {
        var editor = CreateEditor(string.Empty);
        var button = new Button
        {
            Content = "Block",
            Width = 84f,
            Height = 20f
        };

        var layout = LayoutDocument(editor, BuildBlockContainerDocument(button));

        var placement = Assert.Single(layout.HostedElements, static item => item.IsBlock);
        var beforeRun = Assert.Single(layout.Runs, static run => string.Equals(run.Text, "Before", StringComparison.Ordinal));
        var afterRun = Assert.Single(layout.Runs, static run => string.Equals(run.Text, "After", StringComparison.Ordinal));

        Assert.True(placement.Bounds.Y >= beforeRun.Bounds.Y + beforeRun.Bounds.Height - 0.5f);
        Assert.True(afterRun.Bounds.Y >= placement.Bounds.Y + placement.Bounds.Height - 0.5f);
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
        return BuildInlineContainerDocument(null);
    }

    private static FlowDocument BuildInlineContainerDocument(UIElement? child)
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("A"));
        paragraph.Inlines.Add(new InlineUIContainer { Child = child });
        paragraph.Inlines.Add(new Run("B"));
        document.Blocks.Add(paragraph);
        return document;
    }

    private static FlowDocument BuildBlockContainerDocument(UIElement? child)
    {
        var document = new FlowDocument();

        var beforeParagraph = new Paragraph();
        beforeParagraph.Inlines.Add(new Run("Before"));
        document.Blocks.Add(beforeParagraph);

        document.Blocks.Add(new BlockUIContainer { Child = child });

        var afterParagraph = new Paragraph();
        afterParagraph.Inlines.Add(new Run("After"));
        document.Blocks.Add(afterParagraph);

        return document;
    }

    private static DocumentLayoutResult LayoutDocument(RichTextBox editor, FlowDocument document)
    {
        var typography = UiTextRenderer.ResolveTypography(editor, editor.FontSize);
        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(typography));
        var settings = new DocumentLayoutSettings(
            AvailableWidth: 440f,
            Typography: typography,
            Wrapping: editor.TextWrapping,
            Foreground: editor.Foreground,
            LineHeight: lineHeight,
            ListIndent: lineHeight * 1.2f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f);
        return new DocumentLayoutEngine().Layout(document, settings);
    }
}
