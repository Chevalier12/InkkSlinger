using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextBoxRegressionTests
{
    [Fact]
    public void TypingBurst_CoalescesAndSingleUndoRevertsBurst()
    {
        var editor = CreateEditor(320f, 80f, string.Empty);

        Assert.True(editor.HandleTextInputFromInput('a'));
        Assert.True(editor.HandleTextInputFromInput('b'));
        Assert.True(editor.HandleTextInputFromInput('c'));
        Assert.Equal("abc", DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.Equal(string.Empty, DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void CtrlHomeAndCtrlEnd_MoveCaretToDocumentBounds()
    {
        var editor = CreateEditor(320f, 100f, "alpha beta\ngamma");

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.Equal(DocumentEditing.GetText(editor.Document).Length, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.Control));
        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void ShiftHome_SelectsToLineStart()
    {
        var editor = CreateEditor(360f, 90f, "hello world");

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.Shift));

        Assert.Equal("hello world", GetSelectedText(editor));
    }

    [Fact]
    public void SelectAllThenTyping_ReplacesEntireDocument()
    {
        var editor = CreateEditor(360f, 90f, "replace me");

        Assert.True(editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        Assert.True(editor.HandleTextInputFromInput('X'));

        Assert.Equal("X", DocumentEditing.GetText(editor.Document));
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void MouseWheelRequiresFocus_AndScrollsWhenFocused()
    {
        var editor = CreateEditor(180f, 40f, string.Join("\n", BuildLines(20)));
        editor.SetFocusedFromInput(false);
        Assert.False(editor.HandleMouseWheelFromInput(-120));

        editor.SetFocusedFromInput(true);
        Assert.True(editor.HandleMouseWheelFromInput(-120));
    }

    [Fact]
    public void PointerDrag_ProducesSelectionRange()
    {
        var editor = CreateEditor(320f, 90f, "alpha beta gamma");
        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var from = new Vector2(textLeft + FontStashTextRenderer.MeasureWidth(null, "alpha"), textTop + 2f);
        var to = new Vector2(textLeft + FontStashTextRenderer.MeasureWidth(null, "alpha beta"), textTop + 2f);

        Assert.True(editor.HandlePointerDownFromInput(from, extendSelection: false));
        Assert.True(editor.HandlePointerMoveFromInput(to));
        Assert.True(editor.HandlePointerUpFromInput());

        Assert.True(editor.SelectionLength > 0);
        Assert.Contains("beta", GetSelectedText(editor));
    }

    [Fact]
    public void ReadOnlyCtrlEnter_StillActivatesHyperlink()
    {
        var editor = CreateEditor(320f, 90f, string.Empty);
        editor.Document = BuildHyperlinkDocument("https://example.com", "site");
        editor.IsReadOnly = true;

        var hitCount = 0;
        editor.HyperlinkNavigate += (_, args) =>
        {
            if (args.NavigateUri == "https://example.com")
            {
                hitCount++;
            }
        };

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.Control));
        Assert.Equal(1, hitCount);
    }

    [Fact]
    public void InsertTable_UndoAndRedo_RestoresStructure()
    {
        var editor = CreateEditor(360f, 120f, string.Empty);

        CommandManager.Execute(EditingCommands.InsertTable, null, editor);
        Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control));
        Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
    }

    [Fact]
    public void CtrlWordNavigation_StopsAtWhitespaceBoundaries()
    {
        var editor = CreateEditor(320f, 90f, "alpha beta gamma");

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Control));
        Assert.Equal(5, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Control));
        Assert.Equal(6, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Control));
        Assert.Equal(10, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Left, ModifierKeys.Control));
        Assert.Equal(6, editor.CaretIndex);
    }

    [Fact]
    public void ReadOnlyBlocksTypingButAllowsSelectionNavigation()
    {
        var editor = CreateEditor(320f, 90f, "alpha beta");
        editor.IsReadOnly = true;

        Assert.False(editor.HandleTextInputFromInput('x'));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Shift));
        Assert.True(editor.SelectionLength > 0);
        Assert.Equal("alpha beta", DocumentEditing.GetText(editor.Document));
    }

    private static RichTextBox CreateEditor(float width, float height, string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, width, height));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
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

    private static string[] BuildLines(int count)
    {
        var lines = new string[count];
        for (var i = 0; i < count; i++)
        {
            lines[i] = $"Line {i:D2}";
        }

        return lines;
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
}
