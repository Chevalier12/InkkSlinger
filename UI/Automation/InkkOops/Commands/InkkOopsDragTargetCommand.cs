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
        InkkOopsPointerAnchor? anchor = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        DeltaX = deltaX;
        DeltaY = deltaY;
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
    }

    public InkkOopsTargetReference Target { get; }

    public float DeltaX { get; }

    public float DeltaY { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Drag({Target}, dx: {DeltaX:0.###}, dy: {DeltaY:0.###}, anchor: {Anchor})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var start = session.ResolveRequiredActionPoint(Target, Anchor);
        var end = start + new Vector2(DeltaX, DeltaY);
        var distance = Vector2.Distance(start, end);
        var steps = Math.Max(2, (int)MathF.Ceiling(distance / 24f));

        await session.MovePointerAsync(start, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(start, cancellationToken).ConfigureAwait(false);

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var point = Vector2.Lerp(start, end, t);
            await session.MovePointerAsync(point, cancellationToken).ConfigureAwait(false);
            await session.WaitFramesAsync(1, cancellationToken).ConfigureAwait(false);
        }

        await session.ReleasePointerAsync(end, cancellationToken).ConfigureAwait(false);
    }
}
