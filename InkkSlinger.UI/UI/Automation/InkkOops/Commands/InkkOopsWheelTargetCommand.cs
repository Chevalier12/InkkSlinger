using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsWheelTargetCommand : IInkkOopsCommand
{
    public InkkOopsWheelTargetCommand(InkkOopsTargetReference target, int delta, InkkOopsPointerAnchor? anchor = null, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Delta = delta;
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public int Delta { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Wheel({Target}, delta: {Delta}, anchor: {Anchor}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
        await session.WheelAsync(Delta, cancellationToken).ConfigureAwait(false);
    }
}