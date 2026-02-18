using System;

namespace InkkSlinger;

public abstract class BindingBase
{
    public BindingMode Mode { get; set; } = BindingMode.Default;

    public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.Default;

    public object? FallbackValue { get; set; }

    public object? TargetNullValue { get; set; }
}
