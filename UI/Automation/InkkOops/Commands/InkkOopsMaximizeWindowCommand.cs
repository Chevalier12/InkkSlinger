using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsMaximizeWindowCommand : IInkkOopsCommand
{
    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return "MaximizeWindow()";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.MaximizeWindowAsync(cancellationToken);
    }
}
