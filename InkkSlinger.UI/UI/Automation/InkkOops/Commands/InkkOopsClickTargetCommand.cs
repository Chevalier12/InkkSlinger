using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsClickTargetCommand : IInkkOopsCommand
{
    public InkkOopsClickTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null, MouseButton button = MouseButton.Left, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        Button = button;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public MouseButton Button { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Click({Target}, anchor: {Anchor}, button: {Button}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            var point = session.ResolveRequiredActionPoint(Target, Anchor);
            await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
            await session.PressPointerAsync(point, Button, cancellationToken).ConfigureAwait(false);
            await session.ReleasePointerAsync(point, Button, cancellationToken).ConfigureAwait(false);
        }
        catch (InkkOopsCommandException ex) when (ShouldFallbackToInvoke(session, ex))
        {
            await new InkkOopsInvokeTargetCommand(Target).ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldFallbackToInvoke(InkkOopsSession session, InkkOopsCommandException ex)
    {
        if (Button != MouseButton.Left || ex.Category != InkkOopsFailureCategory.Clipped)
        {
            return false;
        }

        var resolved = session.ResolveRequiredTarget(Target);
        return resolved is Button;
    }
}
