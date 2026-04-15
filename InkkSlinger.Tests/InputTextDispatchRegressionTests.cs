using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InputTextDispatchRegressionTests
{
    [Fact]
    public void UiRoot_CtrlSpaceHandledByKeyRoute_ShouldNotDispatchSpaceTextInputToFocusedRichTextBox()
    {
        var root = new StackPanel();
        var editor = new RichTextBox
        {
            Width = 320f,
            Height = 120f,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap
        };
        DocumentEditing.ReplaceAllText(editor.Document, "<");

        var ctrlSpaceHandled = false;
        editor.AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key == Keys.Space && args.Modifiers == ModifierKeys.Control)
            {
                args.Handled = true;
                ctrlSpaceHandled = true;
            }
        });

        root.AddChild(editor);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 480, 240, 16);

        FocusByClick(uiRoot, editor);
        editor.Select(1, 0);

        var pointer = GetCenter(editor);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, pointer, textInput: [' '], heldModifiers: [Keys.LeftControl]));

        Assert.True(ctrlSpaceHandled);
        Assert.Equal("<", DocumentEditing.GetText(editor.Document));
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void UiRoot_CtrlBExecutedByInputGesture_ShouldNotDispatchTextInputToFocusedRichTextBox()
    {
        var root = new StackPanel();
        var editor = new RichTextBox
        {
            Width = 320f,
            Height = 120f,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap
        };
        DocumentEditing.ReplaceAllText(editor.Document, "<");

        var command = new RoutedCommand("ProbeGesture", typeof(InputTextDispatchRegressionTests));
        var commandExecuted = false;
        root.CommandBindings.Add(new CommandBinding(command, (_, _) => commandExecuted = true));
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.B,
            Modifiers = ModifierKeys.Control,
            Command = command
        });
        root.AddChild(editor);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 480, 240, 16);

        FocusByClick(uiRoot, editor);
        editor.Select(1, 0);

        var pointer = GetCenter(editor);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.B, pointer, textInput: ['b'], heldModifiers: [Keys.LeftControl]));

        Assert.True(commandExecuted);
        Assert.Equal("<", DocumentEditing.GetText(editor.Document));
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static void FocusByClick(UiRoot uiRoot, UIElement target)
    {
        var point = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
        Assert.Same(target, FocusManager.GetFocusedElement());
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreateKeyDownDelta(Keys key, Vector2 pointer, char[]? textInput = null, Keys[]? heldModifiers = null)
    {
        var keyboard = heldModifiers == null || heldModifiers.Length == 0
            ? new KeyboardState(key)
            : new KeyboardState(heldModifiers.Concat([key]).ToArray());

        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(heldModifiers ?? []), default, pointer),
            Current = new InputSnapshot(keyboard, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = textInput == null ? new List<char>() : new List<char>(textInput),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}