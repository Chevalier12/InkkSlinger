using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextInputParityTests
{
    [Fact]
    public void WordAndParagraphSelectionGestures_Work()
    {
        var editor = CreateEditor(260f, 100f, "hello world\nnext line");

        var textLeft = 1f + 8f;
        var textTop = 1f + 5f;
        var point = new Vector2(textLeft + FontStashTextRenderer.MeasureWidth(null, "hello wo"), textTop + 2f);

        editor.HandlePointerDownFromInput(point, extendSelection: false);
        editor.HandlePointerUpFromInput();
        editor.HandlePointerDownFromInput(point, extendSelection: false);
        editor.HandlePointerUpFromInput();

        Assert.Equal("world", GetSelectedText(editor));

        editor.HandlePointerDownFromInput(point, extendSelection: false);
        editor.HandlePointerUpFromInput();

        Assert.Equal("hello world", GetSelectedText(editor));
    }

    [Fact]
    public void CtrlShiftWordNavigation_SelectsByWord()
    {
        var editor = CreateEditor(320f, 80f, "alpha beta gamma");

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Left, ModifierKeys.Control | ModifierKeys.Shift));

        Assert.Equal("gamma", GetSelectedText(editor));

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.None));
        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        Assert.Equal(DocumentEditing.GetText(editor.Document).Length, editor.CaretIndex);
    }

    [Fact]
    public void EnsureCaretVisible_AdjustsScrollOffsets()
    {
        var editor = CreateEditor(140f, 36f, string.Join("\n", BuildLines(20)));

        Assert.True(editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control));
        var verticalAfterEnd = GetPrivateFloat(editor, "_verticalOffset");
        Assert.True(verticalAfterEnd > 0f);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.Control));
        var verticalAfterHome = GetPrivateFloat(editor, "_verticalOffset");
        Assert.True(verticalAfterHome <= 0.01f);
    }

    [Fact]
    public void ReadOnlyBlocksMutationButAllowsSelectionAndCopyRange()
    {
        var editor = CreateEditor(260f, 80f, "abc def");
        editor.IsReadOnly = true;

        Assert.True(editor.HandleKeyDownFromInput(Keys.Right, ModifierKeys.Shift));
        Assert.True(editor.SelectionLength > 0);

        var before = DocumentEditing.GetText(editor.Document);
        Assert.False(editor.HandleTextInputFromInput('x'));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None));

        var after = DocumentEditing.GetText(editor.Document);
        Assert.Equal(before, after);
    }

    private static RichTextBox CreateEditor(float width, float height, string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, width, height));
        editor.SetFocusedFromInput(true);
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

    private static float GetPrivateFloat(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (float)(field!.GetValue(target) ?? 0f);
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
}
