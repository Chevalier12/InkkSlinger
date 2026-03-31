using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InkkSlinger;

public sealed class InkkOopsDiagnosticsContext
{
    public required UiRoot UiRoot { get; init; }

    public required LayoutRect Viewport { get; init; }

    public required UIElement? HoveredElement { get; init; }

    public required UIElement? FocusedElement { get; init; }

    public string ArtifactName { get; init; } = string.Empty;

    public InkkOopsDiagnosticsFilter Filter { get; init; } = InkkOopsDiagnosticsFilter.None;
}

public enum InkkOopsDiagnosticsComparison
{
    Exists,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains
}

public enum InkkOopsDiagnosticsNodeRetention
{
    All,
    MatchedNodesAndAncestors
}

public sealed class InkkOopsDiagnosticsFactRule
{
    public string? DisplayNameContains { get; init; }

    public string? ElementTypeName { get; init; }

    public required string Key { get; init; }

    public InkkOopsDiagnosticsComparison Comparison { get; init; } = InkkOopsDiagnosticsComparison.Exists;

    public object? Value { get; init; }

    public bool Matches(string displayName, string elementTypeName, string key, object value, string formattedValue)
    {
        if (!string.Equals(Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ElementTypeName)
            && !string.Equals(ElementTypeName, elementTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(DisplayNameContains)
            && displayName.IndexOf(DisplayNameContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return Comparison switch
        {
            InkkOopsDiagnosticsComparison.Exists => true,
            InkkOopsDiagnosticsComparison.Equal => MatchesEquality(value, formattedValue, expectedEqual: true),
            InkkOopsDiagnosticsComparison.NotEqual => MatchesEquality(value, formattedValue, expectedEqual: false),
            InkkOopsDiagnosticsComparison.GreaterThan => MatchesNumeric(value, static (actual, expected) => actual > expected),
            InkkOopsDiagnosticsComparison.GreaterThanOrEqual => MatchesNumeric(value, static (actual, expected) => actual >= expected),
            InkkOopsDiagnosticsComparison.LessThan => MatchesNumeric(value, static (actual, expected) => actual < expected),
            InkkOopsDiagnosticsComparison.LessThanOrEqual => MatchesNumeric(value, static (actual, expected) => actual <= expected),
            InkkOopsDiagnosticsComparison.Contains => formattedValue.IndexOf(Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0,
            _ => false
        };
    }

    private bool MatchesEquality(object value, string formattedValue, bool expectedEqual)
    {
        var expected = Value;
        if (expected is null)
        {
            return !expectedEqual;
        }

        if (TryGetBoolean(value, out var actualBool) && TryGetBoolean(expected, out var expectedBool))
        {
            return (actualBool == expectedBool) == expectedEqual;
        }

        if (TryGetDouble(value, out var actualNumber) && TryGetDouble(expected, out var expectedNumber))
        {
            return (Math.Abs(actualNumber - expectedNumber) < 0.000001d) == expectedEqual;
        }

        var equals = string.Equals(formattedValue, expected.ToString(), StringComparison.OrdinalIgnoreCase);
        return equals == expectedEqual;
    }

    private bool MatchesNumeric(object value, Func<double, double, bool> comparison)
    {
        return TryGetDouble(value, out var actual)
            && TryGetDouble(Value, out var expected)
            && comparison(actual, expected);
    }

    private static bool TryGetBoolean(object? value, out bool result)
    {
        switch (value)
        {
            case bool boolValue:
                result = boolValue;
                return true;
            case string stringValue when bool.TryParse(stringValue, out var parsed):
                result = parsed;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool TryGetDouble(object? value, out double result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue:
                result = doubleValue;
                return true;
            case decimal decimalValue:
                result = (double)decimalValue;
                return true;
            case string stringValue:
                return double.TryParse(stringValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
            default:
                result = 0d;
                return false;
        }
    }
}

public sealed class InkkOopsDiagnosticsFilter
{
    public static InkkOopsDiagnosticsFilter None { get; } = new();

    public IReadOnlyList<InkkOopsDiagnosticsFactRule> Rules { get; init; } = Array.Empty<InkkOopsDiagnosticsFactRule>();

    public InkkOopsDiagnosticsNodeRetention NodeRetention { get; init; } = InkkOopsDiagnosticsNodeRetention.All;

    public bool IsActive => Rules.Count > 0;

    public bool ShouldInclude(string displayName, string elementTypeName, string key, object rawValue, string formattedValue)
    {
        if (!IsActive)
        {
            return true;
        }

        return Rules.Any(rule => rule.Matches(displayName, elementTypeName, key, rawValue, formattedValue));
    }
}

public sealed class InkkOopsElementDiagnosticsBuilder
{
    private readonly List<KeyValuePair<string, string>> _facts = new();
    private readonly string _displayName;
    private readonly string _elementTypeName;
    private readonly InkkOopsDiagnosticsFilter _filter;

    public InkkOopsElementDiagnosticsBuilder(
        string displayName = "",
        string elementTypeName = "",
        InkkOopsDiagnosticsFilter? filter = null)
    {
        _displayName = displayName;
        _elementTypeName = elementTypeName;
        _filter = filter ?? InkkOopsDiagnosticsFilter.None;
    }

    public IReadOnlyList<KeyValuePair<string, string>> Facts => _facts;

    public bool MatchedFilter { get; private set; }

    public void Add(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (value is null)
        {
            return;
        }

        var text = value switch
        {
            string stringValue => stringValue,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_filter.ShouldInclude(_displayName, _elementTypeName, key, value, text))
        {
            return;
        }

        MatchedFilter = _filter.IsActive;
        _facts.Add(new KeyValuePair<string, string>(key, text));
    }
}

public sealed class InkkOopsVisualTreeNodeSnapshot
{
    public required int Depth { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> Facts { get; init; }

    public bool MatchedFilter { get; init; }
}

public sealed class InkkOopsVisualTreeSnapshot
{
    public required IReadOnlyList<InkkOopsVisualTreeNodeSnapshot> Nodes { get; init; }

    public InkkOopsDiagnosticsNodeRetention NodeRetention { get; init; } = InkkOopsDiagnosticsNodeRetention.All;

    public bool IsFiltered { get; init; }
}
