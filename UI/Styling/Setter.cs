namespace InkkSlinger;

public sealed class Setter
{
    public Setter(DependencyProperty property, object? value)
        : this(string.Empty, property, value)
    {
    }

    public Setter(string targetName, DependencyProperty property, object? value)
    {
        TargetName = targetName ?? string.Empty;
        Property = property;
        Value = value;
    }

    public string TargetName { get; }

    public DependencyProperty Property { get; }

    public object? Value { get; }
}
