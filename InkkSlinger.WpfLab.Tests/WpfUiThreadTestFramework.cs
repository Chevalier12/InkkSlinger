using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;
using WpfDispatcher = System.Windows.Threading.Dispatcher;

namespace InkkSlinger.WpfLab.Tests;

public sealed class WpfUiThreadTestFramework : XunitTestFramework
{
    public WpfUiThreadTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
    }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new WpfUiThreadTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink);
    }
}

public sealed class WpfUiThreadTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public WpfUiThreadTestFrameworkExecutor(
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
                var previousSynchronizationContext = SynchronizationContext.Current;
                using var synchronizationContext = new WpfSynchronizationContext();
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
                        static (_, state) => ((WpfSynchronizationContext)state!).Complete(),
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
            Name = "InkkSlinger.WpfLab.Tests.UiThread"
        };

        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        completed.Wait();
        backgroundFailure?.Throw();
    }
}

internal sealed class WpfSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State, ManualResetEventSlim? Completion)> _queue = new();
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;
    private readonly WpfDispatcher _dispatcher = WpfDispatcher.CurrentDispatcher;

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add((d, state, null));
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { }));
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
        _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { }));
        completion.Wait();
    }

    public void RunOnCurrentThread()
    {
        foreach (var workItem in _queue.GetConsumingEnumerable())
        {
            workItem.Callback(workItem.State);
            workItem.Completion?.Set();

            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            WpfDispatcher.PushFrame(frame);
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