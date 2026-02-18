namespace InkkSlinger;

public enum BindingMode
{
    Default,
    OneWay,
    TwoWay,
    OneTime,
    OneWayToSource
}

public enum UpdateSourceTrigger
{
    Default,
    PropertyChanged,
    LostFocus,
    Explicit
}

public enum RelativeSourceMode
{
    None,
    Self,
    TemplatedParent,
    FindAncestor
}
