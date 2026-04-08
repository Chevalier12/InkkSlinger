using System;

namespace InkkSlinger;

public sealed class InkkOopsTargetReference
{
    public InkkOopsTargetReference(string name)
        : this(InkkOopsTargetSelector.Name(
            string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Target name is required.", nameof(name))
                : name))
    {
    }

    public InkkOopsTargetReference(InkkOopsTargetSelector selector)
    {
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public InkkOopsTargetSelector Selector { get; }

    public string Name => Selector.ToString();

    public override string ToString()
    {
        return Selector.ToString();
    }
}
