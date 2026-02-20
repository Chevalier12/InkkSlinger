using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextBoxComplexFlowsTests
{
    [Fact]
    public void NewEditor_StartsWithValidDocumentAndCaretAtZero()
    {
        var editor = CreateEditor(320f, 90f, string.Empty);

        Assert.NotNull(editor.Document);
        Assert.True(editor.Document.Blocks.Count >= 1);
        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void BackspaceAtStart_DoesNotMutateDocument()
    {
        var editor = CreateEditor(320f, 90f, "abc");

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.Control));
        var before = DocumentEditing.GetText(editor.Document);
        Assert.False(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.Equal(before, DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void DeleteAtEnd_DoesNotMutateDocument()
    {
        var editor = CreateEditor(320f, 90f, "abc");

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        var before = DocumentEditing.GetText(editor.Document);
        Assert.False(editor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None));
        Assert.Equal(before, DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void EnterParagraphBreak_SplitsTextAtCaret()
    {
        var editor = CreateEditor(320f, 90f, "abcd");

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Left, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Left, ModifierKeys.None));
        CommandManager.Execute(EditingCommands.EnterParagraphBreak, null, editor);

        var normalized = DocumentEditing.GetText(editor.Document).Replace("\r\n", "\n");
        Assert.Equal("ab\ncd", normalized);
    }

    [Fact]
    public void MoveLeftRightByCharacter_WithShiftExpandsAndCollapsesSelection()
    {
        var editor = CreateEditor(320f, 90f, "abc");

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Shift));
        Assert.Equal(1, editor.SelectionLength);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Left, ModifierKeys.Shift));
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void SelectAllCutUndoRedo_RoundTripsText()
    {
        TextClipboard.ResetForTests();
        var editor = CreateEditor(380f, 100f, "alpha beta");

        Assert.True(editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        CommandManager.Execute(EditingCommands.Cut, null, editor);
        Assert.Equal(string.Empty, DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.Equal("alpha beta", DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control));
        Assert.Equal(string.Empty, DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void CopyFromOneEditor_PasteIntoAnother_PreservesPlainText()
    {
        TextClipboard.ResetForTests();
        var source = CreateEditor(380f, 100f, "copy source");
        var target = CreateEditor(380f, 100f, "target");

        Assert.True(source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        CommandManager.Execute(EditingCommands.Copy, null, source);

        Assert.True(target.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        CommandManager.Execute(EditingCommands.Paste, null, target);

        Assert.Equal("targetcopy source", DocumentEditing.GetText(target.Document));
    }

    [Fact]
    public void BoldThenTypeThenUndoSteps_RevertsInExpectedOrder()
    {
        var editor = CreateEditor(420f, 120f, "hello world");

        Assert.True(editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.True(editor.HandleTextInputFromInput('!'));
        Assert.Equal("hello world!", DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.Equal("hello world", DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.IsType<Run>(Assert.Single(paragraph.Inlines));
    }

    [Fact]
    public void ListIndentOutdent_UndoRedo_RemainsDeterministic()
    {
        var editor = CreateEditor(480f, 180f, "one\ntwo\nthree");

        Assert.True(editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        CommandManager.Execute(EditingCommands.IncreaseListLevel, null, editor);
        Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));

        CommandManager.Execute(EditingCommands.DecreaseListLevel, null, editor);
        var afterOutdentText = DocumentEditing.GetText(editor.Document);
        Assert.Contains("one", afterOutdentText);
        Assert.Contains("two", afterOutdentText);
        Assert.Contains("three", afterOutdentText);
        Assert.True(editor.Document.Blocks.Count >= 2);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control));
        var afterRedoText = DocumentEditing.GetText(editor.Document);
        Assert.Equal(afterOutdentText, afterRedoText);
    }

    [Fact]
    public void InsertTableSplitMerge_UndoRedo_ReplaysStructure()
    {
        var editor = CreateEditor(480f, 180f, string.Empty);

        CommandManager.Execute(EditingCommands.InsertTable, null, editor);
        CommandManager.Execute(EditingCommands.SplitCell, null, editor);
        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, Assert.Single(table.RowGroups).Rows[0].Cells.Count);

        CommandManager.Execute(EditingCommands.MergeCells, null, editor);
        table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, Assert.Single(table.RowGroups).Rows[0].Cells.Count);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, Assert.Single(table.RowGroups).Rows[0].Cells.Count);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control));
        table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, Assert.Single(table.RowGroups).Rows[0].Cells.Count);
    }

    [Fact]
    public void PointerDoubleClickSelectsWord_ThenTypingReplacesSelection()
    {
        var editor = CreateEditor(380f, 100f, "hello world");
        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var point = new Vector2(textLeft + FontStashTextRenderer.MeasureWidth(null, "hello wo"), textTop + 2f);

        Assert.True(editor.HandlePointerDownFromInput(point, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandlePointerDownFromInput(point, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.Equal("world", GetSelectedText(editor));

        Assert.True(editor.HandleTextInputFromInput('X'));
        Assert.Equal("hello X", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void RichPasteThenUndoRedo_RoundTripsDocumentText()
    {
        TextClipboard.ResetForTests();
        var source = CreateEditorWithBoldTail("Hello ", "World");
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, source);

        var target = CreateEditor(420f, 120f, string.Empty);
        CommandManager.Execute(EditingCommands.Paste, null, target);
        Assert.Equal("Hello World", DocumentEditing.GetText(target.Document));

        Assert.True(target.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.Equal(string.Empty, DocumentEditing.GetText(target.Document));

        Assert.True(target.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control));
        Assert.Equal("Hello World", DocumentEditing.GetText(target.Document));
    }

    private static RichTextBox CreateEditor(float width, float height, string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, width, height));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static RichTextBox CreateEditorWithBoldTail(string prefix, string boldText)
    {
        var editor = CreateEditor(420f, 120f, string.Empty);
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(prefix));
        var bold = new Bold();
        bold.Inlines.Add(new Run(boldText));
        paragraph.Inlines.Add(bold);
        var document = new FlowDocument();
        document.Blocks.Add(paragraph);
        editor.Document = document;
        return editor;
    }

    private static string GetSelectedText(RichTextBox editor)
    {
        var text = DocumentEditing.GetText(editor.Document);
        if (editor.SelectionLength <= 0)
        {
            return string.Empty;
        }

        return text.Substring(editor.SelectionStart, editor.SelectionLength);
    }
}
