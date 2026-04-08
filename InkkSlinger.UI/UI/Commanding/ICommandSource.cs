namespace InkkSlinger;

public interface ICommandSource
{
    System.Windows.Input.ICommand? Command { get; }

    object? CommandParameter { get; }

    UIElement? CommandTarget { get; }
}
