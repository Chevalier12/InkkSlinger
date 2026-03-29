namespace InkkSlinger;

public sealed class InkkOopsFrameworkElementDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 10;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        var slot = frameworkElement.LayoutSlot;
        builder.Add("slot", $"{slot.X:0.##},{slot.Y:0.##},{slot.Width:0.##},{slot.Height:0.##}");
        builder.Add("visible", frameworkElement.Visibility == Visibility.Visible);
        builder.Add("enabled", frameworkElement.IsEnabled);
    }
}
