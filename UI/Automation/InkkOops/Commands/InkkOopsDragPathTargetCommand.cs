using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsDragPathTargetCommand : IInkkOopsCommand
{
    private readonly IReadOnlyList<Vector2> _waypoints;

    public InkkOopsDragPathTargetCommand(InkkOopsTargetReference target, IEnumerable<Vector2> waypoints, InkkOopsPointerAnchor? anchor = null, MouseButton button = MouseButton.Left, InkkOopsPointerMotion? motion = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentNullException.ThrowIfNull(waypoints);
        _waypoints = waypoints.ToArray();
        if (_waypoints.Count == 0)
        {
            throw new ArgumentException("At least one waypoint is required.", nameof(waypoints));
        }

        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
        Button = button;
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public InkkOopsTargetReference Target { get; }

    public IReadOnlyList<Vector2> Waypoints => _waypoints;

    public InkkOopsPointerAnchor Anchor { get; }

    public MouseButton Button { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"DragPath({Target}, count: {_waypoints.Count}, anchor: {Anchor}, button: {Button}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        var start = session.ResolveRequiredActionPoint(Target, Anchor);
        await session.MovePointerAsync(start, Motion, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(start, Button, cancellationToken).ConfigureAwait(false);

        foreach (var waypoint in _waypoints)
        {
            await session.MovePointerAsync(waypoint, Motion, cancellationToken).ConfigureAwait(false);
        }

        await session.ReleasePointerAsync(_waypoints[^1], Button, cancellationToken).ConfigureAwait(false);
    }
}