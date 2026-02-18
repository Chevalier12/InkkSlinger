using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextCommandingTests
{
    [Fact]
    public void CanExecute_ReflectsSelectionAndReadOnlyStates()
    {
        var editor = CreateEditor("alpha beta");

        Assert.False(CommandManager.CanExecute(EditingCommands.Cut, null, editor));
        Assert.False(CommandManager.CanExecute(EditingCommands.Copy, null, editor));

        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        Assert.True(CommandManager.CanExecute(EditingCommands.Copy, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.Cut, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.ToggleBold, null, editor));

        editor.IsReadOnly = true;
        Assert.False(CommandManager.CanExecute(EditingCommands.Cut, null, editor));
        Assert.False(CommandManager.CanExecute(EditingCommands.ToggleBold, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.Copy, null, editor));
    }

    [Fact]
    public void EditingCommands_ExecuteMutatesExpectedDocumentParts()
    {
        var editor = CreateEditor("hello world");
        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.ToggleBold, null, editor);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(editor.Document.Blocks));
        Assert.IsType<Bold>(Assert.Single(paragraph.Inlines));

        editor.SetFocusedFromInput(true);
        editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.EnterParagraphBreak, null, editor);

        var text = DocumentEditing.GetText(editor.Document);
        Assert.EndsWith("\n", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void KeyGesture_InvokesCommandThroughRoutedSystem()
    {
        var editor = CreateEditor("abc");
        editor.HandleKeyDownFromInput(Keys.End, ModifierKeys.Control);

        var handled = editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None);

        Assert.True(handled);
        Assert.Equal("ab", DocumentEditing.GetText(editor.Document));
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }
}
