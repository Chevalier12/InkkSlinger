using System;

namespace InkkSlinger;

internal static class CommandSourceExecution
{
    public static bool TryExecute(ICommandSource source, UIElement fallbackTarget)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fallbackTarget);
        return TryExecute(source.Command, source.CommandParameter, source.CommandTarget, fallbackTarget);
    }

    public static bool CanExecute(ICommandSource source, UIElement fallbackTarget)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fallbackTarget);
        return CanExecute(source.Command, source.CommandParameter, source.CommandTarget, fallbackTarget);
    }

    public static bool TryExecute(
        System.Windows.Input.ICommand? command,
        object? parameter,
        UIElement? commandTarget,
        UIElement fallbackTarget)
    {
        if (command == null)
        {
            return false;
        }

        if (command is RoutedCommand routedCommand)
        {
            var target = CommandTargetResolver.Resolve(commandTarget, fallbackTarget);
            if (!CommandManager.CanExecute(routedCommand, parameter, target))
            {
                return false;
            }

            CommandManager.Execute(routedCommand, parameter, target);
            return true;
        }

        if (!command.CanExecute(parameter))
        {
            return false;
        }

        command!.Execute(parameter);
        return true;
    }

    public static bool CanExecute(
        System.Windows.Input.ICommand? command,
        object? parameter,
        UIElement? commandTarget,
        UIElement fallbackTarget)
    {
        if (command == null)
        {
            return false;
        }

        if (command is RoutedCommand routedCommand)
        {
            var target = CommandTargetResolver.Resolve(commandTarget, fallbackTarget);
            return CommandManager.CanExecute(routedCommand, parameter, target);
        }

        return command.CanExecute(parameter);
    }
}
