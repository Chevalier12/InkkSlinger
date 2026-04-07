using System;
using System.Collections.Generic;

namespace InkkSlinger.Tests;

public abstract class InkkOopsRuntimeScenario : IInkkOopsScriptDefinition
{
    public abstract string Name { get; }

    protected virtual IEnumerable<int>? ActionDiagnosticsIndexes => null;

    public InkkOopsScript CreateScript()
    {
        var builder = new InkkOopsScriptBuilder(Name, ActionDiagnosticsIndexes);
        Build(builder);
        return builder.Build();
    }

    protected abstract void Build(InkkOopsScriptBuilder builder);
}
