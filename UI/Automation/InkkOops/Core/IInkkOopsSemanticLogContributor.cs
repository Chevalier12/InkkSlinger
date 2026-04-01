using System;
using System.Collections.Generic;

namespace InkkSlinger;

public enum InkkOopsSemanticLogTarget
{
    RawTarget,
    Owner,
    Hovered,
    Captured
}

public sealed class InkkOopsSemanticLogContext
{
    public required int CommandIndex { get; init; }

    public required string CommandDescription { get; init; }

    public required string CommandKind { get; init; }

    public required InkkOopsSemanticLogTarget Target { get; init; }
}

public interface IInkkOopsSemanticLogContributor
{
    int Order { get; }

    void Contribute(InkkOopsSemanticLogContext context, UIElement element, InkkOopsSemanticLogPropertyBuilder builder);
}

public sealed class InkkOopsSemanticLogPropertyBuilder
{
    private readonly List<string> _properties = new();

    public IReadOnlyList<string> Properties => _properties;

    public void Add(string propertyName, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name is required.", nameof(propertyName));
        }

        _properties.Add($"{propertyName}={FormatValue(value)}");
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool boolean => boolean ? "True" : "False",
            float number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            double number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}