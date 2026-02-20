using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextWpfParityGapTests
{
    [Fact]
    public void ToggleBold_OnSecondRange_ShouldPreserveFirstBoldRange()
    {
        var editor = CreateEditor("alpha beta");

        SetSelection(editor, start: 0, length: 5);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        SetSelection(editor, start: 6, length: 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Equal("alpha beta", DocumentEditing.GetText(editor.Document));
        Assert.Equal(3, paragraph.Inlines.Count);
        Assert.IsType<Bold>(paragraph.Inlines[0]);
        Assert.IsType<Run>(paragraph.Inlines[1]);
        Assert.IsType<Bold>(paragraph.Inlines[2]);
    }

    [Fact]
    public void ToggleBold_OnFirstLine_ShouldNotAffectSecondLine()
    {
        var editor = CreateEditor("first\nsecond");

        SetSelection(editor, start: 0, length: 5);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.Equal("first\nsecond", DocumentEditing.GetText(editor.Document));
        Assert.Equal(2, editor.Document.Blocks.Count);

        var firstParagraph = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        var secondParagraph = Assert.IsType<Paragraph>(editor.Document.Blocks[1]);

        Assert.Single(firstParagraph.Inlines);
        Assert.IsType<Bold>(firstParagraph.Inlines[0]);
        Assert.Single(secondParagraph.Inlines);
        Assert.IsType<Run>(secondParagraph.Inlines[0]);
    }

    [Fact]
    public void ToggleBold_OnEntireFirstLine_ShouldKeepSelectionOnThatLine()
    {
        var editor = CreateEditor("first\nsecond");

        SetSelection(editor, start: 0, length: 5);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(5, editor.SelectionLength);
        Assert.Equal(5, editor.CaretIndex);
        Assert.Equal("first\nsecond", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void ToggleBold_WhenSelectionIncludesLineBreak_CaretMovesToStartOfNextLine()
    {
        var editor = CreateEditor("first\nsecond");

        SetSelection(editor, start: 0, length: 6);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(6, editor.SelectionLength);
        Assert.Equal(6, editor.CaretIndex);
        Assert.Equal("first\nsecond", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void ToggleBold_RtlSelectionAcrossLineBreak_ShouldPreserveCaretAtSelectionStart()
    {
        var editor = CreateEditor("first\nsecond");

        SetRawSelection(editor, anchor: 6, caret: 0);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(6, editor.SelectionLength);
    }

    [Fact]
    public void ToggleBold_LtrSelectionAcrossLineBreak_ShouldPreserveCaretAtSelectionEnd()
    {
        var editor = CreateEditor("first\nsecond");

        SetRawSelection(editor, anchor: 0, caret: 6);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.Equal(6, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(6, editor.SelectionLength);
    }

    [Fact]
    public void ClickAtEndOfFirstLine_ThenType_ShouldInsertBeforeParagraphBreak()
    {
        var editor = CreateEditor("first\nsecond");

        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var pointAtEndOfFirstLine = new Microsoft.Xna.Framework.Vector2(
            textLeft + FontStashTextRenderer.MeasureWidth(null, "first"),
            textTop + 2f);

        Assert.True(editor.HandlePointerDownFromInput(pointAtEndOfFirstLine, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleTextInputFromInput('X'));

        Assert.Equal("firstX\nsecond", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void BoldFirstLineThenTypeAtItsEnd_ShouldNotMergeWithSecondLine()
    {
        var editor = CreateEditor("first\nsecond");

        SetSelection(editor, start: 0, length: 5);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var layout = BuildLayout(editor, 402f);
        Assert.True(layout.TryGetCaretPosition(5, out var caretAtEndOfFirstLine));
        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var pointAtEndOfFirstLine = new Microsoft.Xna.Framework.Vector2(
            textLeft + caretAtEndOfFirstLine.X,
            textTop + caretAtEndOfFirstLine.Y + 2f);

        Assert.True(editor.HandlePointerDownFromInput(pointAtEndOfFirstLine, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.Equal(5, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
        Assert.True(editor.HandleTextInputFromInput('X'));

        Assert.Equal("firstX\nsecond", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void TypeEnterType_ClickEndOfFirstLine_ThenType_ShouldNotCollapseParagraphBreak()
    {
        var editor = CreateEditor(string.Empty);

        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleTextInputFromInput('e'));
        Assert.True(editor.HandleTextInputFromInput('s'));
        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleTextInputFromInput('e'));
        Assert.True(editor.HandleTextInputFromInput('s'));
        Assert.True(editor.HandleTextInputFromInput('t'));

        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var pointAtEndOfFirstLine = new Microsoft.Xna.Framework.Vector2(
            textLeft + FontStashTextRenderer.MeasureWidth(null, "test"),
            textTop + 2f);

        Assert.True(editor.HandlePointerDownFromInput(pointAtEndOfFirstLine, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleTextInputFromInput('t'));

        Assert.Equal("testt\ntest", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void DoubleClickAtEndOfFirstLine_ThenType_ShouldNotReplaceParagraphBreak()
    {
        var editor = CreateEditor("test\ntest");

        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var pointAtEndOfFirstLine = new Microsoft.Xna.Framework.Vector2(
            textLeft + FontStashTextRenderer.MeasureWidth(null, "test"),
            textTop + 2f);

        Assert.True(editor.HandlePointerDownFromInput(pointAtEndOfFirstLine, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandlePointerDownFromInput(pointAtEndOfFirstLine, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleTextInputFromInput('t'));

        Assert.Equal("testt\ntest", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void EnterAfterBoldText_ShouldPreserveExistingBoldFormatting()
    {
        var editor = CreateEditor(string.Empty);

        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleTextInputFromInput('e'));
        Assert.True(editor.HandleTextInputFromInput('s'));
        Assert.True(editor.HandleTextInputFromInput('t'));

        SetSelection(editor, start: 0, length: 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        SetSelection(editor, start: 4, length: 0);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        Assert.Equal("test\n", DocumentEditing.GetText(editor.Document));
        Assert.Equal(2, editor.Document.Blocks.Count);

        var firstParagraph = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Contains(firstParagraph.Inlines, inline => inline is Bold);
    }

    [Fact]
    public void TypingAfterBoldSelectionAtCaret_ShouldNotRemovePreviousBold()
    {
        var editor = CreateEditor(string.Empty);

        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleTextInputFromInput('e'));
        Assert.True(editor.HandleTextInputFromInput('s'));
        Assert.True(editor.HandleTextInputFromInput('t'));

        SetSelection(editor, start: 0, length: 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        SetSelection(editor, start: 4, length: 0);

        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.Equal("testt", DocumentEditing.GetText(editor.Document));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Contains(paragraph.Inlines, inline => inline is Bold);
    }

    [Fact]
    public void ToggleBold_OnAlreadyBoldSelection_ShouldRemoveBold()
    {
        var editor = CreateEditor(string.Empty);

        Assert.True(editor.HandleTextInputFromInput('t'));
        Assert.True(editor.HandleTextInputFromInput('e'));
        Assert.True(editor.HandleTextInputFromInput('s'));
        Assert.True(editor.HandleTextInputFromInput('t'));

        SetSelection(editor, start: 0, length: 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Equal("test", DocumentEditing.GetText(editor.Document));
        Assert.DoesNotContain(paragraph.Inlines, inline => inline is Bold);
    }

    [Fact]
    public void ToggleBold_OnR1C1TableCellSelection_ShouldPreserveTableStructure()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildR1C1TableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(start >= 0);

        SetSelection(editor, start, 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        var group = Assert.Single(table.RowGroups);
        Assert.Equal(2, group.Rows.Count);
        Assert.Equal(2, group.Rows[0].Cells.Count);
        Assert.Equal(2, group.Rows[1].Cells.Count);

        var firstCellParagraph = Assert.IsType<Paragraph>(Assert.Single(group.Rows[0].Cells[0].Blocks));
        Assert.Contains(firstCellParagraph.Inlines, inline => inline is Bold);
    }

    [Fact]
    public void TypingInR1C1Cell_ShouldNotFlattenTableStructure()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildR1C1TableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(start >= 0);
        SetSelection(editor, start + 4, 0);

        Assert.True(editor.HandleTextInputFromInput('X'));

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        var group = Assert.Single(table.RowGroups);
        Assert.Equal(2, group.Rows.Count);
        Assert.Equal(2, group.Rows[0].Cells.Count);
        Assert.Equal(2, group.Rows[1].Cells.Count);

        var firstCellParagraph = Assert.IsType<Paragraph>(Assert.Single(group.Rows[0].Cells[0].Blocks));
        var firstCellText = FlowDocumentPlainText.GetInlineText(firstCellParagraph.Inlines);
        Assert.Equal("R1C1X", firstCellText);
    }

    [Fact]
    public void BoldThenTypeInR1C1Cell_ShouldNotFlattenTableStructure()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildR1C1TableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(start >= 0);

        SetSelection(editor, start, 4);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        Assert.True(editor.HandleTextInputFromInput('X'));

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        var group = Assert.Single(table.RowGroups);
        Assert.Equal(2, group.Rows.Count);
        Assert.Equal(2, group.Rows[0].Cells.Count);
        Assert.Equal(2, group.Rows[1].Cells.Count);

        var firstCellParagraph = Assert.IsType<Paragraph>(Assert.Single(group.Rows[0].Cells[0].Blocks));
        var firstCellText = FlowDocumentPlainText.GetInlineText(firstCellParagraph.Inlines);
        Assert.Equal("X", firstCellText);
    }

    [Fact]
    public void ClickR1C2ThenType_ShouldEditR1C2Cell()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildR1C1TableDocument();

        var measure = typeof(RichTextBox).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(measure);
        _ = measure!.Invoke(editor, [new Vector2(420f, 140f)]);
        var build = typeof(RichTextBox).GetMethod("BuildOrGetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(build);
        var layout = (DocumentLayoutResult?)build!.Invoke(editor, [400f]);
        Assert.NotNull(layout);
        Assert.True(layout!.TableCellBounds.Count >= 2);

        var r1c2 = layout.TableCellBounds[1];
        var pointInR1C2 = new Vector2(
            (1f + 8f) + r1c2.X + (r1c2.Width * 0.5f),
            (1f + 5f) + r1c2.Y + (r1c2.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(pointInR1C2, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleTextInputFromInput('Z'));

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        var group = Assert.Single(table.RowGroups);
        var r1c1Paragraph = Assert.IsType<Paragraph>(Assert.Single(group.Rows[0].Cells[0].Blocks));
        var r1c2Paragraph = Assert.IsType<Paragraph>(Assert.Single(group.Rows[0].Cells[1].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1Paragraph.Inlines));
        Assert.Contains("Z", FlowDocumentPlainText.GetInlineText(r1c2Paragraph.Inlines));
    }

    [Fact]
    public void PressEnterInR1C1Cell_ShouldNotFlattenTableStructure()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildR1C1TableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(start >= 0);
        SetSelection(editor, start + 4, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var table = Assert.IsType<Table>(Assert.Single(editor.Document.Blocks));
        var group = Assert.Single(table.RowGroups);
        Assert.Equal(2, group.Rows.Count);
        Assert.Equal(2, group.Rows[0].Cells.Count);
        Assert.Equal(2, group.Rows[1].Cells.Count);

        var r1c1Cell = group.Rows[0].Cells[0];
        Assert.True(r1c1Cell.Blocks.Count >= 2);
        var r1c1FirstParagraph = Assert.IsType<Paragraph>(r1c1Cell.Blocks[0]);
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1FirstParagraph.Inlines));
    }

    [Fact]
    public void EnterThenBackspace_AtEndOfSecondListItem_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildTwoItemListDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, text.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(2, list.Items.Count);
        var first = Assert.IsType<Paragraph>(Assert.Single(list.Items[0].Blocks));
        var second = Assert.IsType<Paragraph>(list.Items[1].Blocks[0]);
        Assert.Equal("Item 1", FlowDocumentPlainText.GetInlineText(first.Inlines));
        Assert.Equal("Item 2", FlowDocumentPlainText.GetInlineText(second.Inlines));
    }

    [Fact]
    public void EnterEnterThenBackspace_AtEndOfSecondListItemWithTable_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        var second = Assert.IsType<Paragraph>(Assert.Single(list.Items[1].Blocks));
        Assert.Equal("Item 2", FlowDocumentPlainText.GetInlineText(second.Inlines));

        var table = Assert.IsType<Table>(editor.Document.Blocks[1]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void EnterEnterEnterThenBackspace_AfterListBeforeTable_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // create item 3
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // exit list
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // second top-level blank paragraph
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));  // merge back

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void EnterThenSpace_AtEndOfSecondListItem_ShouldInsertSpace()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildTwoItemListDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, text.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput(' '));

        var list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, list.Items.Count);
        var thirdParagraph = Assert.IsType<Paragraph>(Assert.Single(list.Items[2].Blocks));
        Assert.Equal(" ", FlowDocumentPlainText.GetInlineText(thirdParagraph.Inlines));
    }

    [Fact]
    public void EnterThenSpaceKey_AtEndOfSecondListItem_ShouldInsertSpace()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildTwoItemListDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, text.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        _ = editor.HandleKeyDownFromInput(Keys.Space, ModifierKeys.None);

        var list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, list.Items.Count);
        var thirdParagraph = Assert.IsType<Paragraph>(Assert.Single(list.Items[2].Blocks));
        Assert.Equal(" ", FlowDocumentPlainText.GetInlineText(thirdParagraph.Inlines));
    }

    [Fact]
    public void EnterAtEndOfSecondListItem_ShouldCreateThirdListItem()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildTwoItemListDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(Assert.Single(editor.Document.Blocks));
        Assert.Equal(3, list.Items.Count);
        var thirdParagraph = Assert.IsType<Paragraph>(Assert.Single(list.Items[2].Blocks));
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetInlineText(thirdParagraph.Inlines));
    }

    [Fact]
    public void EnterTwiceAtEndOfSecondListItem_ShouldExitListOnSecondEnter()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildTwoItemListDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        var trailingParagraph = Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetInlineText(trailingParagraph.Inlines));
    }

    [Fact]
    public void EnterThenSpaceInListBeforeTable_ShouldNotJumpCaretIntoR1C1()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var listItem2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(listItem2Start >= 0);
        SetSelection(editor, listItem2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Space, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        var secondItemSecondParagraph = Assert.IsType<Paragraph>(list.Items[1].Blocks[1]);
        Assert.Equal(" ", FlowDocumentPlainText.GetInlineText(secondItemSecondParagraph.Inlines));

        var table = Assert.IsType<Table>(editor.Document.Blocks[1]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void ClickEndOfListItem2_ThenEnterThenSpace_ShouldNotJumpCaretIntoR1C1()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var measure = typeof(RichTextBox).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(measure);
        _ = measure!.Invoke(editor, [new Vector2(640f, 260f)]);
        var build = typeof(RichTextBox).GetMethod("BuildOrGetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(build);
        var layout = (DocumentLayoutResult?)build!.Invoke(editor, [620f]);
        Assert.NotNull(layout);
        var line = Assert.Single(layout!.Lines, static l => l.Text == "Item 2");
        var clickPoint = new Vector2(
            (1f + 8f) + line.TextStartX + line.PrefixWidths[line.PrefixWidths.Length - 1],
            (1f + 5f) + line.Bounds.Y + (line.Bounds.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Space, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(3, list.Items.Count);
        var thirdParagraph = Assert.IsType<Paragraph>(Assert.Single(list.Items[2].Blocks));
        Assert.Equal(" ", FlowDocumentPlainText.GetInlineText(thirdParagraph.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[1]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void EnterEnterThenTypeInListBeforeTable_ShouldNotTypeIntoR1C1()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput('a'));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        var paragraphAfterList = Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.Equal("a", FlowDocumentPlainText.GetInlineText(paragraphAfterList.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void ClickBlankLineAfterEnterEnter_ThenType_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var layout = BuildLayout(editor, 620f);
        var item2Line = Assert.Single(layout.Lines, static l => l.Text == "Item 2");
        var blankCandidates = layout.Lines.Where(l => l.Text.Length == 0 && l.Bounds.Y > item2Line.Bounds.Y).ToList();
        Assert.NotEmpty(blankCandidates);
        var blankLine = blankCandidates[0];
        var clickPoint = new Vector2(
            (1f + 8f) + blankLine.TextStartX + 1f,
            (1f + 5f) + blankLine.Bounds.Y + (blankLine.Bounds.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleTextInputFromInput('a'));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        var paragraphAfterList = Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.Equal("a", FlowDocumentPlainText.GetInlineText(paragraphAfterList.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_EnterEnterThenTypeAfterListItem2_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("List item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "List item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput('a'));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.Equal(2, list.Items.Count);
        var paragraphAfterList = Assert.IsType<Paragraph>(editor.Document.Blocks[2]);
        Assert.Equal("a", FlowDocumentPlainText.GetInlineText(paragraphAfterList.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[3]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_ClickEndOfListItem2_EnterEnterThenType_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var layout = BuildLayout(editor, 620f);
        var line = Assert.Single(layout.Lines, static l => l.Text == "List item 2");
        var clickPoint = new Vector2(
            (1f + 8f) + line.TextStartX + line.PrefixWidths[line.PrefixWidths.Length - 1],
            (1f + 5f) + line.Bounds.Y + (line.Bounds.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput('a'));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.Equal(2, list.Items.Count);
        var paragraphAfterList = Assert.IsType<Paragraph>(editor.Document.Blocks[2]);
        Assert.Equal("a", FlowDocumentPlainText.GetInlineText(paragraphAfterList.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[3]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_EnterEnterBackspaceBackspace_AfterListItem2_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var item2Start = text.IndexOf("List item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "List item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.Equal(2, list.Items.Count);
        var secondItemParagraph = Assert.IsType<Paragraph>(Assert.Single(list.Items[1].Blocks));
        Assert.Equal("List item ", FlowDocumentPlainText.GetInlineText(secondItemParagraph.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_ToggleBold_OnPartialListItemSelection_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var targetStart = text.IndexOf("item", StringComparison.Ordinal);
        Assert.True(targetStart >= 0);
        SetSelection(editor, targetStart, 4);

        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.Equal(2, list.Items.Count);
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_SelectAllThenToggleBold_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, 0, text.Length);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.Equal(2, list.Items.Count);
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_SelectAllToggleBoldThreeTimes_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, 0, text.Length);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.IsType<Table>(editor.Document.Blocks[2]);
    }

    [Fact]
    public void DiagnosticsLab_MultiParagraphBoldToggle_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("List item 2", StringComparison.Ordinal);
        var end = text.IndexOf("R1C1", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        SetSelection(editor, start, end - start + 2);

        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.IsType<Table>(editor.Document.Blocks[2]);
    }

    [Fact]
    public void DiagnosticsLab_RepeatedBoldThenPartialBold_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, 0, text.Length);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var latest = DocumentEditing.GetText(editor.Document);
        var start = Math.Min(60, Math.Max(0, latest.Length - 7));
        SetSelection(editor, start, Math.Min(7, Math.Max(0, latest.Length - start)));
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.IsType<Table>(editor.Document.Blocks[2]);
    }

    [Fact]
    public void DiagnosticsLab_BackspaceDeleteSelectionAcrossListAndTable_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("2", StringComparison.Ordinal);
        var end = text.IndexOf("R1C2", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        SetSelection(editor, start, (end - start) + 2);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.IsType<Table>(editor.Document.Blocks[2]);
    }

    [Fact]
    public void DiagnosticsLab_SelectAllThenBackspace_ShouldClearToSingleEmptyParagraph()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, 0, text.Length);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.Single(editor.Document.Blocks);
        var paragraph = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetInlineText(paragraph.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_BackspaceSelectionThenDeleteForward_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("this hyperlink", StringComparison.Ordinal);
        var end = text.IndexOf("R1C2", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        SetSelection(editor, start, end - start);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.IsType<Table>(editor.Document.Blocks[^1]);
    }

    [Fact]
    public void DiagnosticsLab_BackspaceStructuredRange_ShouldNotLeaveEmptyListShells()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 11, length: 64);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.IsType<Table>(editor.Document.Blocks[1]);
    }

    [Fact]
    public void DiagnosticsLab_BackspaceStructuredRange_ShouldNotLeaveRedundantEmptyParagraphs()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 10, length: 65);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.DoesNotContain(editor.Document.Blocks, static block => block is InkkSlinger.List);
        Assert.Contains(editor.Document.Blocks, static block => block is Table);
        var emptyTopLevelParagraphs = editor.Document.Blocks
            .OfType<Paragraph>()
            .Count(static p => string.IsNullOrWhiteSpace(FlowDocumentPlainText.GetInlineText(p.Inlines)));
        Assert.True(emptyTopLevelParagraphs <= 1, $"Expected at most one empty top-level paragraph, found {emptyTopLevelParagraphs}.");
    }

    [Fact]
    public void DiagnosticsLab_BackspaceStructuredRange_ShouldPreserveRemainingInlineFormatting()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 11, length: 64);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var intro = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Contains(intro.Inlines, inline => inline is Bold);
        Assert.StartsWith("Use Ctrl+En", FlowDocumentPlainText.GetInlineText(intro.Inlines), StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsLab_DeleteBoldAndListRange_ShouldNotClearTableCellText()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        var start = text.IndexOf("Ctrl+Enter", StringComparison.Ordinal);
        var end = text.IndexOf("List item 2", StringComparison.Ordinal) + "List item 2".Length;
        Assert.True(start >= 0 && end > start);
        SetSelection(editor, start, end - start);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var table = Assert.IsType<Table>(editor.Document.Blocks[^1]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        var r1c2 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[1].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
        Assert.Equal("R1C2", FlowDocumentPlainText.GetInlineText(r1c2.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_BackspaceStructuredRangeCrossingTableBoundary_ShouldKeepTableCellText()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 12, length: 73);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var table = Assert.IsType<Table>(editor.Document.Blocks[^1]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        var r1c2 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[1].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
        Assert.Equal("R1C2", FlowDocumentPlainText.GetInlineText(r1c2.Inlines));
    }

    [Fact]
    public void DiagnosticsLab_BackspaceAfterStructuredDelete_ShouldPreserveRemainingBoldSpan()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 12, length: 63);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        var intro = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Contains(intro.Inlines, inline => inline is Bold);
        Assert.StartsWith("Use Ctrl+En", FlowDocumentPlainText.GetInlineText(intro.Inlines), StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsLab_TypeAfterStructuredDelete_ShouldPreserveRemainingBoldSpan()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 10, length: 65);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.True(editor.HandleTextInputFromInput('a'));

        var intro = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Contains(intro.Inlines, inline => inline is Bold);
        Assert.StartsWith("Use Ctrl+Ea", FlowDocumentPlainText.GetInlineText(intro.Inlines), StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsLab_SingleCharacterUnboldAfterStructuredDelete_ShouldPreserveOtherBoldText()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        SetSelection(editor, start: 11, length: 64);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        SetSelection(editor, start: 9, length: 1);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var intro = Assert.IsType<Paragraph>(editor.Document.Blocks[0]);
        Assert.Equal("Use Ctrl+En", FlowDocumentPlainText.GetInlineText(intro.Inlines));
        Assert.Contains(intro.Inlines, inline => inline is Bold);
    }

    [Fact]
    public void DiagnosticsLab_EnterThenTypingBoldText_ShouldNotFlattenDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildDiagnosticsLabDocument();

        var text = DocumentEditing.GetText(editor.Document);
        SetSelection(editor, text.Length, 0);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor); // typing mode on
        Assert.True(editor.HandleTextInputFromInput('s'));

        Assert.IsType<Paragraph>(editor.Document.Blocks[0]); // intro
        Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[1]);
        Assert.IsType<Table>(editor.Document.Blocks[2]);
    }

    [Fact]
    public void ClickEndOfListItem2_ThenEnterEnter_ShouldNotFlattenRichDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var measure = typeof(RichTextBox).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(measure);
        _ = measure!.Invoke(editor, [new Vector2(640f, 260f)]);
        var build = typeof(RichTextBox).GetMethod("BuildOrGetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(build);
        var layout = (DocumentLayoutResult?)build!.Invoke(editor, [620f]);
        Assert.NotNull(layout);
        var line = Assert.Single(layout!.Lines, static l => l.Text == "Item 2");
        var clickPoint = new Vector2(
            (1f + 8f) + line.TextStartX + line.PrefixWidths[line.PrefixWidths.Length - 1],
            (1f + 5f) + line.Bounds.Y + (line.Bounds.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        var paragraphAfterList = Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetInlineText(paragraphAfterList.Inlines));
        var table = Assert.IsType<Table>(editor.Document.Blocks[2]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void EnterAtEndOfListItem2_ThreeTimes_ShouldNotFlattenRichDocument()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.IsType<Paragraph>(editor.Document.Blocks[2]);
        var table = Assert.IsType<Table>(editor.Document.Blocks[3]);
        var r1c1 = Assert.IsType<Paragraph>(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks));
        Assert.Equal("R1C1", FlowDocumentPlainText.GetInlineText(r1c1.Inlines));
    }

    [Fact]
    public void EnterOnEmptyParagraphInRichDocument_ShouldAdvanceCaretAndNotInsertSpace()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // create item 3
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // exit list to empty paragraph
        var caretBefore = editor.CaretIndex;
        var textBefore = DocumentEditing.GetText(editor.Document);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // press enter on empty paragraph

        var textAfter = DocumentEditing.GetText(editor.Document);
        Assert.Equal(caretBefore + 1, editor.CaretIndex);
        Assert.Equal(textBefore.Length + 1, textAfter.Length);
        Assert.DoesNotContain(" ", textAfter.Substring(caretBefore, 1));
    }

    [Fact]
    public void EnterOnEmptyParagraphInRichDocument_ShouldMoveCaretToNextVisualLine()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // create item 3
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // exit list to empty paragraph

        var layoutBefore = BuildLayout(editor, 620f);
        Assert.True(layoutBefore.TryGetCaretPosition(editor.CaretIndex, out var beforePos));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var layoutAfter = BuildLayout(editor, 620f);
        Assert.True(layoutAfter.TryGetCaretPosition(editor.CaretIndex, out var afterPos));
        Assert.True(afterPos.Y > beforePos.Y, $"Expected caret Y to increase after Enter. Before={beforePos.Y}, After={afterPos.Y}");
    }

    [Fact]
    public void UpDownArrows_ShouldMoveCaretBetweenLines()
    {
        var editor = CreateEditor("first\nsecond\nthird");

        SetSelection(editor, start: 8, length: 0);
        var caretBeforeUp = editor.CaretIndex;
        Assert.True(editor.HandleKeyDownFromInput(Keys.Up, ModifierKeys.None));
        Assert.True(editor.CaretIndex < caretBeforeUp);

        var caretBeforeDown = editor.CaretIndex;
        Assert.True(editor.HandleKeyDownFromInput(Keys.Down, ModifierKeys.None));
        Assert.True(editor.CaretIndex > caretBeforeDown);
    }

    [Fact]
    public void ShiftUpDownArrows_ShouldExtendSelectionAcrossLines()
    {
        var editor = CreateEditor("first\nsecond\nthird");

        SetSelection(editor, start: 8, length: 0);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Up, ModifierKeys.Shift));
        Assert.True(editor.SelectionLength > 0);
        var selectionAfterUp = editor.SelectionLength;

        Assert.True(editor.HandleKeyDownFromInput(Keys.Down, ModifierKeys.Shift));
        Assert.True(editor.SelectionLength < selectionAfterUp);
    }

    [Fact]
    public void ClickBlankParagraphThenEnter_ShouldMoveCaretAndKeepStructure()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildListThenTableDocument();

        var allText = DocumentEditing.GetText(editor.Document);
        var item2Start = allText.IndexOf("Item 2", StringComparison.Ordinal);
        Assert.True(item2Start >= 0);
        SetSelection(editor, item2Start + "Item 2".Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // create item 3
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None)); // exit list to empty paragraph

        var layout = BuildLayout(editor, 620f);
        var item2Line = Assert.Single(layout.Lines, static l => l.Text == "Item 2");
        var blankCandidates = layout.Lines.Where(l => l.Text.Length == 0 && l.Bounds.Y > item2Line.Bounds.Y).ToList();
        Assert.NotEmpty(blankCandidates);
        var blankLine = blankCandidates[0];
        var clickPoint = new Vector2(
            (1f + 8f) + blankLine.TextStartX + 1f,
            (1f + 5f) + blankLine.Bounds.Y + (blankLine.Bounds.Height * 0.5f));

        Assert.True(editor.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());

        var before = BuildLayout(editor, 620f);
        Assert.True(before.TryGetCaretPosition(editor.CaretIndex, out var beforePos));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        var after = BuildLayout(editor, 620f);
        Assert.True(after.TryGetCaretPosition(editor.CaretIndex, out var afterPos));
        Assert.True(afterPos.Y > beforePos.Y, $"Expected caret Y to increase after Enter. Before={beforePos.Y}, After={afterPos.Y}");

        var list = Assert.IsType<InkkSlinger.List>(editor.Document.Blocks[0]);
        Assert.Equal(2, list.Items.Count);
        Assert.IsType<Paragraph>(editor.Document.Blocks[1]);
        Assert.IsType<Paragraph>(editor.Document.Blocks[2]);
        Assert.IsType<Table>(editor.Document.Blocks[3]);
    }

    [Fact]
    public void ShiftTab_OutsideTable_ShouldNotDeleteText()
    {
        var editor = CreateEditor("abc");
        SetSelection(editor, start: 3, length: 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Tab, ModifierKeys.Shift));
        Assert.Equal("abc", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void EnterLineBreak_ShouldInsertLineBreakInline_NotParagraphSplit()
    {
        var editor = CreateEditor("abcd");
        SetSelection(editor, start: 2, length: 0);

        CommandManager.Execute(EditingCommands.EnterLineBreak, null, editor);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Contains(paragraph.Inlines, inline => inline is LineBreak);
        Assert.Equal("ab\ncd", DocumentEditing.GetText(editor.Document).Replace("\r\n", "\n"));
    }

    [Fact]
    public void EnterInParagraphContainingLineBreak_InRichStructuredDoc_ShouldSplitWithoutFallbackBlock()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildRichStructuredDocumentWithInlineLineBreak();

        var text = DocumentEditing.GetText(editor.Document);
        var splitOffset = text.IndexOf("beta", StringComparison.Ordinal);
        Assert.True(splitOffset > 0);
        SetSelection(editor, splitOffset, 0);
        var caretBefore = editor.CaretIndex;

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        Assert.True(editor.CaretIndex > caretBefore);
        Assert.Contains(editor.Document.Blocks, static b => b is InkkSlinger.List);
        Assert.Contains(editor.Document.Blocks, static b => b is Table);
    }

    [Fact]
    public void ToggleBold_WithCollapsedCaret_ShouldAffectSubsequentTyping()
    {
        var editor = CreateEditor("abc");
        SetSelection(editor, start: 3, length: 0);

        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);
        Assert.True(editor.HandleTextInputFromInput('x'));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Equal("abcx", DocumentEditing.GetText(editor.Document));
        Assert.IsType<Bold>(paragraph.Inlines[^1]);
    }

    [Fact]
    public void ToggleItalic_WithCollapsedCaret_ShouldAffectSubsequentTyping()
    {
        var editor = CreateEditor("abc");
        SetSelection(editor, start: 3, length: 0);

        CommandManager.Execute(EditingCommands.ToggleItalic, null, editor);
        Assert.True(editor.HandleTextInputFromInput('x'));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Equal("abcx", DocumentEditing.GetText(editor.Document));
        Assert.IsType<Italic>(paragraph.Inlines[^1]);
    }

    [Fact]
    public void ToggleUnderline_WithCollapsedCaret_ShouldAffectSubsequentTyping()
    {
        var editor = CreateEditor("abc");
        SetSelection(editor, start: 3, length: 0);

        CommandManager.Execute(EditingCommands.ToggleUnderline, null, editor);
        Assert.True(editor.HandleTextInputFromInput('x'));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Equal("abcx", DocumentEditing.GetText(editor.Document));
        Assert.IsType<Underline>(paragraph.Inlines[^1]);
    }

    [Fact]
    public void ToggleFormattingCommands_ShouldBeExecutableAtCollapsedCaret()
    {
        var editor = CreateEditor("abc");
        SetSelection(editor, start: 1, length: 0);

        Assert.True(CommandManager.CanExecute(EditingCommands.ToggleBold, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.ToggleItalic, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.ToggleUnderline, null, editor));
    }

    [Fact]
    public void SimpleTyping_AfterExistingFormatting_ShouldPreservePriorInlineStyles()
    {
        var editor = CreateEditorWithBoldPrefix("A", "B");
        SetSelection(editor, start: 2, length: 0);

        Assert.True(editor.HandleTextInputFromInput('x'));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.Contains(paragraph.Inlines, inline => inline is Bold);
        Assert.Equal("ABx", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void Copy_ShouldPublishStandardRichTextFormat_ForWpfInterop()
    {
        TextClipboard.ResetForTests();
        var editor = CreateEditor("copy me");
        SetSelection(editor, start: 0, length: 7);

        CommandManager.Execute(EditingCommands.Copy, null, editor);

        Assert.True(TextClipboard.TryGetData<string>("Rich Text Format", out var rtf));
        Assert.False(string.IsNullOrWhiteSpace(rtf));
    }

    [Fact]
    public void CtrlWordNavigation_ShouldTreatPunctuationAsBoundary()
    {
        var editor = CreateEditor("hello,world");
        SetSelection(editor, start: 0, length: 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Control));
        Assert.Equal(6, editor.CaretIndex);
    }

    [Fact]
    public void Hyperlink_ShouldNotNavigateOnSimpleClickWithoutModifier()
    {
        var editor = CreateEditor(string.Empty);
        editor.Document = BuildHyperlinkDocument("https://example.com", "site");
        SetSelection(editor, start: 0, length: 0);

        var hitCount = 0;
        editor.HyperlinkNavigate += (_, _) => hitCount++;

        Assert.True(editor.HandlePointerDownFromInput(new Microsoft.Xna.Framework.Vector2(10f, 10f), extendSelection: false));
        Assert.True(editor.HandlePointerUpFromInput());

        Assert.Equal(0, hitCount);
    }

    [Fact]
    public void ItalicStyle_ShouldNotAlterForegroundColor()
    {
        var editor = CreateEditor("text");
        var expected = new Microsoft.Xna.Framework.Color(25, 50, 75, 200);
        editor.Foreground = expected;

        var method = typeof(RichTextBox).GetMethod("ResolveRunColor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var style = new DocumentLayoutStyle(IsBold: false, IsItalic: true, IsUnderline: false, IsHyperlink: false, ForegroundOverride: null);
        var actual = (Microsoft.Xna.Framework.Color?)method!.Invoke(editor, [style]);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParagraphSelection_ShouldUseParagraphBoundaries_NotLineBreakCharacters()
    {
        var editor = CreateEditor(string.Empty);
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("ab"));
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Run("cd"));
        document.Blocks.Add(paragraph);
        editor.Document = document;

        var method = typeof(RichTextBox).GetMethod("SelectParagraphAt", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(editor, [3]);

        var all = DocumentEditing.GetText(editor.Document).Replace("\r\n", "\n");
        var selected = all.Substring(editor.SelectionStart, editor.SelectionLength);
        Assert.Equal(all, selected);
    }

    [Fact]
    public void RichPaste_AtParagraphStart_ShouldMergeIntoExistingParagraph_NotInsertExtraParagraph()
    {
        TextClipboard.ResetForTests();
        var editor = CreateEditor("hello");
        SetSelection(editor, start: 0, length: 0);

        var fragmentDoc = new FlowDocument();
        var fragmentParagraph = new Paragraph();
        fragmentParagraph.Inlines.Add(new Run("X"));
        fragmentDoc.Blocks.Add(fragmentParagraph);
        var xaml = FlowDocumentSerializer.Serialize(fragmentDoc);
        TextClipboard.SetData("Xaml", xaml);

        CommandManager.Execute(EditingCommands.Paste, null, editor);

        Assert.Equal("Xhello", DocumentEditing.GetText(editor.Document));
        Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
    }

    [Fact]
    public void TextCompositionCommit_ShouldInsertMultiCharacterStringAsSingleUndoUnit()
    {
        var editor = CreateEditor("ab");
        SetSelection(editor, start: 2, length: 0);

        Assert.True(editor.HandleTextCompositionFromInput("あい"));
        Assert.Equal("abあい", DocumentEditing.GetText(editor.Document));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control));
        Assert.Equal("ab", DocumentEditing.GetText(editor.Document));
    }

    [Fact]
    public void TextCompositionCommit_ShouldRespectCollapsedTypingFormatState()
    {
        var editor = CreateEditor("ab");
        SetSelection(editor, start: 2, length: 0);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        Assert.True(editor.HandleTextCompositionFromInput("xy"));
        Assert.Equal("abxy", DocumentEditing.GetText(editor.Document));

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.IsType<Bold>(paragraph.Inlines[^1]);
    }

    [Fact]
    public void RunForeground_ShouldOverrideControlForeground()
    {
        var editor = CreateEditor(string.Empty);
        editor.Foreground = new Microsoft.Xna.Framework.Color(255, 255, 255, 255);
        var run = new Run("x")
        {
            Foreground = new Microsoft.Xna.Framework.Color(10, 20, 30, 255)
        };
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(run);
        var doc = new FlowDocument();
        doc.Blocks.Add(paragraph);
        editor.Document = doc;

        var measure = typeof(RichTextBox).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(measure);
        _ = measure!.Invoke(editor, [new Microsoft.Xna.Framework.Vector2(400f, 120f)]);
        var build = typeof(RichTextBox).GetMethod("BuildOrGetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(build);
        var layout = (DocumentLayoutResult?)build!.Invoke(editor, [380f]);
        Assert.NotNull(layout);
        Assert.NotEmpty(layout!.Runs);
        var style = layout.Runs[0].Style;

        var resolve = typeof(RichTextBox).GetMethod("ResolveRunColor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(resolve);
        var color = (Microsoft.Xna.Framework.Color?)resolve!.Invoke(editor, [style]);
        Assert.Equal(new Microsoft.Xna.Framework.Color(10, 20, 30, 255), color);
    }

    [Fact]
    public void RunForeground_ShouldRoundTripThroughSerializer()
    {
        var doc = new FlowDocument();
        var paragraph = new Paragraph();
        var run = new Run("x")
        {
            Foreground = new Microsoft.Xna.Framework.Color(40, 80, 120, 200)
        };
        paragraph.Inlines.Add(run);
        doc.Blocks.Add(paragraph);

        var xml = FlowDocumentSerializer.Serialize(doc);
        var restored = FlowDocumentSerializer.Deserialize(xml);
        var restoredParagraph = Assert.IsType<Paragraph>(Assert.Single(restored.Blocks));
        var restoredRun = Assert.IsType<Run>(Assert.Single(restoredParagraph.Inlines));
        Assert.Equal(new Microsoft.Xna.Framework.Color(40, 80, 120, 200), restoredRun.Foreground);
    }

    [Fact]
    public void SetText_ShouldTreatLoneCarriageReturnAsParagraphBreak()
    {
        var doc = new FlowDocument();
        FlowDocumentPlainText.SetText(doc, "a\rb");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("a\nb", DocumentEditing.GetText(doc));
    }

    [Fact]
    public void ReadOnlyEditingKey_ShouldNotReportHandledWhenCommandCannotExecute()
    {
        var editor = CreateEditor("abc");
        editor.IsReadOnly = true;

        var handledBackspace = editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None);
        var handledDelete = editor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None);

        Assert.False(handledBackspace);
        Assert.False(handledDelete);
        Assert.Equal("abc", DocumentEditing.GetText(editor.Document));
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 420f, 120f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static RichTextBox CreateEditorWithBoldPrefix(string boldText, string normalText)
    {
        var editor = CreateEditor(string.Empty);
        var paragraph = new Paragraph();
        var bold = new Bold();
        bold.Inlines.Add(new Run(boldText));
        paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new Run(normalText));
        var document = new FlowDocument();
        document.Blocks.Add(paragraph);
        editor.Document = document;
        return editor;
    }

    private static FlowDocument BuildHyperlinkDocument(string uri, string text)
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        var hyperlink = new Hyperlink { NavigateUri = uri };
        hyperlink.Inlines.Add(new Run(text));
        paragraph.Inlines.Add(hyperlink);
        document.Blocks.Add(paragraph);
        return document;
    }

    private static FlowDocument BuildR1C1TableDocument()
    {
        var table = new Table();
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        for (var rowIndex = 1; rowIndex <= 2; rowIndex++)
        {
            var row = new TableRow();
            group.Rows.Add(row);
            for (var columnIndex = 1; columnIndex <= 2; columnIndex++)
            {
                var cell = new TableCell();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run($"R{rowIndex}C{columnIndex}"));
                cell.Blocks.Add(paragraph);
                row.Cells.Add(cell);
            }
        }

        var document = new FlowDocument();
        document.Blocks.Add(table);
        return document;
    }

    private static FlowDocument BuildTwoItemListDocument()
    {
        var list = new InkkSlinger.List();
        var item1 = new ListItem();
        var p1 = new Paragraph();
        p1.Inlines.Add(new Run("Item 1"));
        item1.Blocks.Add(p1);
        list.Items.Add(item1);

        var item2 = new ListItem();
        var p2 = new Paragraph();
        p2.Inlines.Add(new Run("Item 2"));
        item2.Blocks.Add(p2);
        list.Items.Add(item2);

        var document = new FlowDocument();
        document.Blocks.Add(list);
        return document;
    }

    private static FlowDocument BuildListThenTableDocument()
    {
        var document = BuildTwoItemListDocument();
        var tableDoc = BuildR1C1TableDocument();
        var tableClone = FlowDocumentSerializer.Deserialize(FlowDocumentSerializer.Serialize(tableDoc));
        var tableBlock = tableClone.Blocks[0];
        tableClone.Blocks.RemoveAt(0);
        document.Blocks.Add(tableBlock);
        return document;
    }

    private static FlowDocument BuildDiagnosticsLabDocument()
    {
        var document = new FlowDocument();

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("Use "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("Ctrl+Enter"));
        intro.Inlines.Add(bold);
        intro.Inlines.Add(new Run(" or click to activate "));
        var hyperlink = new Hyperlink { NavigateUri = "https://example.com/inkkslinger-richtext-lab" };
        hyperlink.Inlines.Add(new Run("this hyperlink"));
        intro.Inlines.Add(hyperlink);
        intro.Inlines.Add(new Run("."));
        document.Blocks.Add(intro);

        var list = new InkkSlinger.List { IsOrdered = true };
        var item1 = new ListItem();
        var p1 = new Paragraph();
        p1.Inlines.Add(new Run("List item 1"));
        item1.Blocks.Add(p1);
        list.Items.Add(item1);

        var item2 = new ListItem();
        var p2 = new Paragraph();
        p2.Inlines.Add(new Run("List item 2"));
        item2.Blocks.Add(p2);
        list.Items.Add(item2);
        document.Blocks.Add(list);

        var tableDoc = BuildR1C1TableDocument();
        var tableClone = FlowDocumentSerializer.Deserialize(FlowDocumentSerializer.Serialize(tableDoc));
        var tableBlock = tableClone.Blocks[0];
        tableClone.Blocks.RemoveAt(0);
        document.Blocks.Add(tableBlock);
        return document;
    }

    private static FlowDocument BuildRichStructuredDocumentWithInlineLineBreak()
    {
        var document = new FlowDocument();

        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("alpha"));
        paragraph.Inlines.Add(new LineBreak());
        var bold = new Bold();
        bold.Inlines.Add(new Run("beta"));
        paragraph.Inlines.Add(bold);
        document.Blocks.Add(paragraph);

        var list = new InkkSlinger.List { IsOrdered = true };
        var item = new ListItem();
        var itemParagraph = new Paragraph();
        itemParagraph.Inlines.Add(new Run("item 1"));
        item.Blocks.Add(itemParagraph);
        list.Items.Add(item);
        document.Blocks.Add(list);

        var tableDoc = BuildR1C1TableDocument();
        var tableClone = FlowDocumentSerializer.Deserialize(FlowDocumentSerializer.Serialize(tableDoc));
        var tableBlock = tableClone.Blocks[0];
        tableClone.Blocks.RemoveAt(0);
        document.Blocks.Add(tableBlock);
        return document;
    }

    private static void SetSelection(RichTextBox editor, int start, int length)
    {
        var anchorField = typeof(RichTextBox).GetField("_selectionAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
        var caretField = typeof(RichTextBox).GetField("_caretIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(anchorField);
        Assert.NotNull(caretField);
        anchorField!.SetValue(editor, start);
        caretField!.SetValue(editor, start + length);
    }

    private static void SetRawSelection(RichTextBox editor, int anchor, int caret)
    {
        var anchorField = typeof(RichTextBox).GetField("_selectionAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
        var caretField = typeof(RichTextBox).GetField("_caretIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(anchorField);
        Assert.NotNull(caretField);
        anchorField!.SetValue(editor, anchor);
        caretField!.SetValue(editor, caret);
    }

    private static DocumentLayoutResult BuildLayout(RichTextBox editor, float width)
    {
        var measure = typeof(RichTextBox).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(measure);
        _ = measure!.Invoke(editor, [new Vector2(width + 20f, 260f)]);

        var build = typeof(RichTextBox).GetMethod("BuildOrGetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(build);
        var layout = (DocumentLayoutResult?)build!.Invoke(editor, [width]);
        Assert.NotNull(layout);
        return layout!;
    }
}
