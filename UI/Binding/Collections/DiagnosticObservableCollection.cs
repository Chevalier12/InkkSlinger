using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;

namespace InkkSlinger;

public sealed class DiagnosticObservableCollection<T> : ObservableCollection<T>
{
    private static readonly FieldInfo? CollectionChangedField = ResolveCollectionChangedField();
    private readonly string _diagnosticName;

    public DiagnosticObservableCollection(string diagnosticName)
    {
        _diagnosticName = diagnosticName;
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!SourceCollectionDispatchDiagnostics.Enabled)
        {
            base.OnCollectionChanged(e);
            return;
        }

        var handlers = CollectionChangedField?.GetValue(this) as NotifyCollectionChangedEventHandler;
        if (handlers == null)
        {
            base.OnCollectionChanged(e);
            return;
        }

        var invocationList = handlers.GetInvocationList();
        var timings = new List<SourceCollectionHandlerTiming>(invocationList.Length);
        var totalStart = Stopwatch.GetTimestamp();

        using (BlockReentrancy())
        {
            for (var i = 0; i < invocationList.Length; i++)
            {
                var handler = (NotifyCollectionChangedEventHandler)invocationList[i];
                var handlerStart = Stopwatch.GetTimestamp();
                try
                {
                    handler(this, e);
                }
                finally
                {
                    timings.Add(new SourceCollectionHandlerTiming(
                        BuildHandlerKey(handler),
                        Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds));
                }
            }
        }

        SourceCollectionDispatchDiagnostics.Observe(
            _diagnosticName,
            e.Action,
            Count,
            Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds,
            timings);
    }

    private static FieldInfo? ResolveCollectionChangedField()
    {
        var fields = typeof(ObservableCollection<T>).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        for (var i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType == typeof(NotifyCollectionChangedEventHandler))
            {
                return fields[i];
            }
        }

        return null;
    }

    private static string BuildHandlerKey(Delegate handler)
    {
        var method = handler.Method;
        var owner = method.DeclaringType?.Name ?? "<anon>";
        var target = handler.Target?.GetType().Name;
        return target == null
            ? $"{owner}.{method.Name}"
            : $"{owner}.{method.Name}#{target}";
    }
}
