using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsWaitForIdleCommand : IInkkOopsCommand
{
    public InkkOopsWaitForIdleCommand(InkkOopsIdlePolicy policy = InkkOopsIdlePolicy.LayoutAndRender)
    {
        Policy = policy;
    }

    public InkkOopsIdlePolicy Policy { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"WaitForIdle({Policy})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.WaitForIdleAsync(Policy, cancellationToken);
    }
}
