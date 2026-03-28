using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsClickTargetCommand : IInkkOopsCommand
{
    public InkkOopsClickTargetCommand(InkkOopsTargetReference target, InkkOopsPointerAnchor? anchor = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"Click({Target}, anchor: {Anchor})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = session.ResolveRequiredActionPoint(Target, Anchor);
        await session.MovePointerAsync(point, cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, cancellationToken).ConfigureAwait(false);
    }
}
