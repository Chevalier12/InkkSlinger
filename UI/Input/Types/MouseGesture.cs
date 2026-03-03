namespace InkkSlinger;

public readonly record struct MouseGesture(MouseButton Button, ModifierKeys Modifiers)
{
    public bool Matches(MouseButton button, ModifierKeys modifiers)
    {
        return Button == button && Modifiers == modifiers;
    }

    public override string ToString()
    {
        return Format(Button, Modifiers);
    }

    public static string Format(MouseButton button, ModifierKeys modifiers)
    {
        var buttonText = FormatButton(button);
        if (modifiers == ModifierKeys.None)
        {
            return buttonText;
        }

        var segments = new System.Collections.Generic.List<string>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
        {
            segments.Add("Ctrl");
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            segments.Add("Alt");
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            segments.Add("Shift");
        }

        segments.Add(buttonText);
        return string.Join("+", segments);
    }

    private static string FormatButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "LeftClick",
            MouseButton.Right => "RightClick",
            MouseButton.Middle => "MiddleClick",
            MouseButton.XButton1 => "XButton1Click",
            MouseButton.XButton2 => "XButton2Click",
            _ => button.ToString()
        };
    }
}
