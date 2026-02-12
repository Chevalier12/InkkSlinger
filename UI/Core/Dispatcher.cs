using System;
using System.Threading;

namespace InkkSlinger;

public static class Dispatcher
{
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

    public static void ResetForTests()
    {
        _uiThreadId = null;
    }

    private static void EnsureInitialized()
    {
        _uiThreadId ??= Environment.CurrentManagedThreadId;
    }
}
