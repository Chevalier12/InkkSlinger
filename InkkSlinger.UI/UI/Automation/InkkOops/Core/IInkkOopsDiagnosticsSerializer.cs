namespace InkkSlinger;

public interface IInkkOopsDiagnosticsSerializer
{
    string SerializeVisualTree(InkkOopsVisualTreeSnapshot snapshot);
}
