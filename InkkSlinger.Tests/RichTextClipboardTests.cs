using System.IO;
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
    public void PasteCanExecute_WithCachedClipboardText_DoesNotSyncExternalClipboard()
    {
        TextClipboard.ResetForTests();
        TextClipboard.SetText("cached text");

        var editor = CreateEditor(string.Empty);
        var before = TextClipboard.GetSnapshot();

        Assert.True(CommandManager.CanExecute(EditingCommands.Paste, null, editor));

        var after = TextClipboard.GetSnapshot();
        Assert.Equal(before.SyncCallCount, after.SyncCallCount);
        Assert.Equal(before.SyncExternalReadCount, after.SyncExternalReadCount);
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

    [Fact]
    public void Paste_ReadsRichTextClipboardFormat()
    {
        TextClipboard.ResetForTests();

        TextClipboard.SetData("Rich Text Format", @"{\rtf1\ansi alpha\par beta}");

        var target = CreateEditor(string.Empty);
        CommandManager.Execute(EditingCommands.Paste, null, target);

        Assert.Equal("alpha\nbeta", DocumentEditing.GetText(target.Document));
    }

    [Fact]
    public void Paste_WhenExternalClipboardChanges_IgnoresStaleInternalRichPayload()
    {
        TextClipboard.ResetForTests();

        var source = CreateEditorWithFormattedDocument();
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, source);

        TextClipboard.GetTextOverride = () => "external plain text";

        var target = CreateEditor(string.Empty);
        CommandManager.Execute(EditingCommands.Paste, null, target);

        Assert.Equal("external plain text", DocumentEditing.GetText(target.Document));
    }

    [Fact]
    public void PastePlainText_IntoRichStructuredDocument_ShouldNotFlattenStructure()
    {
        TextClipboard.ResetForTests();

        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListAndTableDocument();
        TextClipboard.SetText("external text");

        SetSelection(editor, start: 3, length: 0);
        CommandManager.Execute(EditingCommands.Paste, null, editor);

        Assert.Contains(editor.Document.Blocks, static b => b is InkkSlinger.List);
        Assert.Contains(editor.Document.Blocks, static b => b is Table);
    }

    [Fact]
    public void CopyPasteWithinRichStructuredDocument_ShouldNotFlattenStructure()
    {
        TextClipboard.ResetForTests();

        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListAndTableDocument();
        var text = DocumentEditing.GetText(editor.Document);
        var copyStart = text.IndexOf("List item 1", StringComparison.Ordinal);
        Assert.True(copyStart >= 0);
        SetSelection(editor, copyStart, "List item 1".Length);
        CommandManager.Execute(EditingCommands.Copy, null, editor);

        var insertAt = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(insertAt >= 0);
        SetSelection(editor, insertAt, length: 0);
        CommandManager.Execute(EditingCommands.Paste, null, editor);

        Assert.Contains(editor.Document.Blocks, static b => b is InkkSlinger.List);
        Assert.Contains(editor.Document.Blocks, static b => b is Table);
    }

    [Fact]
    public void CanLoadAndSave_ShouldRecognizeKnownFormats()
    {
        var editor = CreateEditor("alpha");

        Assert.True(editor.CanLoad(FlowDocumentSerializer.ClipboardFormat));
        Assert.True(editor.CanLoad("Xaml"));
        Assert.True(editor.CanLoad("XamlPackage"));
        Assert.True(editor.CanLoad("Rich Text Format"));
        Assert.True(editor.CanLoad("Text"));
        Assert.True(editor.CanSave("UnicodeText"));
        Assert.False(editor.CanLoad("Bitmap"));
    }

    [Fact]
    public void SaveSelectionAndLoadSelection_Xaml_RoundTripFormatting()
    {
        var source = CreateEditorWithFormattedDocument();
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);

        using var stream = new MemoryStream();
        source.SaveSelection(stream, "Xaml");

        var target = CreateEditor(string.Empty);
        target.LoadSelection(stream, "Xaml");

        Assert.Equal("Hello World", DocumentEditing.GetText(target.Document));
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(target.Document.Blocks));
        Assert.IsType<Bold>(paragraph.Inlines[1]);
    }

    [Fact]
    public void Load_RichTextFormat_ReplacesWholeDocument()
    {
        var editor = CreateEditor("seed");
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(@"{\rtf1\ansi line1\par line2}"));

        editor.Load(stream, "Rich Text Format");

        Assert.Equal("line1\nline2", DocumentEditing.GetText(editor.Document));
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(0, editor.CaretIndex);
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

    private static FlowDocument BuildListAndTableDocument()
    {
        var document = new FlowDocument();
        var intro = new Paragraph();
        intro.Inlines.Add(new Run("Intro"));
        document.Blocks.Add(intro);

        var list = new InkkSlinger.List { IsOrdered = true };
        list.Items.Add(CreateItem("List item 1"));
        list.Items.Add(CreateItem("List item 2"));
        document.Blocks.Add(list);

        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(CreateCell("R1C1"));
        row.Cells.Add(CreateCell("R1C2"));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        document.Blocks.Add(table);
        return document;
    }

    private static ListItem CreateItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static TableCell CreateCell(string text)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        cell.Blocks.Add(paragraph);
        return cell;
    }

    private static void SetSelection(RichTextBox editor, int start, int length)
    {
        var anchorField = typeof(RichTextBox).GetField("_selectionAnchor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var caretField = typeof(RichTextBox).GetField("_caretIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(anchorField);
        Assert.NotNull(caretField);
        anchorField!.SetValue(editor, start);
        caretField!.SetValue(editor, start + length);
    }
}
