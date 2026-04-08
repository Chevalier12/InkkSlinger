using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsLeaveTargetCommand : IInkkOopsCommand
{
    public InkkOopsLeaveTargetCommand(InkkOopsTargetReference target, float padding = 16f, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Padding = Math.Max(1f, padding);
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public float Padding { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"LeaveTarget({Target}, padding: {Padding:0.###}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var state = session.EvaluateTargetState(Target);
        if (!state.HasBounds || !state.HasViewportBounds)
        {
            throw new InkkOopsCommandException(
                state.FailureCategory == InkkOopsFailureCategory.None ? InkkOopsFailureCategory.Unrealized : state.FailureCategory,
                $"Cannot leave target '{Target}' because bounds or viewport are unavailable.");
        }

        var point = ChooseExitPoint(state.Bounds, state.ViewportBounds, Padding);
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
    }

    private static Vector2 ChooseExitPoint(LayoutRect bounds, LayoutRect viewport, float padding)
    {
        var left = bounds.X - padding;
        if (left >= viewport.X)
        {
            return new Vector2(left, bounds.Y + (bounds.Height * 0.5f));
        }

        var right = bounds.X + bounds.Width + padding;
        if (right <= viewport.X + viewport.Width)
        {
            return new Vector2(right, bounds.Y + (bounds.Height * 0.5f));
        }

        var above = bounds.Y - padding;
        if (above >= viewport.Y)
        {
            return new Vector2(bounds.X + (bounds.Width * 0.5f), above);
        }

        var below = Math.Min(viewport.Y + viewport.Height, bounds.Y + bounds.Height + padding);
        return new Vector2(bounds.X + (bounds.Width * 0.5f), below);
    }
}