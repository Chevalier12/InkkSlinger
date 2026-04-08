namespace InkkSlinger;

public sealed class InkkOopsGenericElementDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 0;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        builder.Add("hovered", ReferenceEquals(context.HoveredElement, element));
        builder.Add("focused", ReferenceEquals(context.FocusedElement, element));
    }
}
