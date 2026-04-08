using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsScrollByCommand : IInkkOopsCommand
{
    public InkkOopsScrollByCommand(InkkOopsTargetReference target, float horizontalPercentDelta, float verticalPercentDelta)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        HorizontalPercentDelta = horizontalPercentDelta;
        VerticalPercentDelta = verticalPercentDelta;
    }

    public InkkOopsTargetReference Target { get; }

    public float HorizontalPercentDelta { get; }

    public float VerticalPercentDelta { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"ScrollBy({Target}, dh: {HorizontalPercentDelta:0.###}, dv: {VerticalPercentDelta:0.###})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var peer = session.ResolveRequiredAutomationPeer(Target);
                if (!peer.TryGetPattern(AutomationPatternType.Scroll, out var provider) ||
                    provider is not IScrollProvider scrollProvider)
                {
                    throw new InkkOopsCommandException(
                        InkkOopsFailureCategory.SemanticProviderMissing,
                        $"Target '{Target.Name}' does not support Scroll.");
                }

                scrollProvider.SetScrollPercent(
                    scrollProvider.HorizontalScrollPercent + HorizontalPercentDelta,
                    scrollProvider.VerticalScrollPercent + VerticalPercentDelta);
            },
            cancellationToken);
    }
}
