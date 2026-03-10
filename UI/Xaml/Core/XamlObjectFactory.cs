using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace InkkSlinger;

internal static class XamlObjectFactory
{
    private static readonly ConcurrentDictionary<Type, Func<object?>> Activators = new();

    public static object? CreateInstance(Type type)
    {
        return Activators.GetOrAdd(type, CreateActivator)();
    }

    private static Func<object?> CreateActivator(Type type)
    {
        var defaultConstructor = type.GetConstructor(Type.EmptyTypes);
        if (defaultConstructor != null)
        {
            var newExpression = Expression.New(defaultConstructor);
            var convertExpression = Expression.Convert(newExpression, typeof(object));
            return Expression.Lambda<Func<object?>>(convertExpression).Compile();
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var allOptional = true;
            var arguments = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOptional)
                {
                    allOptional = false;
                    break;
                }

                arguments[i] = Type.Missing;
            }

            if (!allOptional)
            {
                continue;
            }

            return () => constructor.Invoke(arguments);
        }

        return static () => null;
    }
}
