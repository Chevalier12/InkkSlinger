using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class NameScope
{
    private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

    public void RegisterName(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _entries[name] = value;
    }

    public void UnregisterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _entries.Remove(name);
    }

    public object? FindName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _entries.TryGetValue(name, out var value) ? value : null;
    }
}
