namespace InkkSlinger;

public sealed class Label : TextBlock
{
    // Parse-only compatibility shim for theme setter support.
    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Label),
            new FrameworkPropertyMetadata(Thickness.Empty));

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }
}
