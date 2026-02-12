using System;

namespace InkkSlinger;

public sealed class CommandBinding
{
    public CommandBinding(System.Windows.Input.ICommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public CommandBinding(System.Windows.Input.ICommand command, EventHandler<ExecutedRoutedEventArgs> executed)
        : this(command)
    {
        Executed += executed ?? throw new ArgumentNullException(nameof(executed));
    }

    public CommandBinding(
        System.Windows.Input.ICommand command,
        EventHandler<ExecutedRoutedEventArgs> executed,
        EventHandler<CanExecuteRoutedEventArgs> canExecute)
        : this(command, executed)
    {
        CanExecute += canExecute ?? throw new ArgumentNullException(nameof(canExecute));
    }

    public System.Windows.Input.ICommand Command { get; }

    public event EventHandler<ExecutedRoutedEventArgs>? Executed;

    public event EventHandler<CanExecuteRoutedEventArgs>? CanExecute;

    internal bool HasExecutedHandlers => Executed != null;

    internal void RaiseCanExecute(UIElement sender, CanExecuteRoutedEventArgs args)
    {
        CanExecute?.Invoke(sender, args);
    }

    internal void RaiseExecuted(UIElement sender, ExecutedRoutedEventArgs args)
    {
        Executed?.Invoke(sender, args);
    }
}

