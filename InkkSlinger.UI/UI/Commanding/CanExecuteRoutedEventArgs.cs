using System;

namespace InkkSlinger;

public sealed class CanExecuteRoutedEventArgs : EventArgs
{
    public CanExecuteRoutedEventArgs(System.Windows.Input.ICommand command, object? parameter, UIElement? target)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Parameter = parameter;
        Target = target;
    }

    public System.Windows.Input.ICommand Command { get; }

    public object? Parameter { get; }

    public UIElement? Target { get; }

    public bool CanExecute { get; set; }

    public bool Handled { get; set; }

    public Exception? Exception { get; set; }
}

