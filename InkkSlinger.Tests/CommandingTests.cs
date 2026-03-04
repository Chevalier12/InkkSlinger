using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class CommandingTests
{
    public CommandingTests()
    {
        InputGestureService.Clear();
    }

    [Fact]
    public void Button_ImplementsICommandSource()
    {
        var button = new Button();
        Assert.IsAssignableFrom<ICommandSource>(button);
    }

    [Fact]
    public void Hyperlink_ImplementsICommandSource()
    {
        var hyperlink = new Hyperlink();
        Assert.IsAssignableFrom<ICommandSource>(hyperlink);
    }

    [Fact]
    public void Execute_UsesFocusedElementBindingsBeforeAncestors()
    {
        var root = new StackPanel();
        var child = new Button();
        root.AddChild(child);

        var executionOrder = new List<string>();
        child.InputBindings.Add(new KeyBinding
        {
            Key = Keys.N,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("focused"))
        });
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.N,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("ancestor"))
        });

        var executed = InputGestureService.Execute(Keys.N, ModifierKeys.Control, child, root);

        Assert.True(executed);
        Assert.Equal(new[] { "focused", "ancestor" }, executionOrder);
    }

    [Fact]
    public void Execute_UsesAncestorBindingWhenFocusedHasNone()
    {
        var root = new StackPanel();
        var child = new Button();
        root.AddChild(child);

        var executedCount = 0;
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.O,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, child, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void Execute_UsesExplicitCommandTargetWhenProvided()
    {
        var root = new StackPanel();
        var focused = new Button();
        var explicitTarget = new Button();
        root.AddChild(focused);
        root.AddChild(explicitTarget);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        var executedCount = 0;
        explicitTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.O,
            Modifiers = ModifierKeys.Control,
            Command = command,
            CommandTarget = explicitTarget
        });

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, focused, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void Execute_DoesNotShortCircuitWhenEarlierBindingCannotExecute()
    {
        var root = new StackPanel();
        var executedCount = 0;

        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.N,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => { }, _ => false)
        });
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.N,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var executed = InputGestureService.Execute(Keys.N, ModifierKeys.Control, null, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void ExecuteMouse_UsesFocusedElementBindingsBeforeAncestors()
    {
        var root = new StackPanel();
        var child = new Button();
        root.AddChild(child);

        var executionOrder = new List<string>();
        child.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("focused"))
        });
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("ancestor"))
        });

        var executed = InputGestureService.Execute(MouseButton.Left, ModifierKeys.Control, child, root);

        Assert.True(executed);
        Assert.Equal(new[] { "focused", "ancestor" }, executionOrder);
    }

    [Fact]
    public void ExecuteMouse_UsesAncestorBindingWhenFocusedHasNone()
    {
        var root = new StackPanel();
        var child = new Button();
        root.AddChild(child);

        var executedCount = 0;
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var executed = InputGestureService.Execute(MouseButton.Left, ModifierKeys.Control, child, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void ExecuteMouse_UsesExplicitCommandTargetWhenProvided()
    {
        var root = new StackPanel();
        var focused = new Button();
        var explicitTarget = new Button();
        root.AddChild(focused);
        root.AddChild(explicitTarget);

        var command = new RoutedCommand("OpenMouse", typeof(CommandingTests));
        var executedCount = 0;
        explicitTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = command,
            CommandTarget = explicitTarget
        });

        var executed = InputGestureService.Execute(MouseButton.Left, ModifierKeys.Control, focused, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void ExecuteMouse_DoesNotShortCircuitWhenEarlierBindingCannotExecute()
    {
        var root = new StackPanel();
        var executedCount = 0;

        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => { }, _ => false)
        });
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var executed = InputGestureService.Execute(MouseButton.Left, ModifierKeys.Control, null, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void Register_KeyGesture_ExecutesRoutedCommandOnTarget()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void Register_KeyGesture_WithParameter_ForwardsParameter()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        object? seenParameter = null;
        target.CommandBindings.Add(new CommandBinding(command, (_, args) => seenParameter = args.Parameter));

        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target, parameter: "payload");

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.True(executed);
        Assert.Equal("payload", seenParameter);
    }

    [Fact]
    public void Register_KeyGesture_CanExecuteFalse_DoesNotExecute()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(
            new CommandBinding(
                command,
                (_, _) => executedCount++,
                (_, args) => args.CanExecute = false));

        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.False(executed);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void Clear_RemovesImperativeKeyBindings()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target);
        InputGestureService.Clear();

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.False(executed);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void Execute_ImperativeKey_TargetOutsideVisualRoot_DoesNotExecute()
    {
        var root = new StackPanel();
        var otherRoot = new StackPanel();
        var target = new Button();
        otherRoot.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.False(executed);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void Register_MouseGesture_ExecutesRoutedCommandOnTarget()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("OpenMouse", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(MouseButton.Right, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(MouseButton.Right, ModifierKeys.Control, null, root);

        Assert.True(executed);
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void Register_MouseGesture_ModifierMismatch_DoesNotExecute()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("OpenMouse", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(MouseButton.Right, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(MouseButton.Right, ModifierKeys.None, null, root);

        Assert.False(executed);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void Clear_RemovesImperativeMouseBindings()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("OpenMouse", typeof(CommandingTests));
        var executedCount = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => executedCount++));

        InputGestureService.Register(MouseButton.Right, ModifierKeys.Control, command, target);
        InputGestureService.Clear();

        var executed = InputGestureService.Execute(MouseButton.Right, ModifierKeys.Control, null, root);

        Assert.False(executed);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void Execute_DeclarativeThenImperative_OrderIsDeterministic()
    {
        var root = new StackPanel();
        var focused = new Button();
        root.AddChild(focused);

        var executionOrder = new List<string>();
        focused.InputBindings.Add(new KeyBinding
        {
            Key = Keys.O,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("declarative"))
        });

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        focused.CommandBindings.Add(new CommandBinding(command, (_, _) => executionOrder.Add("imperative")));
        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, focused);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, focused, root);

        Assert.True(executed);
        Assert.Equal(new[] { "declarative", "imperative" }, executionOrder);
    }

    [Fact]
    public void Execute_ImperativeDoesNotBlockDeclarativeWhenCanExecuteFalse()
    {
        var root = new StackPanel();
        var focused = new Button();
        root.AddChild(focused);

        var executionOrder = new List<string>();
        focused.InputBindings.Add(new KeyBinding
        {
            Key = Keys.O,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => executionOrder.Add("declarative"))
        });

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        focused.CommandBindings.Add(
            new CommandBinding(
                command,
                (_, _) => executionOrder.Add("imperative"),
                (_, args) => args.CanExecute = false));
        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, focused);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, focused, root);

        Assert.True(executed);
        Assert.Equal(new[] { "declarative" }, executionOrder);
    }

    [Fact]
    public void Execute_ReturnsTrue_WhenOnlyImperativeExecutes()
    {
        var root = new StackPanel();
        var target = new Button();
        root.AddChild(target);

        var command = new RoutedCommand("Open", typeof(CommandingTests));
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => { }));
        InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target);

        var executed = InputGestureService.Execute(Keys.O, ModifierKeys.Control, null, root);

        Assert.True(executed);
    }

    [Fact]
    public void Register_Key_NullCommand_ThrowsArgumentNullException()
    {
        var target = new Button();

        Assert.Throws<ArgumentNullException>(() =>
            InputGestureService.Register(Keys.O, ModifierKeys.Control, command: null!, target));
    }

    [Fact]
    public void Register_Key_NullTarget_ThrowsArgumentNullException()
    {
        var command = new RoutedCommand("Open", typeof(CommandingTests));

        Assert.Throws<ArgumentNullException>(() =>
            InputGestureService.Register(Keys.O, ModifierKeys.Control, command, target: null!));
    }

    [Fact]
    public void Clear_IsIdempotent()
    {
        InputGestureService.Clear();
        InputGestureService.Clear();
    }

    [Fact]
    public void Register_StoresImperativeTargetAsWeakReference()
    {
        var registrationType = typeof(InputGestureService).GetNestedType("ImperativeRegistration", BindingFlags.NonPublic);
        Assert.NotNull(registrationType);
        var targetProperty = registrationType!.GetProperty("Target", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(targetProperty);
        Assert.Equal(typeof(WeakReference<UIElement>), targetProperty!.PropertyType);
    }

    [Fact]
    public void UiRootInputDelta_LeftPressed_TriggersMouseBindingCommand()
    {
        var root = new Grid();
        var executedCount = 0;
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Left,
            Modifiers = ModifierKeys.None,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var uiRoot = new UiRoot(root);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(10f, 10f), leftPressed: true));

        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void UiRootInputDelta_RightPressedWithModifiers_TriggersMatchingMouseBindingOnly()
    {
        var root = new Grid();
        var matchingExecutions = 0;
        var nonMatchingExecutions = 0;
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Right,
            Modifiers = ModifierKeys.Control,
            Command = new CallbackCommand(_ => matchingExecutions++)
        });
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Right,
            Modifiers = ModifierKeys.None,
            Command = new CallbackCommand(_ => nonMatchingExecutions++)
        });

        var keyboard = new KeyboardState(Keys.LeftControl);
        var uiRoot = new UiRoot(root);
        uiRoot.RunInputDeltaForTests(
            CreatePointerDelta(
                new Vector2(10f, 10f),
                rightPressed: true,
                previousKeyboard: keyboard,
                currentKeyboard: keyboard));

        Assert.Equal(1, matchingExecutions);
        Assert.Equal(0, nonMatchingExecutions);
    }

    [Fact]
    public void UiRootInputDelta_MiddlePressed_TriggersMouseBindingCommand()
    {
        var root = new Grid();
        var executedCount = 0;
        root.InputBindings.Add(new MouseBinding
        {
            Button = MouseButton.Middle,
            Modifiers = ModifierKeys.None,
            Command = new CallbackCommand(_ => executedCount++)
        });

        var uiRoot = new UiRoot(root);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(10f, 10f), middlePressed: true));

        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void MenuItem_AutoDerivesInputGestureText_WhenUnset()
    {
        var root = new StackPanel();
        var command = new RoutedCommand("New", typeof(CommandingTests));
        var item = new MenuItem { Header = "_New", Command = command };
        root.AddChild(item);
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.N,
            Modifiers = ModifierKeys.Control,
            Command = command
        });

        var effectiveText = InvokeEffectiveGestureText(item);

        Assert.Equal("Ctrl+N", effectiveText);
    }

    [Fact]
    public void MenuItem_ExplicitInputGestureText_WinsOverDerivedText()
    {
        var root = new StackPanel();
        var command = new RoutedCommand("Save", typeof(CommandingTests));
        var item = new MenuItem { Header = "_Save", Command = command, InputGestureText = "Manual" };
        root.AddChild(item);
        root.InputBindings.Add(new KeyBinding
        {
            Key = Keys.S,
            Modifiers = ModifierKeys.Control,
            Command = command
        });

        var effectiveText = InvokeEffectiveGestureText(item);

        Assert.Equal("Manual", effectiveText);
    }

    [Fact]
    public void MenuItem_KeepsInputGestureTextEmpty_WhenNoBindingMatches()
    {
        var root = new StackPanel();
        var command = new RoutedCommand("Exit", typeof(CommandingTests));
        var item = new MenuItem { Header = "E_xit", Command = command };
        root.AddChild(item);

        var effectiveText = InvokeEffectiveGestureText(item);

        Assert.Equal(string.Empty, effectiveText);
    }

    [Fact]
    public void MenuItem_CommandInvocation_RaisesClickAndExecutesCommand()
    {
        var root = new StackPanel();
        var target = new TextBox();
        var item = new MenuItem();
        root.AddChild(target);
        root.AddChild(item);

        var command = new RoutedCommand("ProbeMenuItem", typeof(CommandingTests));
        var commandExecutions = 0;
        var clickExecutions = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => commandExecutions++));
        item.Click += (_, _) => clickExecutions++;
        item.Command = command;
        item.CommandTarget = target;

        var invoked = InvokeLeaf(item);

        Assert.True(invoked);
        Assert.Equal(1, clickExecutions);
        Assert.Equal(1, commandExecutions);
    }

    [Fact]
    public void MenuItem_CommandTarget_WhenNull_FallsBackToFocusedElement()
    {
        FocusManager.ClearFocus();
        try
        {
            var source = new MenuItem();
            var focusedTarget = new TextBox();
            var command = new RoutedCommand("ProbeMenuItem", typeof(CommandingTests));
            var executedOnFocused = 0;

            focusedTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => executedOnFocused++,
                    (_, args) => args.CanExecute = true));

            source.Command = command;
            FocusManager.SetFocus(focusedTarget);

            var invoked = InvokeLeaf(source);

            Assert.True(invoked);
            Assert.Equal(1, executedOnFocused);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void MenuItem_CommandTarget_ExplicitTarget_WinsOverFocusedFallback()
    {
        FocusManager.ClearFocus();
        try
        {
            var source = new MenuItem();
            var focusedTarget = new TextBox();
            var explicitTarget = new TextBox();
            var command = new RoutedCommand("ProbeMenuItem", typeof(CommandingTests));
            var focusedExecutions = 0;
            var explicitExecutions = 0;

            focusedTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => focusedExecutions++,
                    (_, args) => args.CanExecute = true));
            explicitTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => explicitExecutions++,
                    (_, args) => args.CanExecute = true));

            source.Command = command;
            source.CommandTarget = explicitTarget;
            FocusManager.SetFocus(focusedTarget);

            var invoked = InvokeLeaf(source);

            Assert.True(invoked);
            Assert.Equal(0, focusedExecutions);
            Assert.Equal(1, explicitExecutions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void MenuItem_CommandCannotExecute_ClickStillRaised_CommandNotExecuted()
    {
        var source = new MenuItem();
        var target = new TextBox();
        var command = new RoutedCommand("ProbeMenuItem", typeof(CommandingTests));
        var clickExecutions = 0;
        var commandExecutions = 0;

        target.CommandBindings.Add(
            new CommandBinding(
                command,
                (_, _) => commandExecutions++,
                (_, args) => args.CanExecute = false));

        source.Click += (_, _) => clickExecutions++;
        source.Command = command;
        source.CommandTarget = target;

        var invoked = InvokeLeaf(source);

        Assert.False(invoked);
        Assert.Equal(1, clickExecutions);
        Assert.Equal(0, commandExecutions);
    }

    [Fact]
    public void ButtonCommandTarget_WhenNull_FallsBackToFocusedElement()
    {
        FocusManager.ClearFocus();
        try
        {
            var source = new Button { Text = "Source" };
            var focusedTarget = new TextBox();
            var command = new RoutedCommand("Probe", typeof(CommandingTests));
            var executedOnFocused = 0;

            focusedTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => executedOnFocused++,
                    (_, args) => args.CanExecute = true));

            source.Command = command;
            FocusManager.SetFocus(focusedTarget);

            var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onClick);
            onClick!.Invoke(source, null);

            Assert.Equal(1, executedOnFocused);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void ButtonCommandTarget_ExplicitTarget_WinsOverFocusedFallback()
    {
        FocusManager.ClearFocus();
        try
        {
            var source = new Button { Text = "Source" };
            var focusedTarget = new TextBox();
            var explicitTarget = new TextBox();
            var command = new RoutedCommand("Probe", typeof(CommandingTests));
            var focusedExecutions = 0;
            var explicitExecutions = 0;

            focusedTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => focusedExecutions++,
                    (_, args) => args.CanExecute = true));
            explicitTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => explicitExecutions++,
                    (_, args) => args.CanExecute = true));

            source.Command = command;
            source.CommandTarget = explicitTarget;
            FocusManager.SetFocus(focusedTarget);

            var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onClick);
            onClick!.Invoke(source, null);

            Assert.Equal(0, focusedExecutions);
            Assert.Equal(1, explicitExecutions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void RoutedCommand_ManualInvalidateRequerySuggested_RefreshesCommandSources()
    {
        FocusManager.ClearFocus();
        try
        {
            var root = new Grid();
            var target = new TextBox();
            var source = new Button();
            var command = new RoutedCommand("Probe", typeof(CommandingTests));
            var canExecute = true;

            target.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => { },
                    (_, args) => args.CanExecute = canExecute));

            root.AddChild(target);
            root.AddChild(source);
            FocusManager.SetFocus(target);

            source.Command = command;
            Assert.True(source.IsEnabled);

            canExecute = false;
            CommandManager.InvalidateRequerySuggested();
            Assert.False(source.IsEnabled);

            canExecute = true;
            CommandManager.InvalidateRequerySuggested();
            Assert.True(source.IsEnabled);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void RoutedCommand_FocusChange_RequeriesAcrossMultipleSources()
    {
        FocusManager.ClearFocus();
        try
        {
            var root = new Grid();
            var leftTarget = new TextBox();
            var rightTarget = new TextBox();
            var sourceA = new Button();
            var sourceB = new Button();
            var command = new RoutedCommand("Probe", typeof(CommandingTests));

            leftTarget.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => { },
                    (_, args) => args.CanExecute = ReferenceEquals(args.Target, leftTarget)));

            root.AddChild(leftTarget);
            root.AddChild(rightTarget);
            root.AddChild(sourceA);
            root.AddChild(sourceB);

            sourceA.Command = command;
            sourceB.Command = command;

            FocusManager.SetFocus(leftTarget);
            TriggerInputRequery(root);
            Assert.True(sourceA.IsEnabled);
            Assert.True(sourceB.IsEnabled);

            FocusManager.SetFocus(rightTarget);
            TriggerInputRequery(root);
            Assert.False(sourceA.IsEnabled);
            Assert.False(sourceB.IsEnabled);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void RoutedCommand_InputTriggeredRequery_RefreshesCommandSourcesOnStateChange()
    {
        FocusManager.ClearFocus();
        try
        {
            var root = new Grid();
            var target = new TextBox();
            var source = new Button();
            var command = new RoutedCommand("Probe", typeof(CommandingTests));
            var canExecute = true;

            target.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => { },
                    (_, args) => args.CanExecute = canExecute));

            root.AddChild(target);
            root.AddChild(source);
            source.Command = command;
            FocusManager.SetFocus(target);

            TriggerInputRequery(root);
            Assert.True(source.IsEnabled);

            canExecute = false;
            TriggerInputRequery(root);
            Assert.False(source.IsEnabled);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void MenuItem_UsesRoutedUICommandText_WhenHeaderMissing()
    {
        var item = new MenuItem
        {
            Command = new RoutedUICommand(text: "_Run", name: "Run", ownerType: typeof(CommandingTests))
        };

        var displayHeader = InvokeDisplayHeaderText(item);
        Assert.Equal("Run", displayHeader);
    }

    [Fact]
    public void XamlBinding_ButtonCommand_FromDataContext_ExecutesWithoutParserSpecialCase()
    {
        var viewModel = new CommandHostViewModel();
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button x:Name="Action" Command="{Binding Execute}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        root.DataContext = viewModel;
        var button = Assert.IsType<Button>(root.FindName("Action"));

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 180, 16);
        var clickPoint = new Vector2(button.LayoutSlot.X + 8f, button.LayoutSlot.Y + 8f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftPressed: true, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftReleased: true));

        Assert.Equal(1, viewModel.ExecutionCount);
    }

    [Fact]
    public void RoutedUICommand_Xaml_MenuHeaderAndGestureAndExecution_IntegrateEndToEnd()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <RoutedUICommand x:Key="OpenCommand" Text="_Open" />
  </UserControl.Resources>
  <Grid x:Name="Root">
    <UIElement.InputBindings>
      <KeyBinding Key="O" Modifiers="Control" Command="{StaticResource OpenCommand}" />
    </UIElement.InputBindings>
    <Menu>
      <MenuItem x:Name="OpenItem" Command="{StaticResource OpenCommand}" />
    </Menu>
    <Button x:Name="ExecuteButton" Command="{StaticResource OpenCommand}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var grid = Assert.IsType<Grid>(root.FindName("Root"));
        var openItem = Assert.IsType<MenuItem>(root.FindName("OpenItem"));
        var executeButton = Assert.IsType<Button>(root.FindName("ExecuteButton"));
        var command = Assert.IsType<RoutedUICommand>(openItem.Command);
        var executionCount = 0;
        grid.CommandBindings.Add(new CommandBinding(command, (_, _) => executionCount++));

        Assert.Equal("Open", InvokeDisplayHeaderText(openItem));
        Assert.Equal("Ctrl+O", InvokeEffectiveGestureText(openItem));

        var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        onClick!.Invoke(executeButton, null);

        Assert.Equal(1, executionCount);
    }

    private static string InvokeEffectiveGestureText(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("GetEffectiveInputGestureText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(item, null)!;
    }

    private static string InvokeDisplayHeaderText(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("GetDisplayHeaderText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(item, null)!;
    }

    private static bool InvokeLeaf(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("InvokeLeaf", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method!.Invoke(item, null)!;
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool leftPressed = false,
        bool leftReleased = false,
        bool rightPressed = false,
        bool rightReleased = false,
        bool middlePressed = false,
        bool middleReleased = false,
        KeyboardState? previousKeyboard = null,
        KeyboardState? currentKeyboard = null,
        bool pointerMoved = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(previousKeyboard ?? default, default, pointer),
            Current = new InputSnapshot(currentKeyboard ?? default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = middlePressed,
            MiddleReleased = middleReleased
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static void TriggerInputRequery(UIElement root)
    {
        var uiRoot = new UiRoot(root);
        uiRoot.RunInputDeltaForTests(
            new InputDelta
            {
                Previous = new InputSnapshot(default, default, Vector2.Zero),
                Current = new InputSnapshot(default, default, Vector2.Zero),
                PressedKeys = new List<Keys> { Keys.F1 },
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
            });
    }

    private sealed class CommandHostViewModel
    {
        public CallbackCommand Execute { get; }

        public int ExecutionCount { get; private set; }

        public CommandHostViewModel()
        {
            Execute = new CallbackCommand(_ => ExecutionCount++);
        }
    }
}
