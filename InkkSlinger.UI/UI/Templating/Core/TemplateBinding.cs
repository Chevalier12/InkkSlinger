namespace InkkSlinger;

public sealed class TemplateBinding
{
    public TemplateBinding(string targetName, DependencyProperty targetProperty, DependencyProperty sourceProperty)
        : this(targetName, targetProperty, sourceProperty, null, null)
    {
    }

    public TemplateBinding(
        string targetName,
        DependencyProperty targetProperty,
        DependencyProperty sourceProperty,
        object? fallbackValue,
        object? targetNullValue)
    {
        TargetName = targetName;
        TargetProperty = targetProperty;
        SourceProperty = sourceProperty;
        FallbackValue = fallbackValue;
        TargetNullValue = targetNullValue;
    }

    public string TargetName { get; }

    public DependencyProperty TargetProperty { get; }

    public DependencyProperty SourceProperty { get; }

    public object? FallbackValue { get; }

    public object? TargetNullValue { get; }
}
