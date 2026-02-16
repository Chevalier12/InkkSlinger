using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;

namespace InkkSlinger;

internal static class AnimationPropertyPathResolver
{
    public static AnimationValueSink? Resolve(object root, string propertyPath)
    {
        if (root == null || string.IsNullOrWhiteSpace(propertyPath))
        {
            return null;
        }

        var segments = ParseSegments(propertyPath);
        if (segments.Count == 0)
        {
            return null;
        }

        object? current = root;
        for (var i = 0; i < segments.Count; i++)
        {
            if (current == null)
            {
                return null;
            }

            var isLast = i == segments.Count - 1;
            var segment = segments[i];
            if (isLast)
            {
                return ResolveTerminalSink(current, segment);
            }

            current = ResolveIntermediateValue(current, segment);
        }

        return null;
    }

    private static AnimationValueSink? ResolveTerminalSink(object current, PathSegment segment)
    {
        if (current is DependencyObject dependencyObject)
        {
            var ownerType = segment.OwnerTypeName == null ? dependencyObject.GetType() : ResolveElementType(segment.OwnerTypeName);
            var dp = ResolveDependencyPropertyOnType(ownerType, segment.PropertyName);
            if (dp != null)
            {
                return new DependencyPropertyAnimationSink(dependencyObject, dp);
            }
        }

        var property = ResolveProperty(current.GetType(), segment.OwnerTypeName, segment.PropertyName);
        if (property == null || !property.CanWrite || !property.CanRead)
        {
            return null;
        }

        return new ClrPropertyAnimationSink(current, property);
    }

    private static object? ResolveIntermediateValue(object current, PathSegment segment)
    {
        var property = ResolveProperty(current.GetType(), segment.OwnerTypeName, segment.PropertyName);
        if (property == null || !property.CanRead)
        {
            return null;
        }

        var value = property.GetValue(current);
        if (segment.Indices.Count > 0)
        {
            return ApplyIndices(value, segment.Indices);
        }

        return value;
    }

    private static PropertyInfo? ResolveProperty(Type currentType, string? ownerTypeName, string propertyName)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        if (!string.IsNullOrWhiteSpace(ownerTypeName))
        {
            var ownerType = ResolveElementType(ownerTypeName);
            if (ownerType.IsAssignableFrom(currentType))
            {
                return currentType.GetProperty(propertyName, flags) ?? ownerType.GetProperty(propertyName, flags);
            }
        }

