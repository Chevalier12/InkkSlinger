namespace InkkSlinger;

public interface IInkkOopsDiagnosticsContributor
{
    int Order { get; }

    void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder);
}
