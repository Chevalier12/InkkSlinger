namespace InkkSlinger;

[System.Flags]
public enum FrameworkPropertyMetadataOptions
{
    None = 0,
    AffectsMeasure = 1,
    AffectsArrange = 2,
    AffectsRender = 4,
    Inherits = 8,
    BindsTwoWayByDefault = 16
}
