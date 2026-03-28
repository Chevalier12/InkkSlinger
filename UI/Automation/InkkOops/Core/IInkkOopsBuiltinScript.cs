namespace InkkSlinger;

public interface IInkkOopsBuiltinScript
{
    string Name { get; }

    InkkOopsScript CreateScript();
}
