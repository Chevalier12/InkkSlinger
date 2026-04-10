using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
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

                var previousSynchronizationContext = SynchronizationContext.Current;
                using var synchronizationContext = new SingleThreadSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);

                try
                {
                    using var runner = new XunitTestAssemblyRunner(
                        TestAssembly,
                        testCases,
                        DiagnosticMessageSink,
                        executionMessageSink,
                        executionOptions);

                    var runTask = runner.RunAsync();
                    _ = runTask.ContinueWith(
                        static (task, state) => ((SingleThreadSynchronizationContext)state!).Complete(),
                        synchronizationContext,
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);

                    synchronizationContext.RunOnCurrentThread();
                    _ = runTask.GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
                }
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

internal sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State, ManualResetEventSlim? Completion)> _queue = new();
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add((d, state, null));
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Environment.CurrentManagedThreadId == _owningThreadId)
        {
            d(state);
            return;
        }

        using var completion = new ManualResetEventSlim();
        _queue.Add((d, state, completion));
        completion.Wait();
    }

    public void RunOnCurrentThread()
    {
        foreach (var workItem in _queue.GetConsumingEnumerable())
        {
            workItem.Callback(workItem.State);
            workItem.Completion?.Set();
        }
    }

    public void Complete()
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }
    }

    public void Dispose()
    {
        Complete();
        _queue.Dispose();
    }
}