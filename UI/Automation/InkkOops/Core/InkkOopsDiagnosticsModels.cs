using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkkSlinger;

public sealed class InkkOopsDiagnosticsContext
{
    public required UiRoot UiRoot { get; init; }

    public required LayoutRect Viewport { get; init; }

    public required UIElement? HoveredElement { get; init; }

    public required UIElement? FocusedElement { get; init; }
}

public sealed class InkkOopsElementDiagnosticsBuilder
{
    private readonly List<KeyValuePair<string, string>> _facts = new();

    public IReadOnlyList<KeyValuePair<string, string>> Facts => _facts;

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

        _facts.Add(new KeyValuePair<string, string>(key, text));
    }
}

public sealed class InkkOopsVisualTreeNodeSnapshot
{
    public required int Depth { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> Facts { get; init; }
}

public sealed class InkkOopsVisualTreeSnapshot
{
    public required IReadOnlyList<InkkOopsVisualTreeNodeSnapshot> Nodes { get; init; }
}
