using System;

namespace InkkSlinger;

public sealed class InputBinding
{
    public InputBinding(InputGesture gesture, System.Windows.Input.ICommand command)
    {
        Gesture = gesture ?? throw new ArgumentNullException(nameof(gesture));
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public InputGesture Gesture { get; }

    public System.Windows.Input.ICommand Command { get; }

    public object? CommandParameter { get; set; }

    public UIElement? CommandTarget { get; set; }
}

