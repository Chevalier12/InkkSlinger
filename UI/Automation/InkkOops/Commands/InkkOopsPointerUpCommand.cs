using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsPointerUpCommand : IInkkOopsCommand
{
    public InkkOopsPointerUpCommand(Vector2 position, MouseButton button = MouseButton.Left)
    {
        Position = position;
        Button = button;
    }

    public Vector2 Position { get; }

    public MouseButton Button { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"PointerUp({Position.X:0.###}, {Position.Y:0.###}, button: {Button})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ReleasePointerAsync(Position, Button, cancellationToken);
    }
}
