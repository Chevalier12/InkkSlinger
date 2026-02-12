using System;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class KeyGesture : InputGesture
{
    public KeyGesture(Keys key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public Keys Key { get; }

    public ModifierKeys Modifiers { get; }

    public override bool Matches(RoutedKeyEventArgs args)
    {
        return args.Key == Key && args.Modifiers == Modifiers;
    }

    public override string ToString()
    {
        if (Modifiers == ModifierKeys.None)
        {
            return Key.ToString();
        }

        return $"{Modifiers}+{Key}";
    }
}

