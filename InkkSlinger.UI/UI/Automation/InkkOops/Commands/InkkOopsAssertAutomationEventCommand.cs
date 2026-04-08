using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsAssertAutomationEventCommand : IInkkOopsCommand
{
    public InkkOopsAssertAutomationEventCommand(AutomationEventType eventType, string targetName = "", string propertyName = "")
    {
        EventType = eventType;
        TargetName = targetName ?? string.Empty;
        PropertyName = propertyName ?? string.Empty;
    }

    public AutomationEventType EventType { get; }

    public string TargetName { get; }

    public string PropertyName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertAutomationEvent({EventType}, target: {TargetName}, property: {PropertyName})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var events = session.GetAutomationEventsSnapshot();
                var match = events.FirstOrDefault(entry =>
                    entry.EventType == EventType &&
                    (string.IsNullOrWhiteSpace(PropertyName) || string.Equals(entry.PropertyName, PropertyName, StringComparison.Ordinal)));

                if (match == default)
                {
                    throw new InvalidOperationException(
                        $"Expected automation event '{EventType}' for target '{TargetName}' property '{PropertyName}', but no matching event was captured.");
                }

                if (!string.IsNullOrWhiteSpace(TargetName))
                {
                    var report = session.ResolveTarget(new InkkOopsTargetReference(TargetName));
                    if (report.Peer == null || report.Peer.RuntimeId != match.PeerRuntimeId)
                    {
                        throw new InvalidOperationException(
                            $"Captured event '{EventType}' did not belong to target '{TargetName}'.");
                    }
                }
            },
            cancellationToken);
    }
}
