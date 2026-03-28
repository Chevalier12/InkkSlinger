using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsPointerUpCommand : IInkkOopsCommand
{
    public InkkOopsPointerUpCommand(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"PointerUp({Position.X:0.###}, {Position.Y:0.###})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ReleasePointerAsync(Position, cancellationToken);
    }
}