        return currentType.GetProperty(propertyName, flags);
    }

    private static Type ResolveElementType(string typeName)
    {
        var type = typeof(UIElement).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.IsPublic && string.Equals(t.Name, typeName, StringComparison.Ordinal));
        if (type != null)
        {
            return type;
        }

        throw new InvalidOperationException($"Type '{typeName}' could not be resolved for target property path.");
    }

    private static DependencyProperty? ResolveDependencyPropertyOnType(Type ownerType, string propertyName)
    {
        var fieldName = propertyName + "Property";
        for (var current = ownerType; current != null; current = current.BaseType)
        {
            var field = current.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field?.FieldType == typeof(DependencyProperty))
            {
                return (DependencyProperty?)field.GetValue(null);
            }
        }

        return null;
    }

    private static IReadOnlyList<PathSegment> ParseSegments(string propertyPath)
    {
        var segments = new List<string>();
        var start = 0;
        var parenDepth = 0;
        var bracketDepth = 0;

        for (var i = 0; i < propertyPath.Length; i++)
        {
            var ch = propertyPath[i];
            if (ch == '(')
            {
                parenDepth++;
            }
            else if (ch == ')')
            {
                parenDepth = Math.Max(0, parenDepth - 1);
            }
            else if (ch == '[')
            {
                bracketDepth++;
            }
            else if (ch == ']')
            {
                bracketDepth = Math.Max(0, bracketDepth - 1);
            }
            else if (ch == '.' && parenDepth == 0 && bracketDepth == 0)
            {
                var token = propertyPath[start..i].Trim();
                if (token.Length > 0)
                {
                    segments.Add(token);
                }

                start = i + 1;
            }
        }

        var last = propertyPath[start..].Trim();
        if (last.Length > 0)
        {
            segments.Add(last);
        }

        return segments.Select(ParseSegment).ToArray();
    }

    private static PathSegment ParseSegment(string token)
    {
        var indices = new List<PathIndexToken>();
        var baseToken = token.Trim();

        while (true)
        {
            var closeBracket = baseToken.EndsWith("]", StringComparison.Ordinal);
            var openBracket = closeBracket ? baseToken.LastIndexOf('[') : -1;
            if (!closeBracket || openBracket < 0)
            {
                break;
            }

            var indexText = baseToken[(openBracket + 1)..^1].Trim();
            if (indexText.Length == 0)
            {
                break;
            }

            if (int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                indices.Insert(0, new PathIndexToken(numeric));
            }
            else
            {
                if ((indexText.StartsWith("'", StringComparison.Ordinal) && indexText.EndsWith("'", StringComparison.Ordinal)) ||
                    (indexText.StartsWith("\"", StringComparison.Ordinal) && indexText.EndsWith("\"", StringComparison.Ordinal)))
                {
                    indexText = indexText[1..^1];
                }

                indices.Insert(0, new PathIndexToken(indexText));
            }

            baseToken = baseToken[..openBracket].Trim();
        }

        if (baseToken.StartsWith("(", StringComparison.Ordinal) &&
            baseToken.EndsWith(")", StringComparison.Ordinal))
        {
            var inner = baseToken[1..^1].Trim();
            var dot = inner.IndexOf('.');
            if (dot > 0)
            {
                return new PathSegment(inner[..dot].Trim(), inner[(dot + 1)..].Trim(), indices);
            }
        }

        return new PathSegment(null, baseToken, indices);
    }

    private static object? ApplyIndices(object? value, IReadOnlyList<PathIndexToken> indices)
    {
        var current = value;
        foreach (var index in indices)
        {
            current = ApplySingleIndex(current, index);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static object? ApplySingleIndex(object? value, PathIndexToken index)
    {
        if (value == null)
        {
            return null;
        }

        if (index.TryGetInt32(out var numericIndex))
        {
            if (value is IList list)
            {
                if (numericIndex >= 0 && numericIndex < list.Count)
                {
                    return list[numericIndex];
                }

                return null;
            }

            var intIndexer = value.GetType()
                .GetDefaultMembers()
                .OfType<PropertyInfo>()
                .FirstOrDefault(p =>
                {
                    var parameters = p.GetIndexParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(int) && p.CanRead;
                });
            if (intIndexer != null)
            {
                return intIndexer.GetValue(value, new object[] { numericIndex });
            }

            return null;
        }

        var key = index.StringValue;
        if (key == null)
        {
            return null;
        }

        if (value is IDictionary dictionary)
        {
            return dictionary.Contains(key) ? dictionary[key] : null;
        }

        var stringIndexer = value.GetType()
            .GetDefaultMembers()
            .OfType<PropertyInfo>()
            .FirstOrDefault(p =>
            {
                var parameters = p.GetIndexParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string) && p.CanRead;
            });
        if (stringIndexer != null)
        {
            return stringIndexer.GetValue(value, new object[] { key });
        }

        return null;
    }

    private readonly struct PathSegment
    {
        public PathSegment(string? ownerTypeName, string propertyName, IReadOnlyList<PathIndexToken> indices)
        {
            OwnerTypeName = ownerTypeName;
            PropertyName = propertyName;
            Indices = indices;
        }

        public string? OwnerTypeName { get; }

        public string PropertyName { get; }

        public IReadOnlyList<PathIndexToken> Indices { get; }
    }

    private readonly struct PathIndexToken
    {
        private readonly int? _numericValue;

        public PathIndexToken(int numericValue)
        {
            _numericValue = numericValue;
            StringValue = null;
        }

        public PathIndexToken(string stringValue)
        {
            _numericValue = null;
            StringValue = stringValue;
        }

        public string? StringValue { get; }

        public bool TryGetInt32(out int value)
        {
            if (_numericValue.HasValue)
            {
                value = _numericValue.Value;
                return true;
            }

            value = 0;
            return false;
        }
    }
}

internal readonly struct AnimationLaneKey : IEquatable<AnimationLaneKey>
{
    public AnimationLaneKey(object target, object descriptor)
    {
        Target = target;
        Descriptor = descriptor;
    }

    public object Target { get; }

    public object Descriptor { get; }

    public bool Equals(AnimationLaneKey other)
    {
        return ReferenceEquals(Target, other.Target) &&
               ReferenceEquals(Descriptor, other.Descriptor);
    }

    public override bool Equals(object? obj)
    {
        return obj is AnimationLaneKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Target, Descriptor);
    }
}

internal abstract class AnimationValueSink
{
    public abstract AnimationLaneKey Key { get; }

    public abstract Type ValueType { get; }

    public abstract object? GetValue();

    public abstract void SetValue(object? value);

    public abstract void ClearValue(object? restoreValue);
}

internal sealed class DependencyPropertyAnimationSink : AnimationValueSink
{
    public DependencyPropertyAnimationSink(DependencyObject target, DependencyProperty property)
    {
        Target = target;
        Property = property;
    }

    public DependencyObject Target { get; }

    public DependencyProperty Property { get; }

    public override AnimationLaneKey Key => new(Target, Property);

    public override Type ValueType => Property.PropertyType;

    public override object? GetValue() => Target.GetValue(Property);

    public override void SetValue(object? value) => Target.SetAnimationValue(Property, value);

    public override void ClearValue(object? restoreValue) => Target.ClearAnimationValue(Property);
}

internal sealed class ClrPropertyAnimationSink : AnimationValueSink
{
    public ClrPropertyAnimationSink(object target, PropertyInfo property)
    {
        Target = target;
        Property = property;
    }

    public object Target { get; }

    public PropertyInfo Property { get; }

    public override AnimationLaneKey Key => new(Target, Property);

    public override Type ValueType => Property.PropertyType;

    public override object? GetValue() => Property.GetValue(Target);

    public override void SetValue(object? value) => Property.SetValue(Target, value);

    public override void ClearValue(object? restoreValue) => Property.SetValue(Target, restoreValue);
}
