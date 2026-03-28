using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsHoverTargetCommand : IInkkOopsCommand
{
    public InkkOopsHoverTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Hover({Target}, anchor: {Anchor})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        return session.MovePointerAsync(point, cancellationToken);
    }
}
