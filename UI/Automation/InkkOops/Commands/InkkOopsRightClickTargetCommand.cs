using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsRightClickTargetCommand : IInkkOopsCommand
{
    public InkkOopsRightClickTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null, InkkOopsPointerMotion? motion = null)
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
        return $"RightClick({Target}, anchor: {Anchor}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, MouseButton.Right, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Right, cancellationToken).ConfigureAwait(false);
    }
}