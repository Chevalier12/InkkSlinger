namespace InkkSlinger;

public sealed class MouseBinding : InputBinding
{
    public MouseButton Button { get; set; }

    public ModifierKeys Modifiers { get; set; }

    public MouseGesture Gesture
    {
        get => new(Button, Modifiers);
        set
        {
            Button = value.Button;
            Modifiers = value.Modifiers;
        }
    }

    public bool Matches(MouseButton button, ModifierKeys modifiers)
    {
        return Button == button && Modifiers == modifiers;
    }
}
