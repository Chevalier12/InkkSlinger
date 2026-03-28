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
        string artifactRoot)
    {
        UiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewportAccessor = viewportAccessor ?? throw new ArgumentNullException(nameof(viewportAccessor));
        _renderTargetAccessor = renderTargetAccessor ?? throw new ArgumentNullException(nameof(renderTargetAccessor));
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
            _window.ClientSize);
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
                    _pendingResizes.Add(new PendingResize(width, height, completion));
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
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var previous = _hasPointerPosition ? _pointerPosition : position;
                _pointerPosition = position;
                _hasPointerPosition = true;
                UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true));
            },
            cancellationToken);
    }

    public Task PressPointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var previous = _hasPointerPosition ? _pointerPosition : position;
                _pointerPosition = position;
                _hasPointerPosition = true;
                UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, leftPressed: true));
            },
            cancellationToken);
    }

    public Task ReleasePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var previous = _hasPointerPosition ? _pointerPosition : position;
                _pointerPosition = position;
                _hasPointerPosition = true;
                UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, leftReleased: true));
            },
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

    public Task WriteTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                Directory.CreateDirectory(ArtifactRoot);
                var path = Path.Combine(ArtifactRoot, NormalizeArtifactName(artifactName, ".txt"));
                var viewport = _viewportAccessor();
                var hovered = UiRoot.GetHoveredElementForDiagnostics();
                var builder = new StringBuilder();
                builder.AppendLine($"timestamp_utc={DateTime.UtcNow:O}");
                builder.AppendLine($"hovered={DescribeElement(hovered)}");
                builder.AppendLine($"focused={DescribeElement(FocusManager.GetFocusedElement())}");
                builder.AppendLine($"dirty_regions={UiRoot.GetDirtyRegionSummaryForTests()}");
                builder.AppendLine($"synced_dirty_roots={UiRoot.GetLastSynchronizedDirtyRootSummaryForTests()}");
                builder.AppendLine($"retained_nodes={UiRoot.RetainedRenderNodeCount}");
                builder.AppendLine($"retained_tree_validation={UiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests()}");
                builder.AppendLine($"window_client={_window.ClientSize.X}x{_window.ClientSize.Y}");
                builder.AppendLine($"window_backbuffer={_window.BackBufferSize.X}x{_window.BackBufferSize.Y}");
                builder.AppendLine($"viewport={viewport.Width}x{viewport.Height}");
                builder.AppendLine("visual_tree_begin");
                AppendVisualTree(builder, UiRoot.VisualRoot, depth: 0);
                builder.AppendLine("visual_tree_end");
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
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
                var ready =
                    _window.ClientSize.X == request.Width &&
                    _window.ClientSize.Y == request.Height &&
                    _window.BackBufferSize.X == request.Width &&
                    _window.BackBufferSize.Y == request.Height &&
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
                var path = Path.Combine(ArtifactRoot, NormalizeArtifactName(capture.ArtifactName, ".png"));
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
        bool leftPressed = false,
        bool leftReleased = false,
        int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previous),
            Current = new InputSnapshot(default, default, current),
            PressedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            TextInput = Array.Empty<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static string NormalizeArtifactName(string artifactName, string extension)
    {
        if (string.IsNullOrWhiteSpace(artifactName))
        {
            artifactName = "artifact";
        }

        artifactName = artifactName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? artifactName
            : artifactName + extension;

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            artifactName = artifactName.Replace(invalid, '-');
        }

        return artifactName;
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

    private static void AppendVisualTree(StringBuilder builder, UIElement element, int depth)
    {
        var indent = new string(' ', depth * 2);
        var text = element switch
        {
            Button button => Label.ExtractAutomationText(button.Content),
            Label label => Label.ExtractAutomationText(label.Content),
            TextBlock textBlock => textBlock.Text,
            _ => string.Empty
        };

        if (element is FrameworkElement frameworkElement)
        {
            var slot = frameworkElement.LayoutSlot;
            builder.Append(indent);
            builder.Append(DescribeElement(element));
            builder.Append(" slot=");
            builder.Append($"{slot.X:0.##},{slot.Y:0.##},{slot.Width:0.##},{slot.Height:0.##}");
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append(" text=");
                builder.Append(text.Replace("\r", "\\r").Replace("\n", "\\n"));
            }

            if (element is TextBlock textBlock)
            {
                var typography = UiTextRenderer.ResolveTypography(textBlock, textBlock.FontSize);
                var lineHeight = UiTextRenderer.GetLineHeight(typography);
                var inkBounds = string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests)
                    ? new LayoutRect(0f, 0f, 0f, 0f)
                    : UiTextRenderer.GetInkBoundsForTests(typography, textBlock.LastRenderedLayoutTextForTests);
                builder.Append(" renderLines=");
                builder.Append(textBlock.LastRenderedLineCountForTests);
                builder.Append(" renderWidth=");
                builder.Append($"{textBlock.LastRenderedLayoutWidthForTests:0.##}");
                builder.Append(" desired=");
                builder.Append($"{textBlock.DesiredSize.X:0.##},{textBlock.DesiredSize.Y:0.##}");
                builder.Append(" previousAvailable=");
                builder.Append($"{textBlock.PreviousAvailableSizeForTests.X:0.##},{textBlock.PreviousAvailableSizeForTests.Y:0.##}");
                builder.Append(" measureCalls=");
                builder.Append(textBlock.MeasureCallCount);
                builder.Append(" measureWork=");
                builder.Append(textBlock.MeasureWorkCount);
                builder.Append(" arrangeCalls=");
                builder.Append(textBlock.ArrangeCallCount);
                builder.Append(" arrangeWork=");
                builder.Append(textBlock.ArrangeWorkCount);
                builder.Append(" measureValid=");
                builder.Append(textBlock.IsMeasureValidForTests);
                builder.Append(" arrangeValid=");
                builder.Append(textBlock.IsArrangeValidForTests);
                builder.Append(" lineHeight=");
                builder.Append($"{lineHeight:0.###}");
                builder.Append(" inkBounds=");
                builder.Append($"{inkBounds.X:0.##},{inkBounds.Y:0.##},{inkBounds.Width:0.##},{inkBounds.Height:0.##}");
                if (!string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests))
                {
                    builder.Append(" renderText=");
                    builder.Append(textBlock.LastRenderedLayoutTextForTests.Replace("\r", "\\r").Replace("\n", "\\n"));
                }
            }
            else if (element is Border border)
            {
                builder.Append(" desired=");
                builder.Append($"{border.DesiredSize.X:0.##},{border.DesiredSize.Y:0.##}");
                builder.Append(" previousAvailable=");
                builder.Append($"{border.PreviousAvailableSizeForTests.X:0.##},{border.PreviousAvailableSizeForTests.Y:0.##}");
                builder.Append(" measureCalls=");
                builder.Append(border.MeasureCallCount);
                builder.Append(" measureWork=");
                builder.Append(border.MeasureWorkCount);
                builder.Append(" arrangeCalls=");
                builder.Append(border.ArrangeCallCount);
                builder.Append(" arrangeWork=");
                builder.Append(border.ArrangeWorkCount);
                builder.Append(" measureValid=");
                builder.Append(border.IsMeasureValidForTests);
                builder.Append(" arrangeValid=");
                builder.Append(border.IsArrangeValidForTests);
            }
            else if (element is Grid grid)
            {
                builder.Append(" desired=");
                builder.Append($"{grid.DesiredSize.X:0.##},{grid.DesiredSize.Y:0.##}");
                builder.Append(" previousAvailable=");
                builder.Append($"{grid.PreviousAvailableSizeForTests.X:0.##},{grid.PreviousAvailableSizeForTests.Y:0.##}");
                builder.Append(" measureCalls=");
                builder.Append(grid.MeasureCallCount);
                builder.Append(" measureWork=");
                builder.Append(grid.MeasureWorkCount);
                builder.Append(" arrangeCalls=");
                builder.Append(grid.ArrangeCallCount);
                builder.Append(" arrangeWork=");
                builder.Append(grid.ArrangeWorkCount);
                builder.Append(" measureValid=");
                builder.Append(grid.IsMeasureValidForTests);
                builder.Append(" arrangeValid=");
                builder.Append(grid.IsArrangeValidForTests);
            }

            builder.AppendLine();
        }
        else
        {
            builder.Append(indent);
            builder.AppendLine(DescribeElement(element));
        }

        foreach (var child in element.GetVisualChildren())
        {
            AppendVisualTree(builder, child, depth + 1);
        }
    }

    private readonly record struct PendingFrameGate(int RemainingFrames, TaskCompletionSource Completion);

    private readonly record struct PendingResize(int Width, int Height, TaskCompletionSource Completion);

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
