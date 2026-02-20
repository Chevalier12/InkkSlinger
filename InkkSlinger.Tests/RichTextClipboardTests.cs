using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextClipboardTests
{
    [Fact]
    public void SerializeRange_AndPaste_PreservesInlineFormatting()
    {
        TextClipboard.ResetForTests();

        var source = CreateEditorWithFormattedDocument();
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, source);

        var target = CreateEditor(string.Empty);
        CommandManager.Execute(EditingCommands.Paste, null, target);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(target.Document.Blocks));
        Assert.Equal("Hello World", DocumentEditing.GetText(target.Document));
        Assert.Equal(2, paragraph.Inlines.Count);
        Assert.IsType<Run>(paragraph.Inlines[0]);
        var bold = Assert.IsType<Bold>(paragraph.Inlines[1]);
        Assert.Equal("World", Assert.IsType<Run>(Assert.Single(bold.Inlines)).Text);
    }

    [Fact]
    public void Cut_RemovesSelection_AndStoresPlainAndRichPayloads()
    {
        TextClipboard.ResetForTests();

        var editor = CreateEditor("cut me");
        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Cut, null, editor);

        Assert.Equal(string.Empty, DocumentEditing.GetText(editor.Document));
        Assert.True(TextClipboard.TryGetText(out var text));
        Assert.Equal("cut me", text);
        Assert.True(TextClipboard.TryGetData<string>(FlowDocumentSerializer.ClipboardFormat, out var richXml));

        var fragment = FlowDocumentSerializer.DeserializeFragment(richXml!);
        Assert.Equal("cut me", DocumentEditing.GetText(fragment));
    }

    [Fact]
    public void Paste_WithInvalidRichPayload_FallsBackToPlainText()
    {
        TextClipboard.ResetForTests();

        var editor = CreateEditor(string.Empty);
        TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, "<not-valid");
        TextClipboard.SetText("fallback text");

        CommandManager.Execute(EditingCommands.Paste, null, editor);

        Assert.Equal("fallback text", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void SerializeRange_SlicesSelectedFormattedRun()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("Hi "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("there"));
        paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new Run("!"));
        document.Blocks.Add(paragraph);

        var selection = new DocumentTextSelection(
            DocumentPointers.CreateAtDocumentOffset(document, 3),
            DocumentPointers.CreateAtDocumentOffset(document, 8));
        var xml = FlowDocumentSerializer.SerializeRange(document, selection);
        var fragment = FlowDocumentSerializer.DeserializeFragment(xml);

        Assert.Equal("there", DocumentEditing.GetText(fragment));
        var fragmentParagraph = Assert.IsType<Paragraph>(Assert.Single(fragment.Blocks));
        var fragmentBold = Assert.IsType<Bold>(Assert.Single(fragmentParagraph.Inlines));
        Assert.Equal("there", Assert.IsType<Run>(Assert.Single(fragmentBold.Inlines)).Text);
    }

    [Fact]
    public void Paste_ReadsStandardXamlClipboardFormat()
    {
        TextClipboard.ResetForTests();

        var source = CreateEditorWithFormattedDocument();
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, source);
        Assert.True(TextClipboard.TryGetData<string>("Xaml", out var xamlPayload));

        TextClipboard.ResetForTests();
        TextClipboard.SetData("Xaml", xamlPayload);

        var target = CreateEditor(string.Empty);
        CommandManager.Execute(EditingCommands.Paste, null, target);

        Assert.Equal("Hello World", DocumentEditing.GetText(target.Document));
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(target.Document.Blocks));
        Assert.IsType<Bold>(paragraph.Inlines[1]);
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 240f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static RichTextBox CreateEditorWithFormattedDocument()
    {
        var editor = CreateEditor(string.Empty);
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("Hello "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("World"));
        paragraph.Inlines.Add(bold);

        var formatted = new FlowDocument();
        formatted.Blocks.Add(paragraph);
        editor.Document = formatted;
        return editor;
    }
}
