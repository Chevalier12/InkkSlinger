using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InkkSlinger;

public sealed class InkkOopsScriptRegistry
{
    private readonly Dictionary<string, Type> _scriptTypes;

    public InkkOopsScriptRegistry()
        : this(Assembly.GetExecutingAssembly())
    {
    }

    public InkkOopsScriptRegistry(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _scriptTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract ||
                !typeof(IInkkOopsBuiltinScript).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            var instance = (IInkkOopsBuiltinScript)Activator.CreateInstance(type)!;
            if (_scriptTypes.TryGetValue(instance.Name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate InkkOops script name '{instance.Name}' found on '{existing.FullName}' and '{type.FullName}'.");
            }

            _scriptTypes.Add(instance.Name, type);
        }
    }

    public IReadOnlyList<string> ListScripts()
    {
        return _scriptTypes.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
    }

    public bool TryResolve(string name, out IInkkOopsBuiltinScript? script)
    {
        if (_scriptTypes.TryGetValue(name, out var type))
        {
            script = (IInkkOopsBuiltinScript)Activator.CreateInstance(type)!;
            return true;
        }

        script = null;
        return false;
    }
}
