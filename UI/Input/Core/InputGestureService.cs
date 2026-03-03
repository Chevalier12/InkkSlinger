using System;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public static class InputGestureService
{
    [Obsolete("Imperative gesture registration is no longer supported. Use UIElement.InputBindings with KeyBinding.")]
    public static void Register(
        Keys key,
        ModifierKeys modifiers,
        RoutedCommand command,
        UIElement target,
        object? parameter = null)
    {
        throw new NotSupportedException(
            "Imperative gesture registration is disabled. Use declarative InputBindings/KeyBinding on UI elements.");
    }

    [Obsolete("Imperative gesture registration is no longer supported. Use UIElement.InputBindings with KeyBinding.")]
    public static void Clear()
    {
        throw new NotSupportedException(
            "Imperative gesture registration is disabled. Use declarative InputBindings/KeyBinding on UI elements.");
    }

    public static bool Execute(Keys key, ModifierKeys modifiers, UIElement? focusedElement, UIElement visualRoot)
    {
        return ExecuteMatchingBindings(
            static (binding, state) => binding is KeyBinding keyBinding && keyBinding.Matches(state.Key, state.Modifiers),
            (key, modifiers),
            focusedElement,
            visualRoot);
    }

    public static bool Execute(MouseButton button, ModifierKeys modifiers, UIElement? focusedElement, UIElement visualRoot)
    {
        return ExecuteMatchingBindings(
            static (binding, state) => binding is MouseBinding mouseBinding && mouseBinding.Matches(state.Button, state.Modifiers),
            (button, modifiers),
            focusedElement,
            visualRoot);
    }

    public static bool TryGetFirstGestureTextForCommand(
        System.Windows.Input.ICommand command,
        UIElement start,
        out string gestureText)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            for (var i = 0; i < current.InputBindings.Count; i++)
            {
                if (current.InputBindings[i] is not KeyBinding keyBinding)
                {
                    continue;
                }

                if (!ReferenceEquals(keyBinding.Command, command))
                {
                    continue;
                }

                gestureText = keyBinding.GetDisplayString();
                return true;
            }
        }

        gestureText = string.Empty;
        return false;
    }

    private static bool ExecuteMatchingBindings<TState>(
        Func<InputBinding, TState, bool> matcher,
        TState state,
        UIElement? focusedElement,
        UIElement visualRoot)
    {
        var start = focusedElement ?? visualRoot;
        var executed = false;

        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            for (var i = 0; i < current.InputBindings.Count; i++)
            {
                if (current.InputBindings[i] is not InputBinding binding || !matcher(binding, state))
                {
                    continue;
                }

                if (TryExecuteBinding(binding, focusedElement, current))
                {
                    executed = true;
                }
            }
        }

        return executed;
    }

    private static bool TryExecuteBinding(InputBinding binding, UIElement? focusedElement, UIElement owner)
    {
        var command = binding.Command;
        if (command == null)
        {
            return false;
        }

        var target = binding.CommandTarget ?? focusedElement ?? owner;

        if (command is RoutedCommand routedCommand)
        {
            if (!CommandManager.CanExecute(routedCommand, binding.CommandParameter, target))
            {
                return false;
            }

            CommandManager.Execute(routedCommand, binding.CommandParameter, target);
            return true;
        }

        if (!command.CanExecute(binding.CommandParameter))
        {
            return false;
        }

        command.Execute(binding.CommandParameter);
        return true;
    }
}
