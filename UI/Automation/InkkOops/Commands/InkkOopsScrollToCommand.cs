using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsScrollToCommand : IInkkOopsCommand
{
    public InkkOopsScrollToCommand(InkkOopsTargetReference target, float horizontalPercent, float verticalPercent)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        HorizontalPercent = horizontalPercent;
        VerticalPercent = verticalPercent;
    }

    public InkkOopsTargetReference Target { get; }

    public float HorizontalPercent { get; }

    public float VerticalPercent { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"ScrollTo({Target}, h: {HorizontalPercent:0.###}, v: {VerticalPercent:0.###})";
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

                scrollProvider.SetScrollPercent(HorizontalPercent, VerticalPercent);
            },
            cancellationToken);
    }
}
