using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public static class InputGestureService
{
    private static readonly Dictionary<KeyChord, List<Binding>> Bindings = new();

    public static void Register(
        Keys key,
        ModifierKeys modifiers,
        RoutedCommand command,
        UIElement target,
        object? parameter = null)
    {
        var chord = new KeyChord(key, modifiers);
        if (!Bindings.TryGetValue(chord, out var list))
        {
            list = new List<Binding>(1);
            Bindings[chord] = list;
        }

        list.Add(new Binding(command, target, parameter));
    }

    public static void Clear()
    {
        Bindings.Clear();
    }

    public static bool Execute(Keys key, ModifierKeys modifiers)
    {
        var chord = new KeyChord(key, modifiers);
        if (!Bindings.TryGetValue(chord, out var list))
        {
            return false;
        }

        var executed = false;
        for (var i = 0; i < list.Count; i++)
        {
            var binding = list[i];
            if (!CommandManager.CanExecute(binding.Command, binding.Parameter, binding.Target))
            {
                continue;
            }

            CommandManager.Execute(binding.Command, binding.Parameter, binding.Target);
            executed = true;
        }

        return executed;
    }

    private readonly record struct Binding(RoutedCommand Command, UIElement Target, object? Parameter);

    private readonly record struct KeyChord(Keys Key, ModifierKeys Modifiers);
}
