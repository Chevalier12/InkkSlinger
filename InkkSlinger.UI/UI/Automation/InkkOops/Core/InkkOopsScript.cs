using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsScript
{
    private readonly List<IInkkOopsCommand> _commands = new();

    public InkkOopsScript(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Script name is required.", nameof(name))
            : name;
    }

    public string Name { get; }

    public IReadOnlyList<IInkkOopsCommand> Commands => _commands;

    public InkkOopsScript Add(IInkkOopsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
        return this;
    }
}
