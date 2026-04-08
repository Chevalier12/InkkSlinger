using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace InkkSlinger;

public sealed class InkkOopsScript
{
    private readonly List<IInkkOopsCommand> _commands = new();
    private readonly int[] _actionDiagnosticsIndexes;

    public InkkOopsScript(string name, IEnumerable<int>? actionDiagnosticsIndexes = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Script name is required.", nameof(name))
            : name;
        _actionDiagnosticsIndexes = actionDiagnosticsIndexes == null
            ? []
            : [.. actionDiagnosticsIndexes.Where(static index => index >= 0).Distinct().OrderBy(static index => index)];
    }

    public string Name { get; }

    public IReadOnlyList<IInkkOopsCommand> Commands => _commands;

    public IReadOnlyList<int> ActionDiagnosticsIndexes => new ReadOnlyCollection<int>(_actionDiagnosticsIndexes);

    public InkkOopsScript Add(IInkkOopsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
        return this;
    }
}
