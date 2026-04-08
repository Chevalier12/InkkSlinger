using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public sealed class FontFamily : IEquatable<FontFamily>
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private readonly string[] _familyNames;

    public static FontFamily Empty { get; } = new(string.Empty);

    public FontFamily()
        : this(string.Empty)
    {
    }

    public FontFamily(string? source)
    {
        _familyNames = ParseFamilyNames(source).ToArray();
        Source = _familyNames.Length == 0
            ? string.Empty
            : string.Join(", ", _familyNames);
    }

    public string Source { get; }

    public bool IsEmpty => _familyNames.Length == 0;

    public string PrimaryFamilyName => _familyNames.Length == 0 ? string.Empty : _familyNames[0];

    public IReadOnlyList<string> FamilyNames => _familyNames;

    public static IEnumerable<string> ParseFamilyNames(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            yield break;
        }

        var segments = source.Split(',', StringSplitOptions.TrimEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            var familyName = segments[i].Trim();
            if (familyName.Length == 0)
            {
                continue;
            }

            yield return familyName;
        }
    }

    public bool Equals(FontFamily? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || _familyNames.Length != other._familyNames.Length)
        {
            return false;
        }

        for (var i = 0; i < _familyNames.Length; i++)
        {
            if (!Comparer.Equals(_familyNames[i], other._familyNames[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj switch
        {
            FontFamily family => Equals(family),
            string text => Equals(new FontFamily(text)),
            _ => false
        };
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (var i = 0; i < _familyNames.Length; i++)
        {
            hash.Add(_familyNames[i], Comparer);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return Source;
    }

    public static implicit operator FontFamily(string? source)
    {
        return new FontFamily(source);
    }

    public static implicit operator string(FontFamily? family)
    {
        return family?.Source ?? string.Empty;
    }
}