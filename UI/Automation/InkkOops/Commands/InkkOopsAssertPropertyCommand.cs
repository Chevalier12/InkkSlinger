using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsAssertPropertyCommand : IInkkOopsCommand
{
    public InkkOopsAssertPropertyCommand(InkkOopsTargetReference target, string propertyName, object? expectedValue)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        PropertyName = string.IsNullOrWhiteSpace(propertyName)
            ? throw new ArgumentException("Property name is required.", nameof(propertyName))
            : propertyName;
        ExpectedValue = expectedValue;
    }

    public InkkOopsTargetReference Target { get; }

    public string PropertyName { get; }

    public object? ExpectedValue { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertProperty({Target}, {PropertyName}, {InkkOopsCommandUtilities.FormatObject(ExpectedValue)})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.ExecuteOnUiThreadAsync(
            () =>
            {
                var element = session.ResolveRequiredTarget(Target);
                var actual = InkkOopsCommandUtilities.ReadPropertyValue(element, PropertyName);
                if (!Equals(actual, ExpectedValue))
                {
                    throw new InkkOopsCommandException(
                        InkkOopsFailureCategory.None,
                        $"Property assertion failed for '{Target.Name}.{PropertyName}'. Expected '{InkkOopsCommandUtilities.FormatObject(ExpectedValue)}', actual '{InkkOopsCommandUtilities.FormatObject(actual)}'.");
                }
            },
            cancellationToken);
    }
}
