using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsMovePointerPathCommand : IInkkOopsCommand
{
    private readonly IReadOnlyList<Vector2> _waypoints;

    public InkkOopsMovePointerPathCommand(IEnumerable<Vector2> waypoints, InkkOopsPointerMotion? motion = null)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        _waypoints = waypoints.ToArray();
        if (_waypoints.Count == 0)
        {
            throw new ArgumentException("At least one waypoint is required.", nameof(waypoints));
        }

        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public IReadOnlyList<Vector2> Waypoints => _waypoints;

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"MovePointerPath(count: {_waypoints.Count}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        foreach (var waypoint in _waypoints)
        {
            await session.MovePointerAsync(waypoint, Motion, cancellationToken).ConfigureAwait(false);
        }
    }
}