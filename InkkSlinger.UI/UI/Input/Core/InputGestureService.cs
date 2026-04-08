using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public static class InputGestureService
{
    public static void Register(
        Keys key,
        ModifierKeys modifiers,
        RoutedCommand command,
        UIElement target,
        object? parameter = null)
    {
        Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        _imperativeRegistrations.Add(
            ImperativeRegistration.ForKey(key, modifiers, command, target, parameter));
    }

    public static void Register(
        MouseButton button,
        ModifierKeys modifiers,
        RoutedCommand command,
        UIElement target,
        object? parameter = null)
    {
        Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        _imperativeRegistrations.Add(
            ImperativeRegistration.ForMouse(button, modifiers, command, target, parameter));
    }

    public static void Clear()
    {
        Dispatcher.VerifyAccess();
        _imperativeRegistrations.Clear();
    }

    public static bool Execute(Keys key, ModifierKeys modifiers, UIElement? focusedElement, UIElement visualRoot)
    {
        var executed = ExecuteMatchingBindings(
            static (binding, state) => binding is KeyBinding keyBinding && keyBinding.Matches(state.key, state.modifiers),
            (key, modifiers),
            focusedElement,
            visualRoot);
        return ExecuteMatchingImperativeKeyRegistrations(key, modifiers, visualRoot) || executed;
    }

    public static bool Execute(MouseButton button, ModifierKeys modifiers, UIElement? focusedElement, UIElement visualRoot)
    {
        var executed = ExecuteMatchingBindings(
            static (binding, state) => binding is MouseBinding mouseBinding && mouseBinding.Matches(state.button, state.modifiers),
            (button, modifiers),
            focusedElement,
            visualRoot);
        return ExecuteMatchingImperativeMouseRegistrations(button, modifiers, visualRoot) || executed;
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

    private static bool ExecuteMatchingImperativeKeyRegistrations(Keys key, ModifierKeys modifiers, UIElement visualRoot)
    {
        var executed = false;
        for (var i = 0; i < _imperativeRegistrations.Count; i++)
        {
            var registration = _imperativeRegistrations[i];
            if (!registration.TryGetTarget(out var target))
            {
                _imperativeRegistrations.RemoveAt(i);
                i--;
                continue;
            }

            if (!registration.Matches(key, modifiers))
            {
                continue;
            }

            if (!IsInVisualTree(target, visualRoot))
            {
                continue;
            }

            if (TryExecuteImperativeRegistration(registration, target))
            {
                executed = true;
            }
        }

        return executed;
    }

    private static bool ExecuteMatchingImperativeMouseRegistrations(MouseButton button, ModifierKeys modifiers, UIElement visualRoot)
    {
        var executed = false;
        for (var i = 0; i < _imperativeRegistrations.Count; i++)
        {
            var registration = _imperativeRegistrations[i];
            if (!registration.TryGetTarget(out var target))
            {
                _imperativeRegistrations.RemoveAt(i);
                i--;
                continue;
            }

            if (!registration.Matches(button, modifiers))
            {
                continue;
            }

            if (!IsInVisualTree(target, visualRoot))
            {
                continue;
            }

            if (TryExecuteImperativeRegistration(registration, target))
            {
                executed = true;
            }
        }

        return executed;
    }

    private static bool TryExecuteImperativeRegistration(ImperativeRegistration registration, UIElement target)
    {
        var command = registration.Command;
        if (!CommandManager.CanExecute(command, registration.Parameter, target))
        {
            return false;
        }

        CommandManager.Execute(command, registration.Parameter, target);
        return true;
    }

    private static bool IsInVisualTree(UIElement target, UIElement visualRoot)
    {
        for (var current = target; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, visualRoot))
            {
                return true;
            }
        }

        return false;
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

    private static readonly List<ImperativeRegistration> _imperativeRegistrations = new();

    private readonly struct ImperativeRegistration
    {
        private ImperativeRegistration(
            ImperativeRegistrationKind kind,
            Keys key,
            MouseButton button,
            ModifierKeys modifiers,
            RoutedCommand command,
            UIElement target,
            object? parameter)
        {
            Kind = kind;
            Key = key;
            Button = button;
            Modifiers = modifiers;
            Command = command;
            Target = new WeakReference<UIElement>(target);
            Parameter = parameter;
        }

        public ImperativeRegistrationKind Kind { get; }

        public Keys Key { get; }

        public MouseButton Button { get; }

        public ModifierKeys Modifiers { get; }

        public RoutedCommand Command { get; }

        public WeakReference<UIElement> Target { get; }

        public object? Parameter { get; }

        public static ImperativeRegistration ForKey(
            Keys key,
            ModifierKeys modifiers,
            RoutedCommand command,
            UIElement target,
            object? parameter)
        {
            return new ImperativeRegistration(
                ImperativeRegistrationKind.Key,
                key,
                default,
                modifiers,
                command,
                target,
                parameter);
        }

        public static ImperativeRegistration ForMouse(
            MouseButton button,
            ModifierKeys modifiers,
            RoutedCommand command,
            UIElement target,
            object? parameter)
        {
            return new ImperativeRegistration(
                ImperativeRegistrationKind.Mouse,
                default,
                button,
                modifiers,
                command,
                target,
                parameter);
        }

        public bool Matches(Keys key, ModifierKeys modifiers)
        {
            return Kind == ImperativeRegistrationKind.Key &&
                   Key == key &&
                   Modifiers == modifiers;
        }

        public bool Matches(MouseButton button, ModifierKeys modifiers)
        {
            return Kind == ImperativeRegistrationKind.Mouse &&
                   Button == button &&
                   Modifiers == modifiers;
        }

        public bool TryGetTarget(out UIElement target)
        {
            return Target.TryGetTarget(out target!);
        }
    }

    private enum ImperativeRegistrationKind
    {
        Key,
        Mouse
    }
}
