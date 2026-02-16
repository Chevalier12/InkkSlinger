using System;
using System.Collections.Generic;

namespace InkkSlinger;

public static class Dispatcher
{
    private const int DefaultDrainLimit = 4096;
    private static readonly object DeferredLock = new();
    private static readonly Queue<Action> DeferredOperations = new();
    private static int? _uiThreadId;

    public static void InitializeForCurrentThread()
    {
        var threadId = Environment.CurrentManagedThreadId;
        if (_uiThreadId == null)
        {
            _uiThreadId = threadId;
            return;
        }

        if (_uiThreadId != threadId)
        {
            throw new InvalidOperationException("UI dispatcher is already bound to a different thread.");
        }
    }

    public static bool CheckAccess()
    {
        EnsureInitialized();
        return _uiThreadId == Environment.CurrentManagedThreadId;
    }

    public static void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException("Cross-thread UI access is not allowed.");
        }
    }

    public static int PendingDeferredOperationCount
    {
        get
        {
            lock (DeferredLock)
            {
                return DeferredOperations.Count;
            }
        }
    }

    public static void EnqueueDeferred(Action operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        lock (DeferredLock)
        {
            DeferredOperations.Enqueue(operation);
        }
    }

    internal static int DrainDeferredOperations(int maxOperations = DefaultDrainLimit)
    {
        VerifyAccess();
        if (maxOperations <= 0)
        {
            return 0;
        }

        var executed = 0;
        while (executed < maxOperations)
        {
            Action? operation;
            lock (DeferredLock)
            {
                if (DeferredOperations.Count == 0)
                {
                    break;
                }

                operation = DeferredOperations.Dequeue();
            }

            operation();
            executed++;
        }

        return executed;
    }

    public static void ResetForTests()
    {
        _uiThreadId = null;
        lock (DeferredLock)
        {
            DeferredOperations.Clear();
        }
    }

    private static void EnsureInitialized()
    {
        _uiThreadId ??= Environment.CurrentManagedThreadId;
    }
}
