using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InkkSlinger;

public sealed class ReflectionInkkOopsScriptCatalog : IInkkOopsScriptCatalog
{
    private readonly Dictionary<string, Type> _scriptTypes;

    public ReflectionInkkOopsScriptCatalog()
        : this([Assembly.GetExecutingAssembly()])
    {
    }

    public ReflectionInkkOopsScriptCatalog(Assembly assembly)
        : this([assembly])
    {
    }

    public ReflectionInkkOopsScriptCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        _scriptTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract ||
                    !typeof(IInkkOopsScriptDefinition).IsAssignableFrom(type) ||
                    type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var instance = (IInkkOopsScriptDefinition)Activator.CreateInstance(type)!;
                if (_scriptTypes.TryGetValue(instance.Name, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Duplicate InkkOops script name '{instance.Name}' found on '{existing.FullName}' and '{type.FullName}'.");
                }

                _scriptTypes.Add(instance.Name, type);
            }
        }
    }

    public IReadOnlyList<string> ListScripts()
    {
        return _scriptTypes.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
    }

    public bool TryResolve(string name, out IInkkOopsScriptDefinition? script)
    {
        if (_scriptTypes.TryGetValue(name, out var type))
        {
            script = (IInkkOopsScriptDefinition)Activator.CreateInstance(type)!;
            return true;
        }

        script = null;
        return false;
    }
}
