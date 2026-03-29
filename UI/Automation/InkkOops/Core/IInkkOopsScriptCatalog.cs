using System.Collections.Generic;

namespace InkkSlinger;

public interface IInkkOopsScriptCatalog
{
    IReadOnlyList<string> ListScripts();

    bool TryResolve(string name, out IInkkOopsScriptDefinition? script);
}
