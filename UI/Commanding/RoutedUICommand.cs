using System;

namespace InkkSlinger;

public sealed class RoutedUICommand : RoutedCommand
{
    public RoutedUICommand(string? text = null, string? name = null, Type? ownerType = null)
        : base(name, ownerType)
    {
        Text = text;
    }

    public string? Text { get; }
}
