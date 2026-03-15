using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AccessKeyRoutingTests
{
    [Fact]
    public void AltS_InvokesButtonThroughAccessTextTargetName()
    {
        var executions = 0;
        var (uiRoot, root) = CreateRoot();
        var button = new Button
        {
            Name = "SaveButton",
            Content = "Save"
        };
        button.Click += (_, _) => executions++;
        var accessText = new AccessText
        {
            Text = "_Save",
            TargetName = "SaveButton"
        };
        root.AddChild(accessText);
        root.AddChild(button);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(1, executions);
    }

    [Fact]
    public void DisabledTarget_DoesNotInvoke()
    {
        var executions = 0;
        var (uiRoot, root) = CreateRoot();
        var button = new Button
        {
            Name = "SaveButton",
            Content = "Save",
            IsEnabled = false
        };
        button.Click += (_, _) => executions++;
        root.AddChild(new AccessText
        {
            Text = "_Save",
            TargetName = "SaveButton"
        });
        root.AddChild(button);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(0, executions);
    }

    [Fact]
    public void ExplicitTargetName_BypassesRecognizesAccessKeyRequirement()
    {
        var executions = 0;
        var (uiRoot, root) = CreateRoot();
        var button = new Button
        {
            Name = "SaveButton",
            Content = "Save",
            RecognizesAccessKey = false
        };
        button.Click += (_, _) => executions++;
        root.AddChild(new AccessText
        {
            Text = "_Save",
            TargetName = "SaveButton"
        });
        root.AddChild(button);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(1, executions);
    }

    [Fact]
    public void MultipleMatches_InvokeFirstResolvedTargetOnly()
    {
        var firstExecutions = 0;
        var secondExecutions = 0;
        var (uiRoot, root) = CreateRoot();

        var firstPanel = new StackPanel();
        firstPanel.AddChild(new AccessText { Text = "_Save", TargetName = "FirstButton" });
        var firstButton = new Button { Name = "FirstButton", Content = "First" };
        firstButton.Click += (_, _) => firstExecutions++;
        firstPanel.AddChild(firstButton);

        var secondPanel = new StackPanel();
        secondPanel.AddChild(new AccessText { Text = "_Save", TargetName = "SecondButton" });
        var secondButton = new Button { Name = "SecondButton", Content = "Second" };
        secondButton.Click += (_, _) => secondExecutions++;
        secondPanel.AddChild(secondButton);

        root.AddChild(firstPanel);
        root.AddChild(secondPanel);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(1, firstExecutions);
        Assert.Equal(0, secondExecutions);
    }

    [Fact]
    public void MenuPrecedence_ConsumesAltKeyBeforeAccessText()
    {
        var executions = 0;
        var (uiRoot, root) = CreateRoot();

        var menu = new Menu();
        var saveMenu = new MenuItem { Header = "_Save" };
        saveMenu.Items.Add(new MenuItem { Header = "_As" });
        menu.Items.Add(saveMenu);
        root.AddChild(menu);

        var button = new Button { Name = "SaveButton", Content = "Save" };
        button.Click += (_, _) => executions++;
        root.AddChild(new AccessText { Text = "_Save", TargetName = "SaveButton" });
        root.AddChild(button);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.True(menu.IsMenuMode);
        Assert.True(saveMenu.IsSubmenuOpen);
        Assert.Equal(0, executions);
    }

    [Fact]
    public void MenuMiss_FallsBackToAccessTextRouting()
    {
        var executions = 0;
        var (uiRoot, root) = CreateRoot();

        var menu = new Menu();
        menu.Items.Add(new MenuItem { Header = "_File" });
        root.AddChild(menu);

        var button = new Button { Name = "SaveButton", Content = "Save" };
        button.Click += (_, _) => executions++;
        root.AddChild(new AccessText { Text = "_Save", TargetName = "SaveButton" });
        root.AddChild(button);
        RunLayout(uiRoot, 800, 400, 16);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.False(menu.IsMenuMode);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void ConflictingAccessKeys_PrioritizeFocusedElementAncestorRoute()
    {
        var firstExecutions = 0;
        var secondExecutions = 0;
        var (uiRoot, root) = CreateRoot();

        var firstContainer = new StackPanel();
        firstContainer.AddChild(new AccessText { Text = "_Save", TargetName = "FirstButton" });
        var firstButton = new Button { Name = "FirstButton", Content = "First" };
        firstButton.Click += (_, _) => firstExecutions++;
        firstContainer.AddChild(firstButton);
        root.AddChild(firstContainer);

        var secondContainer = new StackPanel();
        secondContainer.AddChild(new AccessText { Text = "_Save", TargetName = "SecondButton" });
        var secondButton = new Button { Name = "SecondButton", Content = "Second" };
        secondButton.Click += (_, _) => secondExecutions++;
        secondContainer.AddChild(secondButton);
        root.AddChild(secondContainer);

        RunLayout(uiRoot, 800, 400, 16);
        FocusByClick(uiRoot, secondButton);
        firstExecutions = 0;
        secondExecutions = 0;

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(0, firstExecutions);
        Assert.Equal(1, secondExecutions);
    }

    private static (UiRoot UiRoot, StackPanel Root) CreateRoot()
    {
        var root = new StackPanel();
        return (new UiRoot(root), root);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var state = keyboard ?? default;
        var pointer = new Vector2(12f, 12f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(state, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
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

    private static void FocusByClick(UiRoot uiRoot, UIElement target)
    {
        Click(uiRoot, target);
        Assert.Same(target, FocusManager.GetFocusedElement());
    }

    private static void Click(UiRoot uiRoot, UIElement target)
    {
        var point = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
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
}
