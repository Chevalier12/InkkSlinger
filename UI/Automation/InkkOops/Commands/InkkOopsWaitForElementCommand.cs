using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsWaitForElementCommand : IInkkOopsCommand
{
    public InkkOopsWaitForElementCommand(InkkOopsTargetReference target, int maxFrames, InkkOopsWaitCondition condition = InkkOopsWaitCondition.Exists, InkkOopsPointerAnchor? anchor = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        MaxFrames = maxFrames <= 0 ? throw new ArgumentOutOfRangeException(nameof(maxFrames)) : maxFrames;
        Condition = condition;
        Anchor = anchor ?? InkkOopsPointerAnchor.Center;
    }

    public InkkOopsTargetReference Target { get; }

    public int MaxFrames { get; }

    public InkkOopsWaitCondition Condition { get; }

    public InkkOopsPointerAnchor Anchor { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"WaitFor{Condition}({Target}, maxFrames: {MaxFrames}, anchor: {Anchor})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            if (IsSatisfied(session))
            {
                return;
            }

            await session.WaitFramesAsync(1, cancellationToken).ConfigureAwait(false);
        }

        var finalReport = session.ResolveTarget(Target);
        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Timeout,
            $"Timed out waiting for {Condition} on target '{Target.Name}'.{Environment.NewLine}{finalReport.Describe()}");
    }

    private bool IsSatisfied(InkkOopsSession session)
    {
        if (Condition == InkkOopsWaitCondition.Exists)
        {
            var report = session.ResolveTarget(Target);
            return report.Status == InkkOopsTargetResolutionStatus.Resolved && report.Element != null;
        }

        var state = session.EvaluateTargetState(Target, Anchor);
        return Condition switch
        {
            InkkOopsWaitCondition.Visible => state.Resolution.Status == InkkOopsTargetResolutionStatus.Resolved && state.IsVisible,
            InkkOopsWaitCondition.Enabled => state.Resolution.Status == InkkOopsTargetResolutionStatus.Resolved && state.IsEnabled,
            InkkOopsWaitCondition.InViewport => state.Resolution.Status == InkkOopsTargetResolutionStatus.Resolved && state.IsInViewport,
            InkkOopsWaitCondition.Interactive => state.Resolution.Status == InkkOopsTargetResolutionStatus.Resolved && state.IsInteractive,
            _ => state.Resolution.Status == InkkOopsTargetResolutionStatus.Resolved
        };
    }
}
