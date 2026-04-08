using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsAssertExistsCommand : IInkkOopsCommand
{
    public InkkOopsAssertExistsCommand(InkkOopsTargetReference target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertExists({Target})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var report = session.ResolveTarget(Target);
                if (report.Status != InkkOopsTargetResolutionStatus.Resolved || report.Element == null)
                {
                    throw new InkkOopsCommandException(
                        report.Status == InkkOopsTargetResolutionStatus.Ambiguous
                            ? InkkOopsFailureCategory.Ambiguous
                            : InkkOopsFailureCategory.Unresolved,
                        $"Expected target '{Target.Name}' to exist.{Environment.NewLine}{report.Describe()}");
                }
            },
            cancellationToken);
    }
}
