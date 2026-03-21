using Xunit;

namespace InkkSlinger.Tests;

public sealed class InstrumentationTests
{
    [Fact]
    public void Trace_WritesLineToConsole()
    {
        Instrumentation.Trace("Test message from InstrumentationTests");
    }

    [Fact]
    public void TraceTiming_WritesTimingLine()
    {
        Instrumentation.TraceTiming("FakeMethod", 12345);
    }

    [Fact]
    public void TraceCounter_WritesCounterLine()
    {
        Instrumentation.TraceCounter("FakeMethod", 42);
    }

    [Fact]
    public void Trace_MultipleLines_AreOrdered()
    {
        Instrumentation.Trace("Line 1");
        Instrumentation.Trace("Line 2");
        Instrumentation.Trace("Line 3");
    }
}
