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
