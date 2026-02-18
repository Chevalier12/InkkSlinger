using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class CommandingTests
{
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

    private static string InvokeEffectiveGestureText(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("GetEffectiveInputGestureText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(item, null)!;
    }

    private sealed class CallbackCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;

        public CallbackCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute ?? (_ => true);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
