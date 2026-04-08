namespace InkkSlinger;

public interface IInkkOopsDiagnosticsFilterPolicy
{
    InkkOopsDiagnosticsFilter CreateFilter(string artifactName);
}