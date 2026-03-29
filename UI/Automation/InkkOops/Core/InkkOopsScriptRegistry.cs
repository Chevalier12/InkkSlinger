using System;
using System.Collections.Generic;
using System.Reflection;

namespace InkkSlinger;

public sealed class InkkOopsScriptRegistry : IInkkOopsScriptCatalog
{
    private readonly ReflectionInkkOopsScriptCatalog _catalog;

    public InkkOopsScriptRegistry()
        : this(Assembly.GetExecutingAssembly())
    {
    }

    public InkkOopsScriptRegistry(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _catalog = new ReflectionInkkOopsScriptCatalog(assembly);
    }

    public IReadOnlyList<string> ListScripts()
    {
        return _catalog.ListScripts();
    }

    public bool TryResolve(string name, out IInkkOopsBuiltinScript? script)
    {
        if (_catalog.TryResolve(name, out var definition) && definition is IInkkOopsBuiltinScript builtInScript)
        {
            script = builtInScript;
            return true;
        }

        script = null;
        return false;
    }

    bool IInkkOopsScriptCatalog.TryResolve(string name, out IInkkOopsScriptDefinition? script)
    {
        return _catalog.TryResolve(name, out script);
    }
}
