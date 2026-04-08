using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsInvokeTargetCommand : IInkkOopsCommand
{
    public InkkOopsInvokeTargetCommand(InkkOopsTargetReference target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"Invoke({Target})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var peer = session.ResolveRequiredAutomationPeer(Target);
                if (!peer.TryGetPattern(AutomationPatternType.Invoke, out var provider) ||
                    provider is not IInvokeProvider invokeProvider)
                {
                    throw new InkkOopsCommandException(
                        InkkOopsFailureCategory.SemanticProviderMissing,
                        $"Target '{Target.Name}' does not support Invoke.");
                }

                invokeProvider.Invoke();
            },
            cancellationToken);
    }
}
