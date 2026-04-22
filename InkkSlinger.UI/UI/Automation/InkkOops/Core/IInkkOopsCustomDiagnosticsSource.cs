namespace InkkSlinger;

public interface IInkkOopsCustomDiagnosticsSource
{
    void ContributeInkkOopsDiagnostics(InkkOopsElementDiagnosticsBuilder builder);
}