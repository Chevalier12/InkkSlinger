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
        Assert.False(CommandManager.CanExecute(EditingCommands.DeletePreviousWord, null, editor));

        editor.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        Assert.True(CommandManager.CanExecute(EditingCommands.Copy, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.Cut, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.ToggleBold, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.DeletePreviousWord, null, editor));
        Assert.True(CommandManager.CanExecute(EditingCommands.DeleteNextWord, null, editor));

        editor.IsReadOnly = true;
        Assert.False(CommandManager.CanExecute(EditingCommands.Cut, null, editor));
        Assert.False(CommandManager.CanExecute(EditingCommands.ToggleBold, null, editor));
        Assert.False(CommandManager.CanExecute(EditingCommands.DeletePreviousWord, null, editor));
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

    [Fact]
    public void NavigationCommands_ExecuteThroughCommandManager()
    {
        var editor = CreateEditor("alpha beta\ngamma");
        editor.Select(8, 0);

        CommandManager.Execute(EditingCommands.MoveToLineStart, null, editor);
        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);

        CommandManager.Execute(EditingCommands.MoveToLineEnd, null, editor);
        Assert.Equal(10, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);

        CommandManager.Execute(EditingCommands.MoveToDocumentEnd, null, editor);
        Assert.Equal("alpha beta\ngamma".Length, editor.CaretIndex);

        CommandManager.Execute(EditingCommands.MoveToDocumentStart, null, editor);
        Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void PageNavigationCommands_ExecuteThroughCommandManager()
    {
        var editor = CreateEditor("line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9");
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 72f));
        editor.Select(0, 0);

        CommandManager.Execute(EditingCommands.MoveDownByPage, null, editor);
        Assert.True(editor.CaretIndex > 0);

        var movedCaret = editor.CaretIndex;
        CommandManager.Execute(EditingCommands.SelectUpByPage, null, editor);
        Assert.Equal(0, editor.SelectionStart);
        Assert.True(editor.SelectionLength >= movedCaret);
        Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void ParagraphNavigationCommands_ExecuteThroughCommandManager()
    {
        var editor = CreateEditor("alpha\nbeta\ngamma");
        editor.Select(8, 0);

        CommandManager.Execute(EditingCommands.MoveToParagraphStart, null, editor);
        Assert.Equal(6, editor.CaretIndex);

        CommandManager.Execute(EditingCommands.MoveToParagraphEnd, null, editor);
        Assert.Equal(10, editor.CaretIndex);

        CommandManager.Execute(EditingCommands.MoveDownByParagraph, null, editor);
        Assert.Equal(11, editor.CaretIndex);

        CommandManager.Execute(EditingCommands.MoveUpByParagraph, null, editor);
        Assert.Equal(6, editor.CaretIndex);
    }

    [Fact]
    public void SelectionNavigationCommands_ShouldExtendSelection()
    {
        var editor = CreateEditor("alpha beta\ngamma");
        editor.Select(8, 0);

        CommandManager.Execute(EditingCommands.SelectToLineStart, null, editor);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(8, editor.SelectionLength);
        Assert.Equal(0, editor.CaretIndex);

        editor.Select(6, 0);
        CommandManager.Execute(EditingCommands.SelectToDocumentEnd, null, editor);
        Assert.Equal(6, editor.SelectionStart);
        Assert.Equal("alpha beta\ngamma".Length - 6, editor.SelectionLength);
        Assert.Equal("alpha beta\ngamma".Length, editor.CaretIndex);
    }

    [Fact]
    public void CharacterAndWordSelectionCommands_ShouldExtendSelection()
    {
        var editor = CreateEditor("alpha beta");
        editor.Select(6, 0);

        CommandManager.Execute(EditingCommands.SelectLeftByCharacter, null, editor);
        Assert.Equal(5, editor.SelectionStart);
        Assert.Equal(1, editor.SelectionLength);
        Assert.Equal(5, editor.CaretIndex);

        editor.Select(6, 0);
        CommandManager.Execute(EditingCommands.SelectRightByWord, null, editor);
        Assert.Equal(6, editor.SelectionStart);
        Assert.Equal(4, editor.SelectionLength);
        Assert.Equal(10, editor.CaretIndex);
    }

    [Fact]
    public void WordDeletionCommands_ExecuteThroughCommandManager()
    {
        var deletePreviousEditor = CreateEditor("alpha beta gamma");
        deletePreviousEditor.Select(11, 0);

        CommandManager.Execute(EditingCommands.DeletePreviousWord, null, deletePreviousEditor);

        Assert.Equal("alpha gamma", DocumentEditing.GetText(deletePreviousEditor.Document));
        Assert.Equal(6, deletePreviousEditor.CaretIndex);

        var deleteNextEditor = CreateEditor("alpha beta gamma");
        deleteNextEditor.Select(5, 0);

        CommandManager.Execute(EditingCommands.DeleteNextWord, null, deleteNextEditor);

        Assert.Equal("alpha gamma", DocumentEditing.GetText(deleteNextEditor.Document));
        Assert.Equal(5, deleteNextEditor.CaretIndex);
    }

    [Fact]
    public void HomeEndAndVerticalKeys_RouteThroughNavigationCommands()
    {
        var editor = CreateEditor("alpha\ngamma");
        editor.Select(3, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Down, ModifierKeys.None));
        Assert.True(editor.CaretIndex > "alpha\n".Length - 1);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.None));
        Assert.Equal("alpha\n".Length, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Home, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal(0, editor.SelectionStart);
        Assert.True(editor.SelectionLength > 0);
        Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void CtrlVerticalKeys_RouteThroughParagraphCommands()
    {
        var editor = CreateEditor("alpha\nbeta\ngamma");
        editor.Select(8, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Down, ModifierKeys.Control));
        Assert.Equal(11, editor.CaretIndex);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Up, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal(6, editor.SelectionStart);
        Assert.Equal(5, editor.SelectionLength);
        Assert.Equal(6, editor.CaretIndex);
    }

    [Fact]
    public void CtrlBackspaceAndCtrlDelete_RouteThroughWordDeletionCommands()
    {
        var deletePreviousEditor = CreateEditor("alpha beta gamma");
        deletePreviousEditor.Select(11, 0);

        Assert.True(deletePreviousEditor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.Control));
        Assert.Equal("alpha gamma", DocumentEditing.GetText(deletePreviousEditor.Document));

        var deleteNextEditor = CreateEditor("alpha beta gamma");
        deleteNextEditor.Select(5, 0);

        Assert.True(deleteNextEditor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.Control));
        Assert.Equal("alpha gamma", DocumentEditing.GetText(deleteNextEditor.Document));
    }

    [Fact]
    public void PageUpAndPageDown_RouteThroughPageNavigationCommands()
    {
        var editor = CreateEditor("line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9");
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 72f));
        editor.Select(0, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.PageDown, ModifierKeys.None));
        var movedCaret = editor.CaretIndex;
        Assert.True(movedCaret > 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.PageUp, ModifierKeys.Shift));
        Assert.Equal(0, editor.SelectionStart);
        Assert.True(editor.SelectionLength >= movedCaret);
        Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void HyperlinkCommand_Activation_ExecutesBoundCommand()
    {
        var executions = 0;
        var command = new CallbackCommand(_ => executions++);
        var editor = new RichTextBox();
        var hyperlink = new Hyperlink
        {
            Command = command
        };

        var activationMethod = typeof(RichTextBox).GetMethod("TryActivateHyperlink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(activationMethod);
        var handled = (bool)activationMethod!.Invoke(editor, new object[] { hyperlink })!;

        Assert.True(handled);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void HyperlinkCommand_RoutedCommand_UsesFocusedFallbackTarget()
    {
        FocusManager.ClearFocus();
        try
        {
            var editor = new RichTextBox();
            var target = new TextBox();
            var command = new RoutedCommand("Probe", typeof(RichTextCommandingTests));
            var executions = 0;

            target.CommandBindings.Add(new CommandBinding(command, (_, _) => executions++));
            FocusManager.SetFocus(target);

            var hyperlink = new Hyperlink
            {
                Command = command
            };

            var handled = InvokeHyperlinkActivation(editor, hyperlink);

            Assert.True(handled);
            Assert.Equal(1, executions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void HyperlinkCommand_RoutedCommand_ExplicitTarget_Wins()
    {
        FocusManager.ClearFocus();
        try
        {
            var editor = new RichTextBox();
            var focusedTarget = new TextBox();
            var explicitTarget = new TextBox();
            var command = new RoutedCommand("Probe", typeof(RichTextCommandingTests));
            var focusedExecutions = 0;
            var explicitExecutions = 0;

            focusedTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => focusedExecutions++));
            explicitTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => explicitExecutions++));
            FocusManager.SetFocus(focusedTarget);

            var hyperlink = new Hyperlink
            {
                Command = command,
                CommandTarget = explicitTarget
            };

            var handled = InvokeHyperlinkActivation(editor, hyperlink);

            Assert.True(handled);
            Assert.Equal(0, focusedExecutions);
            Assert.Equal(1, explicitExecutions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static bool InvokeHyperlinkActivation(RichTextBox editor, Hyperlink hyperlink)
    {
        var activationMethod = typeof(RichTextBox).GetMethod("TryActivateHyperlink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(activationMethod);
        return (bool)activationMethod!.Invoke(editor, new object[] { hyperlink })!;
    }

}
