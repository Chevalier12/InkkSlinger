namespace InkkSlinger;

public interface IInkkOopsScriptDefinition
{
    string Name { get; }

    InkkOopsScript CreateScript();
}
