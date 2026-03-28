using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsAssertNotExistsCommand : IInkkOopsCommand
{
    public InkkOopsAssertNotExistsCommand(InkkOopsTargetReference target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public InkkOopsTargetReference Target { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertNotExists({Target})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var report = session.ResolveTarget(Target);
                if (report.Status == InkkOopsTargetResolutionStatus.Resolved && report.Element != null)
                {
                    throw new InkkOopsCommandException(
                        InkkOopsFailureCategory.None,
                        $"Expected target '{Target.Name}' to be absent, but resolved to {report.Source}.{Environment.NewLine}{report.Describe()}");
                }
            },
            cancellationToken);
    }
}
