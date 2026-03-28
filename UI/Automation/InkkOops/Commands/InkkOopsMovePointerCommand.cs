using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsMovePointerCommand : IInkkOopsCommand
{
    public InkkOopsMovePointerCommand(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"MovePointer({Position.X:0.###}, {Position.Y:0.###})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.MovePointerAsync(Position, cancellationToken);
    }
}
