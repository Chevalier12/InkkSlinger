using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsMovePointerCommand : IInkkOopsCommand
{
    public InkkOopsMovePointerCommand(Vector2 position, InkkOopsPointerMotion? motion = null)
    {
        Position = position;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public Vector2 Position { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"MovePointer({Position.X:0.###}, {Position.Y:0.###}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.MovePointerAsync(Position, Motion, cancellationToken);
    }
}
