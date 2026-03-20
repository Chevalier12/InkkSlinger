using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace InkkSlinger.Tests.TestDoubles;

/// <summary>
/// Captures instrumentation output emitted via System.Diagnostics.Trace.WriteLine
/// during a test. Thread-safe and disposable.
///
/// Usage:
/// ```csharp
/// using var capture = new InstrumentationCapture();
/// // exercise instrumented code
/// var lines = capture.GetInstrumentLines();
/// foreach (var line in lines) { /* parse */ }
/// ```
/// </summary>
public sealed class InstrumentationCapture : System.IDisposable
{
    private readonly StringWriter _writer;
    private readonly TraceListener _listener;
    private bool _disposed;

    public InstrumentationCapture()
    {
        _writer = new StringWriter();
        _listener = new TextWriterTraceListener(_writer) { TraceOutputOptions = TraceOptions.None };

        Trace.Listeners.Add(_listener);
    }

    /// <summary>
    /// Returns all captured lines that contain "[INSTRUMENT]".
    /// </summary>
    public IReadOnlyList<string> GetInstrumentLines()
    {
        var output = _writer.ToString();
        var lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (line.Contains("[INSTRUMENT]"))
                result.Add(line);
        }
        return result;
    }

    /// <summary>
    /// Parses a timing entry like "[INSTRUMENT] MyMethod took 1234us" → ("MyMethod", 1234).
    /// </summary>
    public static (string method, long microseconds)? TryParseTiming(string line)
    {
        var m = Regex.Match(line, @"\[INSTRUMENT\]\s+(\w+)\s+took\s+(\d+)us");
        if (m.Success && long.TryParse(m.Groups[2].Value, out var us))
            return (m.Groups[1].Value, us);
        return null;
    }

    /// <summary>
    /// Parses a counter entry like "[INSTRUMENT] MyMethod #42" → ("MyMethod", 42).
    /// </summary>
    public static (string method, int count)? TryParseCounter(string line)
    {
        var m = Regex.Match(line, @"\[INSTRUMENT\]\s+(\w+)\s+#(\d+)");
        if (m.Success && int.TryParse(m.Groups[2].Value, out var n))
            return (m.Groups[1].Value, n);
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
        _writer.Dispose();
    }
}
