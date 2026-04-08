using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class KeyBinding : InputBinding
{
    public Keys Key { get; set; }

    public ModifierKeys Modifiers { get; set; }

    public KeyGesture Gesture
    {
        get => new(Key, Modifiers);
        set
        {
            Key = value.Key;
            Modifiers = value.Modifiers;
        }
    }

    public bool Matches(Keys key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == modifiers;
    }

    public string GetDisplayString()
    {
        return KeyGesture.Format(Key, Modifiers);
    }
}
