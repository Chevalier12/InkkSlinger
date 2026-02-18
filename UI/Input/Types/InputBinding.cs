namespace InkkSlinger;

public class InputBinding
{
    public System.Windows.Input.ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    public UIElement? CommandTarget { get; set; }
}
