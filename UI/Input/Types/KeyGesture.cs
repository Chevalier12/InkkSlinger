using System;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public readonly record struct KeyGesture(Keys Key, ModifierKeys Modifiers)
{
    public bool Matches(Keys key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == modifiers;
    }

    public override string ToString()
    {
        return Format(Key, Modifiers);
    }

    public static string Format(Keys key, ModifierKeys modifiers)
    {
        var keyText = FormatKey(key);
        if (modifiers == ModifierKeys.None)
        {
            return keyText;
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

        segments.Add(keyText);
        return string.Join("+", segments);
    }

    private static string FormatKey(Keys key)
    {
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return ((int)(key - Keys.D0)).ToString();
        }

        return key switch
        {
            Keys.OemPlus => "+",
            Keys.OemMinus => "-",
            _ => key.ToString()
        };
    }
}
