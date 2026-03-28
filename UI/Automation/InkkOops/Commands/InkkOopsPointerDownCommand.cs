using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsPointerDownCommand : IInkkOopsCommand
{
    public InkkOopsPointerDownCommand(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"PointerDown({Position.X:0.###}, {Position.Y:0.###})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.PressPointerAsync(Position, cancellationToken);
    }
}
