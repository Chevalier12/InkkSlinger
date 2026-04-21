using System;
using System.Collections.Generic;
using System.Text;

namespace InkkSlinger;

internal static class InkkOopsObjectObserverParser
{
    public static InkkOopsObjectObserver[] Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var parts = text.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var observers = new List<InkkOopsObjectObserver>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            observers.Add(Create(parts[i]));
        }

        return [.. observers];
    }

    private static InkkOopsObjectObserver Create(string name)
    {
        switch (Normalize(name))
        {
            case "sourceeditorgutter":
            case "sourceeditorgutterobjectobserver":
                return new SourceEditorGutterObjectObserver();
            default:
                throw new ArgumentException(
                    $"Unknown InkkOops object observer '{name}'. Known observers: source-editor-gutter.",
                    nameof(name));
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}