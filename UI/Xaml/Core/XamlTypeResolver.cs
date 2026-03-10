using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace InkkSlinger;

internal static class XamlTypeResolver
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> WritableProperties = new();
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), DependencyProperty?> DependencyProperties = new();
    private static readonly ConcurrentDictionary<(Type OwnerType, Type TargetType, string PropertyName), MethodInfo?> AttachedSettersByTarget = new();
    private static readonly ConcurrentDictionary<(Type OwnerType, Type TargetType, string PropertyName, Type ValueType), MethodInfo?> AttachedSetters = new();
    private static readonly ConcurrentDictionary<(Type Type, string EventName), EventInfo?> Events = new();
    private static readonly ConcurrentDictionary<(Type Type, string MethodName), MethodInfo?> Methods = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), MemberInfo?> StaticMembers = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), FieldInfo?> Fields = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), PropertyInfo?> Properties = new();

    public static PropertyInfo? GetWritableProperty(Type type, string propertyName)
    {
        return WritableProperties.GetOrAdd((type, propertyName), key =>
        {
            var property = key.Type.GetProperty(
                key.PropertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            return property is { CanWrite: true } ? property : property;
        });
    }

    public static DependencyProperty? ResolveDependencyProperty(Type targetType, string propertyName)
    {
        return DependencyProperties.GetOrAdd((targetType, propertyName), key =>
        {
            var fieldName = key.PropertyName + "Property";
            var current = key.Type;
            while (current != null)
            {
                var field = current.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field?.FieldType == typeof(DependencyProperty))
                {
                    return (DependencyProperty?)field.GetValue(null);
                }

                current = current.BaseType;
            }

            return null;
        });
    }

    public static MethodInfo? ResolveAttachedSetter(Type ownerType, Type targetType, string propertyName, Type valueType)
    {
        return AttachedSetters.GetOrAdd((ownerType, targetType, propertyName, valueType), key =>
        {
            var setterName = $"Set{key.PropertyName}";
            var ownerSetter = FindCompatibleAttachedSetter(key.OwnerType, setterName, key.TargetType, key.ValueType);
            if (ownerSetter != null)
            {
                return ownerSetter;
            }

            foreach (var candidateType in XamlLoader.TypeByName.Values)
            {
                var method = FindCompatibleAttachedSetter(candidateType, setterName, key.TargetType, key.ValueType);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        });
    }

    public static MethodInfo? ResolveAttachedSetterForTarget(Type ownerType, Type targetType, string propertyName)
    {
        return AttachedSettersByTarget.GetOrAdd((ownerType, targetType, propertyName), key =>
        {
            var setterName = $"Set{key.PropertyName}";
            var ownerSetter = FindCompatibleAttachedSetterForTarget(key.OwnerType, setterName, key.TargetType);
            if (ownerSetter != null)
            {
                return ownerSetter;
            }

            foreach (var candidateType in XamlLoader.TypeByName.Values)
            {
                var method = FindCompatibleAttachedSetterForTarget(candidateType, setterName, key.TargetType);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        });
    }

    public static EventInfo? GetEvent(Type targetType, string eventName)
    {
        return Events.GetOrAdd(
            (targetType, eventName),
            key => key.Type.GetEvent(key.EventName, BindingFlags.Instance | BindingFlags.Public));
    }

    public static MethodInfo? GetInstanceMethod(Type type, string methodName)
    {
        return Methods.GetOrAdd(
            (type, methodName),
            key => key.Type.GetMethod(key.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    public static FieldInfo? GetInstanceField(Type type, string memberName)
    {
        return Fields.GetOrAdd(
            (type, memberName),
            key => key.Type.GetField(key.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    public static PropertyInfo? GetInstanceProperty(Type type, string memberName)
    {
        return Properties.GetOrAdd(
            (type, memberName),
            key => key.Type.GetProperty(key.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    public static bool TryResolveStaticMember(Type ownerType, string memberName, out object resolved)
    {
        var member = StaticMembers.GetOrAdd((ownerType, memberName), key =>
        {
            return (MemberInfo?)key.Type.GetField(
                       key.MemberName,
                       BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                   ?? key.Type.GetProperty(
                       key.MemberName,
                       BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        });

        switch (member)
        {
            case FieldInfo field:
                resolved = field.GetValue(null)!;
                return true;
            case PropertyInfo property when property.CanRead && property.GetIndexParameters().Length == 0:
                resolved = property.GetValue(null)!;
                return true;
            default:
                resolved = null!;
                return false;
        }
    }

    private static MethodInfo? FindCompatibleAttachedSetter(Type ownerType, string setterName, Type targetType, Type valueType)
    {
        foreach (var method in ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (!IsCompatibleAttachedSetter(method, setterName, targetType, valueType))
            {
                continue;
            }

            return method;
        }

        return null;
    }

    private static MethodInfo? FindCompatibleAttachedSetterForTarget(Type ownerType, string setterName, Type targetType)
    {
        foreach (var method in ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != setterName)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 2)
            {
                continue;
            }

            if (parameters[0].ParameterType.IsAssignableFrom(targetType))
            {
                return method;
            }
        }

        return null;
    }

    private static bool IsCompatibleAttachedSetter(MethodInfo method, string setterName, Type targetType, Type valueType)
    {
        if (method.Name != setterName)
        {
            return false;
        }

        var parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            return false;
        }

        return parameters[0].ParameterType.IsAssignableFrom(targetType) &&
               parameters[1].ParameterType.IsAssignableFrom(valueType);
    }
}
