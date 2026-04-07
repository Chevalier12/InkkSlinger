using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsDoubleClickTargetCommand : IInkkOopsCommand
{
    public InkkOopsDoubleClickTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null, int interClickWaitFrames = 1, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        InterClickWaitFrames = Math.Max(0, interClickWaitFrames);
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public int InterClickWaitFrames { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"DoubleClick({Target}, anchor: {Anchor}, interClickWaitFrames: {InterClickWaitFrames}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        if (InterClickWaitFrames > 0)
        {
            await session.WaitFramesAsync(InterClickWaitFrames, cancellationToken).ConfigureAwait(false);
        }
        await session.PressPointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
    }
}