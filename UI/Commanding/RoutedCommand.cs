using System;

namespace InkkSlinger;

public sealed class RoutedCommand : System.Windows.Input.ICommand
{
    public RoutedCommand(string? name = null, Type? ownerType = null)
    {
        Name = name;
        OwnerType = ownerType;
    }

    public string? Name { get; }

    public Type? OwnerType { get; }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return CanExecute(parameter, null);
    }

    public void Execute(object? parameter)
    {
        Execute(parameter, null);
    }

    public bool CanExecute(object? parameter, UIElement? target)
    {
        return CommandManager.CanExecute(this, parameter, target);
    }

    public void Execute(object? parameter, UIElement? target)
    {
        CommandManager.Execute(this, parameter, target);
    }
}
