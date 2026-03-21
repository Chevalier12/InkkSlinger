using System.Diagnostics;

namespace InkkSlinger.Tests;

public static class Instrumentation
{
    /// <summary>
    /// Writes a line to the test output that can be captured by InstrumentationCapture
    /// and also appears in the test console output.
    /// </summary>
    [DebuggerStepThrough]
    public static void Trace(string message)
    {
        var line = $"[INSTRUMENT] {message}";
        System.Diagnostics.Trace.WriteLine(line);
        Console.Out.WriteLine(line);
    }

    /// <summary>
    /// Writes a timing entry in the standard format parsed by InstrumentationCapture.TryParseTiming.
    /// Example: "MyMethod took 1234us"
    /// </summary>
    [DebuggerStepThrough]
    public static void TraceTiming(string method, long microseconds)
    {
        Trace($"{method} took {microseconds}us");
    }

    /// <summary>
    /// Writes a counter entry in the standard format parsed by InstrumentationCapture.TryParseCounter.
    /// Example: "MyMethod #42"
    /// </summary>
    [DebuggerStepThrough]
    public static void TraceCounter(string method, int count)
    {
        Trace($"{method} #{count}");
    }
}
