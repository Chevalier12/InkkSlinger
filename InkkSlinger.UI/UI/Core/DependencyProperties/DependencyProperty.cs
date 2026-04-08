using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DependencyProperty
{
    private static readonly List<DependencyProperty> RegisteredProperties = new();
    private readonly Dictionary<Type, FrameworkPropertyMetadata> _metadataByType;

    private DependencyProperty(
        string name,
        Type propertyType,
        Type ownerType,
        FrameworkPropertyMetadata metadata,
        ValidateValueCallback? validateValueCallback,
        bool isAttached)
    {
        Name = name;
        PropertyType = propertyType;
        OwnerType = ownerType;
        ValidateValueCallback = validateValueCallback;
        IsAttached = isAttached;

        _metadataByType = new Dictionary<Type, FrameworkPropertyMetadata>
        {
            [ownerType] = metadata
        };
    }

    public string Name { get; }

    public Type PropertyType { get; }

    public Type OwnerType { get; }

    public ValidateValueCallback? ValidateValueCallback { get; }

    public bool IsAttached { get; }

    public static DependencyProperty Register(
        string name,
        Type propertyType,
        Type ownerType,
        FrameworkPropertyMetadata? metadata = null,
        ValidateValueCallback? validateValueCallback = null)
    {
        return RegisterCore(name, propertyType, ownerType, metadata, validateValueCallback, isAttached: false);
    }

    public static DependencyProperty RegisterAttached(
        string name,
        Type propertyType,
        Type ownerType,
        FrameworkPropertyMetadata? metadata = null,
        ValidateValueCallback? validateValueCallback = null)
    {
        return RegisterCore(name, propertyType, ownerType, metadata, validateValueCallback, isAttached: true);
    }

    public static IEnumerable<DependencyProperty> GetRegisteredProperties()
    {
        return RegisteredProperties;
    }

    public bool IsApplicableTo(Type type)
    {
        return IsAttached || OwnerType.IsAssignableFrom(type);
    }

    public bool IsApplicableTo(DependencyObject dependencyObject)
    {
        return IsApplicableTo(dependencyObject.GetType());
    }

    public FrameworkPropertyMetadata GetMetadata(Type forType)
    {
        if (!IsApplicableTo(forType))
        {
            throw new InvalidOperationException($"Type {forType.Name} is not compatible with {OwnerType.Name}.");
        }

        for (var current = forType; current != null; current = current.BaseType)
        {
            if (_metadataByType.TryGetValue(current, out var metadata))
            {
                return metadata;
            }
        }

        return _metadataByType[OwnerType];
    }

    public FrameworkPropertyMetadata GetMetadata(DependencyObject dependencyObject)
    {
        return GetMetadata(dependencyObject.GetType());
    }

    public void OverrideMetadata(Type forType, FrameworkPropertyMetadata metadata)
    {
        if (!IsApplicableTo(forType))
        {
            throw new InvalidOperationException($"Type {forType.Name} is not compatible with {OwnerType.Name}.");
        }

        _metadataByType[forType] = metadata;
    }

    public override string ToString()
    {
        return $"{OwnerType.Name}.{Name}";
    }

    private static DependencyProperty RegisterCore(
        string name,
        Type propertyType,
        Type ownerType,
        FrameworkPropertyMetadata? metadata,
        ValidateValueCallback? validateValueCallback,
        bool isAttached)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name cannot be null or empty.", nameof(name));
        }

        metadata ??= new FrameworkPropertyMetadata();
        var dependencyProperty = new DependencyProperty(
            name,
            propertyType,
            ownerType,
            metadata,
            validateValueCallback,
            isAttached);
        RegisteredProperties.Add(dependencyProperty);
        return dependencyProperty;
    }
}
