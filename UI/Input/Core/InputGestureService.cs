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
        var start = focusedElement ?? visualRoot;
        var executed = false;

        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            for (var i = 0; i < current.InputBindings.Count; i++)
            {
                if (current.InputBindings[i] is not KeyBinding binding || !binding.Matches(key, modifiers))
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

    private static bool TryExecuteBinding(KeyBinding binding, UIElement? focusedElement, UIElement owner)
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
