namespace InkkSlinger;

public enum BindingMode
{
    OneWay,
    TwoWay,
    OneTime
}

public enum UpdateSourceTrigger
{
    PropertyChanged,
    Explicit
}

public enum RelativeSourceMode
{
    None,
    Self,
    TemplatedParent,
    FindAncestor
}
