using System;

namespace InkkSlinger;

public sealed class Binding
{
    public string Path { get; set; } = string.Empty;

    public object? Source { get; set; }

    public string? ElementName { get; set; }

    public RelativeSourceMode RelativeSourceMode { get; set; } = RelativeSourceMode.None;

    public Type? RelativeSourceAncestorType { get; set; }

    public int RelativeSourceAncestorLevel { get; set; } = 1;

    public BindingMode Mode { get; set; } = BindingMode.OneWay;

    public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.PropertyChanged;

    public object? FallbackValue { get; set; }

    public object? TargetNullValue { get; set; }
}
