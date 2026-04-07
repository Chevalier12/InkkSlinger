using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InkkOopsGameHost : IInkkOopsHost, IDisposable
{
    private const float PointerMoveStepDistance = 24f;

    private readonly InkkOopsHostConfiguration _hostConfiguration;
    private readonly IInkkOopsArtifactNamingPolicy _artifactNamingPolicy;
    private readonly InkkOopsVisualTreeDiagnostics _visualTreeDiagnostics;
    private readonly Window _window;
    private readonly Func<Viewport> _viewportAccessor;
    private readonly Func<RenderTarget2D?> _renderTargetAccessor;
    private readonly object _sync = new();
    private readonly List<PendingFrameGate> _frameGates = new();
    private readonly List<PendingResize> _pendingResizes = new();
    private readonly List<PendingCapture> _pendingCaptures = new();
    private readonly List<AutomationEventRecord> _automationEvents = new();
    private InkkOopsInteractionRecorder? _recorder;
    private Vector2 _pointerPosition;
    private bool _hasPointerPosition;
    private bool _disposed;
    private string _artifactRoot;

    public InkkOopsGameHost(
        UiRoot uiRoot,
        Window window,
        Func<Viewport> viewportAccessor,
        Func<RenderTarget2D?> renderTargetAccessor,
        string artifactRoot,
        InkkOopsHostConfiguration hostConfiguration)
    {
        UiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewportAccessor = viewportAccessor ?? throw new ArgumentNullException(nameof(viewportAccessor));
        _renderTargetAccessor = renderTargetAccessor ?? throw new ArgumentNullException(nameof(renderTargetAccessor));
        _hostConfiguration = hostConfiguration ?? throw new ArgumentNullException(nameof(hostConfiguration));
        _artifactNamingPolicy = _hostConfiguration.ArtifactNamingPolicy;
        _visualTreeDiagnostics = new InkkOopsVisualTreeDiagnostics(_hostConfiguration.DiagnosticsContributors);
        _artifactRoot = artifactRoot ?? string.Empty;
        UiRoot.Automation.AutomationEventRaised += OnAutomationEventRaised;
    }

    public UiRoot UiRoot { get; }

    public string ArtifactRoot => _artifactRoot;

    public UIElement? GetVisualRootElement()
    {
        return UiRoot.VisualRoot;
    }

    public LayoutRect GetViewportBounds()
    {
        var viewport = _viewportAccessor();
        return new LayoutRect(0f, 0f, viewport.Width, viewport.Height);
    }

    public string GetDisplayedFps()
    {
        return Game1.ExtractDisplayedFpsFromWindowTitle(_window.Title);
    }

    public void SetArtifactRoot(string artifactRoot)
    {
        _artifactRoot = artifactRoot ?? string.Empty;
    }

    public void StartRecording(string recordingRoot)
    {
        if (_recorder != null)
        {
            return;
        }

        _recorder = new InkkOopsInteractionRecorder(
            recordingRoot,
            _window.ClientSize,
            _artifactNamingPolicy);
    }

    public string StopRecording()
    {
        if (_recorder == null)
        {
            return string.Empty;
        }

        var directoryPath = _recorder.DirectoryPath;
        _recorder.Dispose();
        _recorder = null;
        return directoryPath;
    }

    public Task ResizeWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return ExecuteOnUiThreadAsync(
            () =>
            {
                _window.SetClientSize(width, height, applyChanges: true);
                lock (_sync)
                {
                    _pendingResizes.Add(PendingResize.ForExactSize(width, height, completion));
                }
            },
            completion,
            cancellationToken);
    }

    public Task MaximizeWindowAsync(CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var previousSize = _window.ClientSize;
                _window.Maximize();
                lock (_sync)
                {
                    _pendingResizes.Add(PendingResize.ForSizeChange(previousSize.X, previousSize.Y, completion));
                }
            },
            completion,
            cancellationToken);
    }

    public Task AdvanceFrameAsync(int frameCount, CancellationToken cancellationToken = default)
    {
        if (frameCount <= 0)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            _frameGates.Add(new PendingFrameGate(frameCount, completion));
        }

        cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), completion);
        return completion.Task;
    }

    public async Task WaitForIdleAsync(InkkOopsIdlePolicy policy, CancellationToken cancellationToken = default)
    {
        IdleSnapshot? previous = null;
        var stableFrames = 0;
        while (stableFrames < 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AdvanceFrameAsync(1, cancellationToken).ConfigureAwait(false);
            var snapshot = await QueryOnUiThreadAsync(() => CaptureIdleSnapshot(policy), cancellationToken).ConfigureAwait(false);
            var idle = snapshot.IsSatisfied && previous is not null && snapshot.IsEquivalentTo(previous.Value);
            stableFrames = idle ? stableFrames + 1 : 0;
            previous = snapshot;
        }
    }

    public Task MovePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        return MovePointerAsync(position, InkkOopsPointerMotion.Default, cancellationToken);
    }

    public Task MovePointerAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken = default)
    {
        return MovePointerSmoothAsync(position, motion, cancellationToken);
    }

        public Task PressPointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () => ApplyPointerPress(position, button),
            cancellationToken);
    }

        public Task ReleasePointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () => ApplyPointerRelease(position, button),
            cancellationToken);
    }

    public Task WheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var current = _hasPointerPosition ? _pointerPosition : Vector2.Zero;
                UiRoot.RunInputDeltaForTests(CreateDelta(current, current, wheelDelta: delta));
            },
            cancellationToken);
    }

    public Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return ExecuteOnUiThreadAsync(
            () =>
            {
                lock (_sync)
                {
                    _pendingCaptures.Add(new PendingCapture(artifactName, completion));
                }

                UiRoot.ForceFullRedrawForDiagnosticsCapture();
            },
            completion,
            cancellationToken);
    }

    public Task<string> CaptureTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        return QueryOnUiThreadAsync(
            () =>
            {
                var viewport = _viewportAccessor();
                var hovered = UiRoot.GetHoveredElementForDiagnostics();
                var focused = FocusManager.GetFocusedElement();
                var diagnosticsContext = new InkkOopsDiagnosticsContext
                {
                    UiRoot = UiRoot,
                    Viewport = new LayoutRect(0f, 0f, viewport.Width, viewport.Height),
                    HoveredElement = hovered,
                    FocusedElement = focused,
                    ArtifactName = artifactName,
                    Filter = _hostConfiguration.DiagnosticsFilterPolicy.CreateFilter(artifactName)
                };
                var visualTree = _visualTreeDiagnostics.Capture(UiRoot.VisualRoot, diagnosticsContext);
                var builder = new StringBuilder();
                builder.AppendLine($"timestamp_utc={DateTime.UtcNow:O}");
                builder.AppendLine($"hovered={DescribeElement(hovered)}");
                builder.AppendLine($"focused={DescribeElement(focused)}");
                builder.AppendLine($"dirty_regions={UiRoot.GetDirtyRegionSummaryForTests()}");
                builder.AppendLine($"synced_dirty_roots={UiRoot.GetLastSynchronizedDirtyRootSummaryForTests()}");
                builder.AppendLine($"retained_nodes={UiRoot.RetainedRenderNodeCount}");
                builder.AppendLine($"retained_tree_validation={UiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests()}");
                builder.AppendLine($"window_client={_window.ClientSize.X}x{_window.ClientSize.Y}");
                builder.AppendLine($"window_backbuffer={_window.BackBufferSize.X}x{_window.BackBufferSize.Y}");
                builder.AppendLine($"viewport={viewport.Width}x{viewport.Height}");
                builder.AppendLine("visual_tree_begin");
                builder.Append(_hostConfiguration.DiagnosticsSerializer.SerializeVisualTree(visualTree));
                builder.AppendLine("visual_tree_end");
                return builder.ToString();
            },
            cancellationToken);
    }

    public UIElement? FindElement(string identifier)
    {
        return QueryOnUiThread(() =>
        {
            if (UiRoot.VisualRoot is FrameworkElement frameworkRoot)
            {
                return frameworkRoot.FindName(identifier);
            }

            return null;
        });
    }

    public AutomationPeer? FindAutomationPeer(UIElement element)
    {
        return QueryOnUiThread(() => UiRoot.Automation.GetPeer(element));
    }

    public IReadOnlyList<AutomationPeer> GetAutomationPeersSnapshot()
    {
        return QueryOnUiThread(() => UiRoot.Automation.GetTreeSnapshot());
    }

    public IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot()
    {
        lock (_sync)
        {
            return _automationEvents.ToArray();
        }
    }

    public void ClearAutomationEvents()
    {
        lock (_sync)
        {
            _automationEvents.Clear();
        }
    }

    Task IInkkOopsHost.ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        return ExecuteOnUiThreadAsync(action, cancellationToken);
    }

    Task<T> IInkkOopsHost.QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken)
    {
        return QueryOnUiThreadAsync(query, cancellationToken);
    }

    public void OnAfterDraw()
    {
        CompleteCaptureRequests();
    }

    public void OnAfterUpdate()
    {
        CompleteFrameGates();
        CompleteResizeRequestsIfReady();
        _recorder?.RecordFrame(_window.ClientSize, Mouse.GetState());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopRecording();
        UiRoot.Automation.AutomationEventRaised -= OnAutomationEventRaised;
    }

    private void OnAutomationEventRaised(object? sender, AutomationEventArgs args)
    {
        lock (_sync)
        {
            _automationEvents.Add(new AutomationEventRecord(
                args.EventType,
                args.Peer.RuntimeId,
                args.PropertyName,
                args.OldValue,
                args.NewValue,
                args.OldPeer?.RuntimeId,
                args.NewPeer?.RuntimeId));
        }
    }

    private void CompleteFrameGates()
    {
        lock (_sync)
        {
            for (var i = _frameGates.Count - 1; i >= 0; i--)
            {
                var updated = _frameGates[i] with { RemainingFrames = _frameGates[i].RemainingFrames - 1 };
                if (updated.RemainingFrames <= 0)
                {
                    updated.Completion.TrySetResult();
                    _frameGates.RemoveAt(i);
                }
                else
                {
                    _frameGates[i] = updated;
                }
            }
        }
    }

    private void CompleteResizeRequestsIfReady()
    {
        var viewport = _viewportAccessor();
        lock (_sync)
        {
            for (var i = _pendingResizes.Count - 1; i >= 0; i--)
            {
                var request = _pendingResizes[i];
                                var clientWidth = _window.ClientSize.X;
                                var clientHeight = _window.ClientSize.Y;
                                var backBufferWidth = _window.BackBufferSize.X;
                                var backBufferHeight = _window.BackBufferSize.Y;
                                var ready = request.RequiresChangeFromPreviousSize
                                        ? clientWidth > 0 &&
                                            clientHeight > 0 &&
                                            (clientWidth != request.PreviousWidth || clientHeight != request.PreviousHeight) &&
                                            backBufferWidth == clientWidth &&
                                            backBufferHeight == clientHeight &&
                                            viewport.Width == clientWidth &&
                                            viewport.Height == clientHeight
                                        : clientWidth == request.Width &&
                                            clientHeight == request.Height &&
                                            backBufferWidth == request.Width &&
                                            backBufferHeight == request.Height &&
                                            viewport.Width == request.Width &&
                                            viewport.Height == request.Height;

                if (!ready)
                {
                    continue;
                }

                request.Completion.TrySetResult();
                _pendingResizes.RemoveAt(i);
            }
        }
    }

    private void CompleteCaptureRequests()
    {
        List<PendingCapture>? captures = null;
        lock (_sync)
        {
            if (_pendingCaptures.Count == 0)
            {
                return;
            }

            captures = new List<PendingCapture>(_pendingCaptures);
            _pendingCaptures.Clear();
        }

        Directory.CreateDirectory(ArtifactRoot);
        var renderTarget = _renderTargetAccessor();
        foreach (var capture in captures)
        {
            if (renderTarget == null || renderTarget.IsDisposed)
            {
                capture.Completion.TrySetException(new InvalidOperationException("UI composite render target is unavailable."));
                continue;
            }

            try
            {
                var path = Path.Combine(ArtifactRoot, _artifactNamingPolicy.GetFrameCaptureFileName(capture.ArtifactName));
                using var stream = File.Create(path);
                renderTarget.SaveAsPng(stream, renderTarget.Width, renderTarget.Height);
                capture.Completion.TrySetResult();
            }
            catch (Exception ex)
            {
                capture.Completion.TrySetException(ex);
            }
        }
    }

    private async Task MovePointerSmoothAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken)
    {
        var start = await QueryOnUiThreadAsync(GetCurrentPointerPositionForMotion, cancellationToken).ConfigureAwait(false);
        var steps = CalculatePointerMoveStepCount(start, position, motion);
        for (var i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t = ApplyPointerEasing(i / (float)steps, motion.Easing);
            var sample = Vector2.Lerp(start, position, t);
            await ExecuteOnUiThreadAsync(() => ApplyPointerMove(sample), cancellationToken).ConfigureAwait(false);
            if (i < steps)
            {
                await AdvanceFrameAsync(1, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Vector2 GetCurrentPointerPositionForMotion()
    {
        if (_hasPointerPosition)
        {
            return _pointerPosition;
        }

        var mouseState = Mouse.GetState();
        return new Vector2(mouseState.X, mouseState.Y);
    }

    private void ApplyPointerMove(Vector2 position)
    {
        var previous = _hasPointerPosition ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointerPosition = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true));
    }

    private void ApplyPointerPress(Vector2 position, MouseButton button)
    {
        var previous = _hasPointerPosition ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointerPosition = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, buttonPressed: button));
    }

    private void ApplyPointerRelease(Vector2 position, MouseButton button)
    {
        var previous = _hasPointerPosition ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointerPosition = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, buttonReleased: button));
    }

    private static int CalculatePointerMoveStepCount(Vector2 start, Vector2 end, InkkOopsPointerMotion motion)
    {
        if (motion.TravelFrames > 0)
        {
            return motion.TravelFrames + 1;
        }

        var distance = Vector2.Distance(start, end);
        var stepDistance = motion.StepDistance > 0f ? motion.StepDistance : PointerMoveStepDistance;
        return Math.Max(1, (int)MathF.Ceiling(distance / stepDistance));
    }

    private static float ApplyPointerEasing(float t, InkkOopsPointerEasing easing)
    {
        return easing switch
        {
            InkkOopsPointerEasing.EaseInOut => t * t * (3f - (2f * t)),
            _ => t
        };
    }

    private IdleSnapshot CaptureIdleSnapshot(InkkOopsIdlePolicy policy)
    {
        var hovered = DescribeElement(UiRoot.GetHoveredElementForDiagnostics());
        var focused = DescribeElement(FocusManager.GetFocusedElement());
        var queuedAutomationEvents = UiRoot.Automation.GetQueuedEventCountForDiagnostics();
        var baseSatisfied = UiRoot.PendingDeferredOperationCount == 0 &&
                            !UiRoot.HasPendingMeasureInvalidation &&
                            !UiRoot.HasPendingArrangeInvalidation &&
                            !UiRoot.HasPendingRenderInvalidation &&
                            !UiRoot.HasPendingForcedDrawForInkkOops;

        var inputSatisfied = baseSatisfied &&
                             FocusManager.GetCapturedPointerElement() == null &&
                             queuedAutomationEvents == 0;

        var diagnosticsSatisfied = inputSatisfied;

        var satisfied = policy switch
        {
            InkkOopsIdlePolicy.InputStable => inputSatisfied,
            InkkOopsIdlePolicy.DiagnosticsStable => diagnosticsSatisfied,
            _ => baseSatisfied
        };

        return new IdleSnapshot(
            satisfied,
            hovered,
            focused,
            UiRoot.GetDirtyRegionSummaryForTests(),
            queuedAutomationEvents,
            FocusManager.GetCapturedPointerElement() == null);
    }

    private Task ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return ExecuteOnUiThreadAsync(action, completion, cancellationToken);
    }

    private Task ExecuteOnUiThreadAsync(Action action, TaskCompletionSource completion, CancellationToken cancellationToken)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            completion.TrySetResult();
            return completion.Task;
        }

        cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), completion);
        UiRoot.EnqueueDeferredOperation(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    private T QueryOnUiThread<T>(Func<T> query)
    {
        if (Dispatcher.CheckAccess())
        {
            return query();
        }

        return QueryOnUiThreadAsync(query, CancellationToken.None).GetAwaiter().GetResult();
    }

    private Task<T> QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken)
    {
        if (Dispatcher.CheckAccess())
        {
            return Task.FromResult(query());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(static state => ((TaskCompletionSource<T>)state!).TrySetCanceled(), completion);
        UiRoot.EnqueueDeferredOperation(() =>
        {
            try
            {
                completion.TrySetResult(query());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    private static InputDelta CreateDelta(
        Vector2 previous,
        Vector2 current,
        bool pointerMoved = false,
        MouseButton? buttonPressed = null,
        MouseButton? buttonReleased = null,
        int wheelDelta = 0)
    {
        var leftPressed = buttonPressed == MouseButton.Left;
        var leftReleased = buttonReleased == MouseButton.Left;
        var rightPressed = buttonPressed == MouseButton.Right;
        var rightReleased = buttonReleased == MouseButton.Right;
        var middlePressed = buttonPressed == MouseButton.Middle;
        var middleReleased = buttonReleased == MouseButton.Middle;
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previous),
            Current = new InputSnapshot(default, default, current),
            PressedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            TextInput = Array.Empty<char>(),
            PointerMoved = pointerMoved || buttonPressed != null || buttonReleased != null,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = middlePressed,
            MiddleReleased = middleReleased
        };
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        return element is FrameworkElement { Name.Length: > 0 } frameworkElement
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    private readonly record struct PendingFrameGate(int RemainingFrames, TaskCompletionSource Completion);

    private readonly record struct PendingResize(
        int Width,
        int Height,
        int PreviousWidth,
        int PreviousHeight,
        bool RequiresChangeFromPreviousSize,
        TaskCompletionSource Completion)
    {
        public static PendingResize ForExactSize(int width, int height, TaskCompletionSource completion)
        {
            return new PendingResize(width, height, 0, 0, false, completion);
        }

        public static PendingResize ForSizeChange(int previousWidth, int previousHeight, TaskCompletionSource completion)
        {
            return new PendingResize(0, 0, previousWidth, previousHeight, true, completion);
        }
    }

    private readonly record struct PendingCapture(string ArtifactName, TaskCompletionSource Completion);

    private readonly record struct IdleSnapshot(
        bool IsSatisfied,
        string HoveredElement,
        string FocusedElement,
        string DirtyRegionSummary,
        int QueuedAutomationEventCount,
        bool PointerCaptureReleased)
    {
        public bool IsEquivalentTo(IdleSnapshot other)
        {
            return HoveredElement == other.HoveredElement &&
                   FocusedElement == other.FocusedElement &&
                   DirtyRegionSummary == other.DirtyRegionSummary &&
                   QueuedAutomationEventCount == other.QueuedAutomationEventCount &&
                   PointerCaptureReleased == other.PointerCaptureReleased;
        }
    }
}
