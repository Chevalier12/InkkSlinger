using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsHoverTargetCommand : IInkkOopsCommand
{
    public InkkOopsHoverTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null, int dwellFrames = 0, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        DwellFrames = Math.Max(0, dwellFrames);
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public int DwellFrames { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Hover({Target}, anchor: {Anchor}, dwellFrames: {DwellFrames}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        return ExecuteAsync(session, point, cancellationToken);
    }

    private async Task ExecuteAsync(InkkOopsSession session, System.Numerics.Vector2 point, CancellationToken cancellationToken)
    {
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
        if (DwellFrames > 0)
        {
            await session.WaitFramesAsync(DwellFrames, cancellationToken).ConfigureAwait(false);
        }
    }
}
