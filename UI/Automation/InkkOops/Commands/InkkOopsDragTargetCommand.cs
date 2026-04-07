using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsDragTargetCommand : IInkkOopsCommand
{
    public InkkOopsDragTargetCommand(
        InkkOopsTargetReference target,
        float deltaX,
        float deltaY,
        InkkOopsPointerAnchor? anchor = null,
        MouseButton button = MouseButton.Left,
        InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        DeltaX = deltaX;
        DeltaY = deltaY;
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        Button = button;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public float DeltaX { get; }

    public float DeltaY { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public MouseButton Button { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Drag({Target}, dx: {DeltaX:0.###}, dy: {DeltaY:0.###}, anchor: {Anchor}, button: {Button}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var start = session.ResolveRequiredActionPoint(Target, Anchor);
        var end = start + new Vector2(DeltaX, DeltaY);

        await session.MovePointerAsync(start, Motion, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(start, Button, cancellationToken).ConfigureAwait(false);
        await session.MovePointerAsync(end, Motion, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(end, Button, cancellationToken).ConfigureAwait(false);
    }
}
