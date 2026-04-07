using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsMovePointerTargetCommand : IInkkOopsCommand
{
    public InkkOopsMovePointerTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"MovePointerTo({Target}, anchor: {Anchor}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        return session.MovePointerAsync(point, Motion, cancellationToken);
    }
}