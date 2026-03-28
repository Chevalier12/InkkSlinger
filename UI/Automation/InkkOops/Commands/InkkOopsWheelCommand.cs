using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsWheelCommand : IInkkOopsCommand
{
    public InkkOopsWheelCommand(int delta)
    {
        Delta = delta;
    }

    public int Delta { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Wheel({Delta})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.WheelAsync(Delta, cancellationToken);
    }
}
