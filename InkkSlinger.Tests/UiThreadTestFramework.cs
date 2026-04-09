using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InkkSlinger.Tests;

public sealed class UiThreadTestFramework : XunitTestFramework
{
    public UiThreadTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
    }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new UiThreadTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink);
    }
}

public sealed class UiThreadTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public UiThreadTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        ExceptionDispatchInfo? backgroundFailure = null;
        using var completed = new ManualResetEventSlim();

        var uiThread = new Thread(() =>
        {
            try
            {
                Dispatcher.ResetForTests();
                Dispatcher.InitializeForCurrentThread();

                using var runner = new XunitTestAssemblyRunner(
                    TestAssembly,
                    testCases,
                    DiagnosticMessageSink,
                    executionMessageSink,
                    executionOptions);

                _ = runner.RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                backgroundFailure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "InkkSlinger.Tests.UiThread"
        };

        uiThread.Start();
        completed.Wait();
        backgroundFailure?.Throw();
    }
}