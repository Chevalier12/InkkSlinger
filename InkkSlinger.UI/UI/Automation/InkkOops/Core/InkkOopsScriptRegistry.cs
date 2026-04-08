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
        _catalog = new ReflectionInkkOopsScriptCatalog(CreateDefaultAssemblies(assembly));
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

    private static IReadOnlyList<Assembly> CreateDefaultAssemblies(Assembly assembly)
    {
        var assemblies = new List<Assembly>();
        var seenAssemblyNames = new HashSet<string>(StringComparer.Ordinal);

        AddAssembly(typeof(InkkOopsScriptRegistry).Assembly);
        AddAssembly(assembly);

        return assemblies;

        void AddAssembly(Assembly candidate)
        {
            var assemblyName = candidate.FullName ?? candidate.GetName().Name ?? string.Empty;
            if (seenAssemblyNames.Add(assemblyName))
            {
                assemblies.Add(candidate);
            }
        }
    }
}
