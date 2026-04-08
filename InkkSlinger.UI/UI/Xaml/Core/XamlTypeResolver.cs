using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace InkkSlinger;

internal static class XamlTypeResolver
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> WritableProperties = new();
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), DependencyProperty?> DependencyProperties = new();
    private static readonly ConcurrentDictionary<(Type OwnerType, Type TargetType, string PropertyName), MethodInfo?> AttachedSettersByTarget = new();
    private static readonly ConcurrentDictionary<(Type OwnerType, Type TargetType, string PropertyName, Type ValueType), MethodInfo?> AttachedSetters = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, MethodInfo[]>> StaticMethodsByName = new();
    private static readonly ConcurrentDictionary<string, MethodInfo[]> GlobalAttachedSetterCandidates = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type Type, string EventName), EventInfo?> Events = new();
    private static readonly ConcurrentDictionary<(Type Type, string MethodName), MethodInfo?> Methods = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), MemberInfo?> StaticMembers = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), FieldInfo?> Fields = new();
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), PropertyInfo?> Properties = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), Action<object, object>?> NameAssigners = new();
    private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> MethodParameters = new();

    public static PropertyInfo? GetWritableProperty(Type type, string propertyName)
    {
        return WritableProperties.GetOrAdd((type, propertyName), key =>
        {
            return FindPropertyInHierarchy(
                key.Type,
                key.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);
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

            foreach (var method in GetGlobalAttachedSetterCandidates(setterName))
            {
                if (IsCompatibleAttachedSetter(method, setterName, key.TargetType, key.ValueType))
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

            foreach (var method in GetGlobalAttachedSetterCandidates(setterName))
            {
                if (IsCompatibleAttachedSetterForTarget(method, setterName, key.TargetType))
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
            key => FindPropertyInHierarchy(
                key.Type,
                key.MemberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
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

    public static Action<object, object>? GetCodeBehindNameAssigner(Type codeBehindType, string name)
    {
        return NameAssigners.GetOrAdd((codeBehindType, name), static key =>
        {
            var field = GetInstanceField(key.Type, key.Name);
            if (field != null)
            {
                var targetParameter = Expression.Parameter(typeof(object), "target");
                var valueParameter = Expression.Parameter(typeof(object), "value");
                var assign = Expression.Assign(
                    Expression.Field(Expression.Convert(targetParameter, key.Type), field),
                    Expression.Convert(valueParameter, field.FieldType));
                return Expression.Lambda<Action<object, object>>(assign, targetParameter, valueParameter).Compile();
            }

            var property = GetInstanceProperty(key.Type, key.Name);
            if (property is { CanWrite: true } settableProperty)
            {
                var targetParameter = Expression.Parameter(typeof(object), "target");
                var valueParameter = Expression.Parameter(typeof(object), "value");
                var assign = Expression.Assign(
                    Expression.Property(Expression.Convert(targetParameter, key.Type), settableProperty),
                    Expression.Convert(valueParameter, settableProperty.PropertyType));
                return Expression.Lambda<Action<object, object>>(assign, targetParameter, valueParameter).Compile();
            }

            return null;
        });
    }

    public static ParameterInfo[] GetMethodParameters(MethodInfo method)
    {
        return MethodParameters.GetOrAdd(method, static candidate => candidate.GetParameters());
    }

    private static PropertyInfo? FindPropertyInHierarchy(Type type, string propertyName, BindingFlags bindingFlags)
    {
        var current = type;
        while (current != null)
        {
            var properties = current.GetProperties(bindingFlags | BindingFlags.DeclaredOnly);
            for (var i = 0; i < properties.Length; i++)
            {
                var candidate = properties[i];
                if (string.Equals(candidate.Name, propertyName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            current = current.BaseType;
        }

        return null;
    }

    private static MethodInfo? FindCompatibleAttachedSetter(Type ownerType, string setterName, Type targetType, Type valueType)
    {
        if (!GetStaticMethodsByName(ownerType).TryGetValue(setterName, out var methods))
        {
            return null;
        }

        foreach (var method in methods)
        {
            if (IsCompatibleAttachedSetter(method, setterName, targetType, valueType))
            {
                return method;
            }
        }

        return null;
    }

    private static MethodInfo? FindCompatibleAttachedSetterForTarget(Type ownerType, string setterName, Type targetType)
    {
        if (!GetStaticMethodsByName(ownerType).TryGetValue(setterName, out var methods))
        {
            return null;
        }

        foreach (var method in methods)
        {
            if (IsCompatibleAttachedSetterForTarget(method, setterName, targetType))
            {
                return method;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, MethodInfo[]> GetStaticMethodsByName(Type type)
    {
        return StaticMethodsByName.GetOrAdd(type, static ownerType =>
        {
            var buckets = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);
            foreach (var method in ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!buckets.TryGetValue(method.Name, out var methods))
                {
                    methods = new List<MethodInfo>();
                    buckets[method.Name] = methods;
                }

                methods.Add(method);
            }

            var result = new Dictionary<string, MethodInfo[]>(buckets.Count, StringComparer.Ordinal);
            foreach (var pair in buckets)
            {
                result[pair.Key] = pair.Value.ToArray();
            }

            return result;
        });
    }

    private static MethodInfo[] GetGlobalAttachedSetterCandidates(string setterName)
    {
        return GlobalAttachedSetterCandidates.GetOrAdd(setterName, static name =>
        {
            var methods = new List<MethodInfo>();
            var seenTypes = new HashSet<Type>();
            foreach (var candidateType in XamlLoader.TypeByName.Values)
            {
                if (!seenTypes.Add(candidateType))
                {
                    continue;
                }

                if (!GetStaticMethodsByName(candidateType).TryGetValue(name, out var candidateMethods))
                {
                    continue;
                }

                methods.AddRange(candidateMethods);
            }

            return methods.ToArray();
        });
    }

    private static bool IsCompatibleAttachedSetterForTarget(MethodInfo method, string setterName, Type targetType)
    {
        if (method.Name != setterName)
        {
            return false;
        }

        var parameters = GetMethodParameters(method);
        return parameters.Length == 2 &&
               parameters[0].ParameterType.IsAssignableFrom(targetType);
    }

    private static bool IsCompatibleAttachedSetter(MethodInfo method, string setterName, Type targetType, Type valueType)
    {
        if (method.Name != setterName)
        {
            return false;
        }

        var parameters = GetMethodParameters(method);
        if (parameters.Length != 2)
        {
            return false;
        }

        return parameters[0].ParameterType.IsAssignableFrom(targetType) &&
               parameters[1].ParameterType.IsAssignableFrom(valueType);
    }
}
