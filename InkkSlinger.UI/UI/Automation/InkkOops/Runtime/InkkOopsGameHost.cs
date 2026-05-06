using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InkkOopsGameHost : IInkkOopsHost, IDisposable
{
    private const float PointerMoveStepDistance = 24f;
    private const int MaxFrameTimingSamples = 512;

    private readonly InkkOopsHostConfiguration _hostConfiguration;
    private readonly IInkkOopsArtifactNamingPolicy _artifactNamingPolicy;
    private readonly Window _window;
    private readonly Func<Viewport> _viewportAccessor;
    private readonly Func<RenderTarget2D?> _renderTargetAccessor;
    private readonly Func<string> _displayedFpsAccessor;
    private readonly object _sync = new();
    private readonly List<PendingFrameGate> _frameGates = new();
    private readonly List<PendingResize> _pendingResizes = new();
    private readonly List<PendingCapture> _pendingCaptures = new();
    private readonly List<FrameTimingSample> _frameTimingSamples = new();
    private readonly List<AutomationEventRecord> _automationEvents = new();
    private readonly HashSet<Keys> _automationHeldKeys = new();
    private InkkOopsInteractionRecorder? _recorder;
    private Vector2 _pointerPosition;
    private bool _hasPointerPosition;
    private float _lastPointerMotionLowestFps = float.PositiveInfinity;
    private string _lastPointerMoveInputTelemetrySummary = "input=none";
    private string _lastPointerMotionTelemetrySummary = "motion=none";
    private int _frameTimingSerial;
    private bool _disposed;
    private string _artifactRoot;

    public InkkOopsGameHost(
        UiRoot uiRoot,
        Window window,
        Func<Viewport> viewportAccessor,
        Func<RenderTarget2D?> renderTargetAccessor,
        Func<string> displayedFpsAccessor,
        string artifactRoot,
        InkkOopsHostConfiguration hostConfiguration)
    {
        UiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewportAccessor = viewportAccessor ?? throw new ArgumentNullException(nameof(viewportAccessor));
        _renderTargetAccessor = renderTargetAccessor ?? throw new ArgumentNullException(nameof(renderTargetAccessor));
        _displayedFpsAccessor = displayedFpsAccessor ?? throw new ArgumentNullException(nameof(displayedFpsAccessor));
        _hostConfiguration = hostConfiguration ?? throw new ArgumentNullException(nameof(hostConfiguration));
        _artifactNamingPolicy = _hostConfiguration.ArtifactNamingPolicy;
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
        return _displayedFpsAccessor();
    }

    public string GetLastPointerMotionTelemetrySummary()
    {
        return _lastPointerMotionTelemetrySummary;
    }

    public int GetFrameTimingCursor()
    {
        lock (_sync)
        {
            return _frameTimingSerial;
        }
    }

    public Task<string> CaptureFrameTimingWindowAsync(string artifactName, int startFrameSerial, int maxFrameCount, CancellationToken cancellationToken = default)
    {
        return QueryOnUiThreadAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var samples = GetFrameTimingSamplesSince(startFrameSerial, maxFrameCount);
                var report = FormatFrameTimingWindow(artifactName, startFrameSerial, samples);
                if (!string.IsNullOrWhiteSpace(ArtifactRoot))
                {
                    Directory.CreateDirectory(ArtifactRoot);
                    var fileName = artifactName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                        ? artifactName
                        : artifactName + "-frame-window.md";
                    File.WriteAllText(Path.Combine(ArtifactRoot, fileName), report, Encoding.UTF8);
                }

                return report;
            },
            cancellationToken);
    }

    public void SetArtifactRoot(string artifactRoot)
    {
        _artifactRoot = artifactRoot ?? string.Empty;
    }

    public void StartRecording(string recordingRoot, string launchProjectPath)
    {
        if (_recorder != null)
        {
            return;
        }

        _recorder = new InkkOopsInteractionRecorder(
            recordingRoot,
            _window.ClientSize,
            launchProjectPath,
            _artifactNamingPolicy);
        _window.NativeWindow.TextInput += OnRecordingTextInput;
    }

    public string StopRecording()
    {
        if (_recorder == null)
        {
            return string.Empty;
        }

        var directoryPath = _recorder.DirectoryPath;
        _window.NativeWindow.TextInput -= OnRecordingTextInput;
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

    public Task KeyDownAsync(Keys key, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var pointer = _hasPointerPosition ? _pointerPosition : Vector2.Zero;
                var previousKeyboard = CreateKeyboardState(_automationHeldKeys);
                _automationHeldKeys.Add(key);
                var currentKeyboard = CreateKeyboardState(_automationHeldKeys);
                UiRoot.RunInputDeltaForTests(CreateDelta(
                    pointer,
                    pointer,
                    previousKeyboard: previousKeyboard,
                    currentKeyboard: currentKeyboard,
                    pressedKeys: [key]));
            },
            cancellationToken);
    }

    public Task KeyUpAsync(Keys key, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var pointer = _hasPointerPosition ? _pointerPosition : Vector2.Zero;
                var previousKeyboard = CreateKeyboardState(_automationHeldKeys);
                _automationHeldKeys.Remove(key);
                var currentKeyboard = CreateKeyboardState(_automationHeldKeys);
                UiRoot.RunInputDeltaForTests(CreateDelta(
                    pointer,
                    pointer,
                    previousKeyboard: previousKeyboard,
                    currentKeyboard: currentKeyboard,
                    releasedKeys: [key]));
            },
            cancellationToken);
    }

    public Task TextInputAsync(char character, CancellationToken cancellationToken = default)
    {
        return ExecuteOnUiThreadAsync(
            () =>
            {
                var pointer = _hasPointerPosition ? _pointerPosition : Vector2.Zero;
                var keyboard = CreateKeyboardState(_automationHeldKeys);
                UiRoot.RunInputDeltaForTests(CreateDelta(
                    pointer,
                    pointer,
                    previousKeyboard: keyboard,
                    currentKeyboard: keyboard,
                    textInput: [character]));
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

    public Task<InkkOopsFrameRegionSample> SampleCurrentFrameRegionAsync(LayoutRect region, CancellationToken cancellationToken = default)
    {
        return QueryOnUiThreadAsync(() => SampleCurrentFrameRegion(region), cancellationToken);
    }

    private InkkOopsFrameRegionSample SampleCurrentFrameRegion(LayoutRect region)
    {
        var renderTarget = _renderTargetAccessor();
        if (renderTarget == null || renderTarget.IsDisposed)
        {
            throw new InvalidOperationException("UI composite render target is unavailable.");
        }

        var x = Math.Clamp((int)MathF.Floor(region.X), 0, renderTarget.Width);
        var y = Math.Clamp((int)MathF.Floor(region.Y), 0, renderTarget.Height);
        var right = Math.Clamp((int)MathF.Ceiling(region.X + region.Width), x, renderTarget.Width);
        var bottom = Math.Clamp((int)MathF.Ceiling(region.Y + region.Height), y, renderTarget.Height);
        var width = Math.Max(0, right - x);
        var height = Math.Max(0, bottom - y);
        if (width == 0 || height == 0)
        {
            return new InkkOopsFrameRegionSample(x, y, width, height, 0, 0, 0, 0f);
        }

        var pixels = new Microsoft.Xna.Framework.Color[width * height];
        renderTarget.GetData(
            level: 0,
            rect: new Microsoft.Xna.Framework.Rectangle(x, y, width, height),
            data: pixels,
            startIndex: 0,
            elementCount: pixels.Length);

        var brightPixels = 0;
        var maxLuma = 0;
        var lumaSum = 0L;
        foreach (var pixel in pixels)
        {
            var luma = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
            lumaSum += luma;
            if (luma > maxLuma)
            {
                maxLuma = luma;
            }

            if (pixel.A > 0 && luma >= 150)
            {
                brightPixels++;
            }
        }

        return new InkkOopsFrameRegionSample(
            x,
            y,
            width,
            height,
            pixels.Length,
            brightPixels,
            maxLuma,
            pixels.Length == 0 ? 0f : lumaSum / (float)pixels.Length);
    }

    public Task<string> CaptureTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        return QueryOnUiThreadAsync(
            () =>
            {
                var viewport = _viewportAccessor();
                var hovered = UiRoot.GetHoveredElementForDiagnostics();
                var focused = FocusManager.GetFocusedElement();
                var rootMetrics = UiRoot.GetMetricsSnapshot();
                var uiRootTelemetry = UiRoot.GetUiRootTelemetrySnapshot();
                var inputTelemetry = UiRoot.GetInputMetricsSnapshot();
                var pointerTelemetry = UiRoot.GetPointerMoveTelemetrySnapshotForTests();
                var performanceTelemetry = UiRoot.GetPerformanceTelemetrySnapshotForTests();
                var renderTelemetry = UiRoot.GetRenderTelemetrySnapshotForTests();
                var retainedControllerTelemetry = UiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
                var invalidationTelemetry = UiRoot.GetRenderInvalidationDebugSnapshotForTests();
                var frameworkTelemetry = FrameworkElement.GetAggregateTelemetrySnapshotForDiagnostics();
                var controlTelemetry = Control.GetAggregateTelemetrySnapshotForDiagnostics();
                var buttonTelemetry = Button.GetAggregateTelemetrySnapshotForDiagnostics();
                var scrollViewerTelemetry = ScrollViewer.GetAggregateTelemetrySnapshotForDiagnostics();
                var treeViewTelemetry = TreeView.GetAggregateTelemetrySnapshotForDiagnostics();
                var richTextBoxTelemetry = RichTextBox.GetAggregateTelemetrySnapshotForDiagnostics();
                var editorTelemetry = IDE_Editor.GetAggregateTelemetrySnapshotForDiagnostics();
                var builder = new StringBuilder();
                builder.AppendLine($"artifact_name={artifactName}");
                builder.AppendLine($"timestamp_utc={DateTime.UtcNow:O}");
                builder.AppendLine($"hovered={DescribeElement(hovered)}");
                builder.AppendLine($"focused={DescribeElement(focused)}");
                builder.AppendLine($"dirty_regions={UiRoot.GetDirtyRegionSummaryForTests()}");
                builder.AppendLine($"synced_dirty_roots={UiRoot.GetLastSynchronizedDirtyRootSummaryForTests()}");
                builder.AppendLine($"retained_nodes={UiRoot.RetainedRenderNodeCount}");
                builder.AppendLine($"retained_tree_validation={UiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests()}");
                builder.AppendLine($"uiRootLayoutExecutedFrameCount={UiRoot.LayoutExecutedFrameCount}");
                builder.AppendLine($"appDisplayedFps={InkkSlingerGameHost.ExtractDisplayedFpsFromWindowTitle(_window.Title)}");
                builder.AppendLine($"uiRootLastUpdateMs={UiRoot.LastUpdateMs:0.###}");
                builder.AppendLine($"uiRootLastDrawMs={UiRoot.LastDrawMs:0.###}");
                builder.AppendLine($"uiRootUpdateCallCount={uiRootTelemetry.UpdateCallCount}");
                builder.AppendLine($"uiRootUpdateElapsedMs={uiRootTelemetry.UpdateElapsedMs:0.###}");
                builder.AppendLine($"uiRootForceFullRedrawForDiagnosticsCaptureCallCount={uiRootTelemetry.ForceFullRedrawForDiagnosticsCaptureCallCount}");
                builder.AppendLine($"lastDirtyRectCount={rootMetrics.LastDirtyRectCount}");
                builder.AppendLine($"lastDirtyAreaPercentage={rootMetrics.LastDirtyAreaPercentage:0.###}");
                builder.AppendLine($"lastInputPhaseMs={inputTelemetry.LastInputPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastBindingPhaseMs={performanceTelemetry.BindingPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastInputPointerDispatchMs={inputTelemetry.LastInputPointerDispatchMilliseconds:0.###}");
                builder.AppendLine($"lastInputPointerTargetResolveMs={inputTelemetry.LastInputPointerTargetResolveMilliseconds:0.###}");
                builder.AppendLine($"lastInputHoverUpdateMs={inputTelemetry.LastInputHoverUpdateMilliseconds:0.###}");
                builder.AppendLine($"lastInputPointerRouteMs={inputTelemetry.LastInputPointerRouteMilliseconds:0.###}");
                builder.AppendLine($"lastInputHitTestCount={inputTelemetry.HitTestCount}");
                builder.AppendLine($"lastInputRoutedEventCount={inputTelemetry.RoutedEventCount}");
                builder.AppendLine($"lastInputPointerEventCount={inputTelemetry.PointerEventCount}");
                builder.AppendLine($"lastPointerMotion={_lastPointerMotionTelemetrySummary}");
                builder.AppendLine($"lastPointerResolvePath={pointerTelemetry.PointerResolvePath}");
                builder.AppendLine($"lastPointerResolveHoverReuseCheckMs={pointerTelemetry.PointerResolveHoverReuseCheckMilliseconds:0.###}");
                builder.AppendLine($"lastPointerResolveFinalHitTestMs={pointerTelemetry.PointerResolveFinalHitTestMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceInputMs={performanceTelemetry.InputPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceLayoutMs={performanceTelemetry.LayoutPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceAnimationMs={performanceTelemetry.AnimationPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceLayoutMeasureWorkMs={performanceTelemetry.LayoutMeasureWorkMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceLayoutMeasureExclusiveWorkMs={performanceTelemetry.LayoutMeasureExclusiveWorkMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceLayoutArrangeWorkMs={performanceTelemetry.LayoutArrangeWorkMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceHottestMeasure={performanceTelemetry.HottestLayoutMeasureElementType}#{performanceTelemetry.HottestLayoutMeasureElementName}:{performanceTelemetry.HottestLayoutMeasureElementMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceHottestMeasurePath={performanceTelemetry.HottestLayoutMeasureElementPath}");
                builder.AppendLine($"lastPerformanceHottestArrange={performanceTelemetry.HottestLayoutArrangeElementType}#{performanceTelemetry.HottestLayoutArrangeElementName}:{performanceTelemetry.HottestLayoutArrangeElementMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceHottestArrangePath={performanceTelemetry.HottestLayoutArrangeElementPath}");
                builder.AppendLine($"lastPerformanceRenderSchedulingMs={performanceTelemetry.RenderSchedulingPhaseMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceVisualUpdateMs={performanceTelemetry.VisualUpdateMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceRetainedSubtreeUpdateMs={performanceTelemetry.RetainedSubtreeUpdateMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceRetainedShallowSyncMs={performanceTelemetry.RetainedShallowSyncMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceRetainedDeepSyncMs={performanceTelemetry.RetainedDeepSyncMilliseconds:0.###}");
                builder.AppendLine($"lastPerformanceRetainedForceDeepSyncCount={performanceTelemetry.RetainedForceDeepSyncCount}");
                builder.AppendLine($"lastPerformanceRetainedShallowSuccessCount={performanceTelemetry.RetainedShallowSuccessCount}");
                builder.AppendLine($"lastRenderRetainedNodesVisited={renderTelemetry.RetainedNodesVisited}");
                builder.AppendLine($"lastRenderRetainedNodesDrawn={renderTelemetry.RetainedNodesDrawn}");
                builder.AppendLine($"lastRenderRetainedTraversalCount={renderTelemetry.RetainedTraversalCount}");
                builder.AppendLine($"lastRenderDirtyRegionTraversalCount={renderTelemetry.DirtyRegionTraversalCount}");
                builder.AppendLine($"lastRenderDirtyRootCount={renderTelemetry.DirtyRootCount}");
                builder.AppendLine($"lastRenderDirtyDecisionReason={renderTelemetry.LastDirtyDrawDecisionReason}");
                builder.AppendLine($"retainedControllerDirtyRegionCount={retainedControllerTelemetry.DirtyRegionCount}");
                builder.AppendLine($"retainedControllerDirtyCoverage={retainedControllerTelemetry.DirtyCoverage:0.###}");
                builder.AppendLine($"retainedControllerIsFullFrameDirty={retainedControllerTelemetry.IsFullFrameDirty}");
                builder.AppendLine($"retainedControllerFullRedrawFallbackCount={retainedControllerTelemetry.FullRedrawFallbackCount}");
                builder.AppendLine($"retainedControllerDirtyRegionAddCount={retainedControllerTelemetry.DirtyRegionAddCount}");
                builder.AppendLine($"retainedControllerDirtyRegionFragmentationFullDirtyCount={retainedControllerTelemetry.DirtyRegionFragmentationFullDirtyCount}");
                builder.AppendLine($"retainedControllerDirtyRegionBoundsDeltaSuppressedByTransformScrollCount={retainedControllerTelemetry.DirtyRegionBoundsDeltaSuppressedByTransformScrollCount}");
                builder.AppendLine($"retainedControllerLastDirtyRegionAddReason={retainedControllerTelemetry.LastDirtyRegionAddReason}");
                builder.AppendLine($"retainedControllerLastFullDirtySource={retainedControllerTelemetry.LastFullDirtySource}");
                builder.AppendLine($"fullDirtyInitialStateCount={renderTelemetry.FullDirtyInitialStateCount}");
                builder.AppendLine($"fullDirtyViewportChangeCount={renderTelemetry.FullDirtyViewportChangeCount}");
                builder.AppendLine($"fullDirtySurfaceResetCount={renderTelemetry.FullDirtySurfaceResetCount}");
                builder.AppendLine($"fullDirtyVisualStructureChangeCount={renderTelemetry.FullDirtyVisualStructureChangeCount}");
                builder.AppendLine($"fullDirtyRetainedRebuildCount={renderTelemetry.FullDirtyRetainedRebuildCount}");
                builder.AppendLine($"fullDirtyDetachedVisualCount={renderTelemetry.FullDirtyDetachedVisualCount}");
                builder.AppendLine($"lastDirtyBoundsVisual={invalidationTelemetry.DirtyBoundsVisualType}#{invalidationTelemetry.DirtyBoundsVisualName}");
                builder.AppendLine($"lastDirtyBoundsSourceResolution={invalidationTelemetry.DirtyBoundsSourceResolution}");
                builder.AppendLine($"lastDirtyBoundsUsedHint={invalidationTelemetry.DirtyBoundsUsedHint}");
                builder.AppendLine($"lastDirtyBounds={FormatLayoutRectForTelemetry(invalidationTelemetry.DirtyBounds)}");
                builder.AppendLine($"dirtyBoundsTrace={FormatDirtyBoundsTrace(UiRoot.GetDirtyBoundsEventTraceForTests(), 24)}");
                builder.AppendLine($"lastDrawVisualTreeMs={renderTelemetry.DrawVisualTreeMilliseconds:0.###}");
                builder.AppendLine($"lastDrawClearMs={renderTelemetry.DrawClearMilliseconds:0.###}");
                builder.AppendLine($"lastDrawCursorMs={renderTelemetry.DrawCursorMilliseconds:0.###}");
                builder.AppendLine($"lastDrawFinalBatchEndMs={renderTelemetry.DrawFinalBatchEndMilliseconds:0.###}");
                builder.AppendLine($"frameworkMeasureCallCount={frameworkTelemetry.MeasureCallCount}");
                builder.AppendLine($"frameworkMeasureMilliseconds={frameworkTelemetry.MeasureMilliseconds:0.###}");
                builder.AppendLine($"frameworkArrangeCallCount={frameworkTelemetry.ArrangeCallCount}");
                builder.AppendLine($"frameworkArrangeMilliseconds={frameworkTelemetry.ArrangeMilliseconds:0.###}");
                builder.AppendLine($"frameworkInvalidateMeasureCallCount={frameworkTelemetry.InvalidateMeasureCallCount}");
                builder.AppendLine($"controlDependencyPropertyChangedCallCount={controlTelemetry.DependencyPropertyChangedCallCount}");
                builder.AppendLine($"buttonRenderCallCount={buttonTelemetry.RenderCallCount}");
                builder.AppendLine($"buttonRenderMilliseconds={buttonTelemetry.RenderMilliseconds:0.###}");
                builder.AppendLine($"buttonTextLayoutCacheHitCount={buttonTelemetry.TextLayoutCacheHitCount}");
                builder.AppendLine($"scrollViewerSetOffsetCalls={scrollViewerTelemetry.SetOffsetCalls}");
                builder.AppendLine($"scrollViewerSetOffsetsMs={scrollViewerTelemetry.SetOffsetsMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerSetOffsetsDeferredLayoutPathCount={scrollViewerTelemetry.SetOffsetsDeferredLayoutPathCount}");
                builder.AppendLine($"scrollViewerSetOffsetsTransformInvalidationPathCount={scrollViewerTelemetry.SetOffsetsTransformInvalidationPathCount}");
                builder.AppendLine($"scrollViewerSetOffsetsPopupCloseCallCount={scrollViewerTelemetry.PopupCloseCallCount}");
                builder.AppendLine($"scrollViewerArrangeOverrideMs={scrollViewerTelemetry.ArrangeOverrideMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerResolveBarsForArrangeCallCount={scrollViewerTelemetry.ResolveBarsForArrangeCallCount}");
                builder.AppendLine($"scrollViewerResolveBarsForArrangeMs={scrollViewerTelemetry.ResolveBarsForArrangeMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerResolveBarsForArrangeIterationCount={scrollViewerTelemetry.ResolveBarsForArrangeIterationCount}");
                builder.AppendLine($"scrollViewerArrangeContentForCurrentOffsetsCallCount={scrollViewerTelemetry.ArrangeContentForCurrentOffsetsCallCount}");
                builder.AppendLine($"scrollViewerArrangeContentForCurrentOffsetsMs={scrollViewerTelemetry.ArrangeContentForCurrentOffsetsMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerArrangeContentTransformPathCount={scrollViewerTelemetry.ArrangeContentTransformPathCount}");
                builder.AppendLine($"scrollViewerArrangeContentOffsetPathCount={scrollViewerTelemetry.ArrangeContentOffsetPathCount}");
                builder.AppendLine($"scrollViewerUpdateScrollBarsCallCount={scrollViewerTelemetry.UpdateScrollBarsCallCount}");
                builder.AppendLine($"scrollViewerUpdateScrollBarsMs={scrollViewerTelemetry.UpdateScrollBarsMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerUpdateScrollBarValuesMs={scrollViewerTelemetry.UpdateScrollBarValuesMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerUpdateVerticalScrollBarValueMs={scrollViewerTelemetry.UpdateVerticalScrollBarValueMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerVerticalValueChangedMs={scrollViewerTelemetry.VerticalValueChangedMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerVerticalValueChangedSetOffsetsMs={scrollViewerTelemetry.VerticalValueChangedSetOffsetsMilliseconds:0.###}");
                builder.AppendLine($"scrollViewerArrangeOverrideCallCount={scrollViewerTelemetry.ArrangeOverrideCallCount}");
                builder.AppendLine($"treeViewHierarchicalRefreshRowsCallCount={treeViewTelemetry.HierarchicalRefreshRowsCallCount}");
                builder.AppendLine($"treeViewHierarchicalRealizeContainerCallCount={treeViewTelemetry.HierarchicalRealizeContainerCallCount}");
                builder.AppendLine($"treeViewHierarchicalRealizeContainerRecycledCount={treeViewTelemetry.HierarchicalRealizeContainerRecycledCount}");
                builder.AppendLine($"treeViewHierarchicalRealizeContainerNewCount={treeViewTelemetry.HierarchicalRealizeContainerNewCount}");
                builder.AppendLine($"treeViewHierarchicalRecycleContainerCallCount={treeViewTelemetry.HierarchicalRecycleContainerCallCount}");
                builder.AppendLine($"treeViewHierarchicalApplyContainerCallCount={treeViewTelemetry.HierarchicalApplyContainerCallCount}");
                builder.AppendLine($"treeViewSetHierarchicalItemExpandedCallCount={treeViewTelemetry.SetHierarchicalItemExpandedCallCount}");
                builder.AppendLine($"richTextBoxHostedRootRenderCallCount={richTextBoxTelemetry.HostedRootRenderCallCount}");
                builder.AppendLine($"ideEditorLayoutUpdatedCallCount={editorTelemetry.EditorLayoutUpdatedCallCount}");
                builder.AppendLine($"window_client={_window.ClientSize.X}x{_window.ClientSize.Y}");
                builder.AppendLine($"window_backbuffer={_window.BackBufferSize.X}x{_window.BackBufferSize.Y}");
                builder.AppendLine($"viewport={viewport.Width}x{viewport.Height}");
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
        RecordFrameTimingSample();
        CompleteCaptureRequests();
    }

    public void OnAfterUpdate()
    {
        CompleteFrameGates();
        CompleteResizeRequestsIfReady();
        _recorder?.RecordFrame(_window.ClientSize, Mouse.GetState(), Keyboard.GetState());
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

    private void OnRecordingTextInput(object? sender, Microsoft.Xna.Framework.TextInputEventArgs args)
    {
        _recorder?.RecordTextInput(args.Character);
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
        await ExecuteOnUiThreadAsync(ResetPointerMotionTelemetry, cancellationToken).ConfigureAwait(false);
        for (var i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t = ApplyPointerEasing(i / (float)steps, motion.Easing);
            var sample = Vector2.Lerp(start, position, t);
            await ExecuteOnUiThreadAsync(() => ApplyPointerMove(sample), cancellationToken).ConfigureAwait(false);
            if (i < steps)
            {
                await AdvanceFrameAsync(1, cancellationToken).ConfigureAwait(false);
                await ExecuteOnUiThreadAsync(() => RecordPointerMotionFrameTelemetry(i, steps, sample), cancellationToken).ConfigureAwait(false);
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
        _lastPointerMoveInputTelemetrySummary = FormatPointerMoveInputTelemetry(UiRoot.GetPointerMoveTelemetrySnapshotForTests());
    }

    private void ResetPointerMotionTelemetry()
    {
        _lastPointerMotionLowestFps = float.PositiveInfinity;
        _lastPointerMoveInputTelemetrySummary = "input=none";
        _lastPointerMotionTelemetrySummary = "motion=none";
    }

    private void RecordPointerMotionFrameTelemetry(int step, int steps, Vector2 pointerPosition)
    {
        var displayedFps = GetDisplayedFps();
        if (!TryParseDisplayedFps(displayedFps, out var fps) || fps >= _lastPointerMotionLowestFps)
        {
            return;
        }

        _lastPointerMotionLowestFps = fps;
        var performance = UiRoot.GetPerformanceTelemetrySnapshotForTests();
        var render = UiRoot.GetRenderTelemetrySnapshotForTests();
        _lastPointerMotionTelemetrySummary = string.Create(
            CultureInfo.InvariantCulture,
            $"motionStep={step}/{steps} motionAt={FormatVector2(pointerPosition)} motionFps={displayedFps} {_lastPointerMoveInputTelemetrySummary} frameUpdateMs={UiRoot.LastUpdateMs:0.###} frameDrawMs={UiRoot.LastDrawMs:0.###} renderSchedulingMs={performance.RenderSchedulingPhaseMilliseconds:0.###} dirtyRoots={performance.DirtyRootCount} retainedShallowMs={performance.RetainedShallowSyncMilliseconds:0.###} retainedDeepMs={performance.RetainedDeepSyncMilliseconds:0.###} retainedTraversal={performance.RetainedTraversalCount} drawVisualMs={render.DrawVisualTreeMilliseconds:0.###} dirtyDecision={render.LastDirtyDrawDecisionReason}");
    }

    private static string FormatPointerMoveInputTelemetry(UiPointerMoveTelemetrySnapshot telemetry)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"path={telemetry.PointerResolvePath} inputMs={telemetry.PointerDispatchMilliseconds:0.###} resolveMs={telemetry.PointerTargetResolveMilliseconds:0.###} hoverMs={telemetry.HoverUpdateMilliseconds:0.###} routeMs={telemetry.PointerRouteMilliseconds:0.###} finalHitTestMs={telemetry.PointerResolveFinalHitTestMilliseconds:0.###} hitTests={telemetry.HitTestCount} routedEvents={telemetry.RoutedEventCount} pointerEvents={telemetry.PointerEventCount}");
    }

    private static bool TryParseDisplayedFps(string displayedFps, out float fps)
    {
        return float.TryParse(displayedFps, NumberStyles.Float, CultureInfo.InvariantCulture, out fps);
    }

    private static string FormatVector2(Vector2 value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"({value.X:0.###},{value.Y:0.###})");
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
        KeyboardState? previousKeyboard = null,
        KeyboardState? currentKeyboard = null,
        IReadOnlyList<Microsoft.Xna.Framework.Input.Keys>? pressedKeys = null,
        IReadOnlyList<Microsoft.Xna.Framework.Input.Keys>? releasedKeys = null,
        IReadOnlyList<char>? textInput = null,
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
            Previous = new InputSnapshot(previousKeyboard ?? default, default, previous),
            Current = new InputSnapshot(currentKeyboard ?? default, default, current),
            PressedKeys = pressedKeys ?? Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = releasedKeys ?? Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            TextInput = textInput ?? Array.Empty<char>(),
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

    private static KeyboardState CreateKeyboardState(HashSet<Keys> keys)
    {
        return keys.Count == 0 ? default : new KeyboardState([.. keys.OrderBy(static key => (int)key)]);
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

    private static string FormatLayoutRectForTelemetry(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private void RecordFrameTimingSample()
    {
        var rootMetrics = UiRoot.GetMetricsSnapshot();
        var performance = UiRoot.GetPerformanceTelemetrySnapshotForTests();
        var render = UiRoot.GetRenderTelemetrySnapshotForTests();
        var retained = UiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        var sample = new FrameTimingSample(
            _frameTimingSerial + 1,
            DateTime.UtcNow,
            GetDisplayedFps(),
            UiRoot.LastUpdateMs,
            UiRoot.LastDrawMs,
            performance.InputPhaseMilliseconds,
            performance.BindingPhaseMilliseconds,
            performance.LayoutPhaseMilliseconds,
            performance.AnimationPhaseMilliseconds,
            performance.LayoutMeasureWorkMilliseconds,
            performance.LayoutMeasureExclusiveWorkMilliseconds,
            performance.LayoutArrangeWorkMilliseconds,
            performance.HottestLayoutMeasureElementType,
            performance.HottestLayoutMeasureElementName,
            performance.HottestLayoutMeasureElementPath,
            performance.HottestLayoutMeasureElementMilliseconds,
            performance.HottestLayoutArrangeElementType,
            performance.HottestLayoutArrangeElementName,
            performance.HottestLayoutArrangeElementPath,
            performance.HottestLayoutArrangeElementMilliseconds,
            performance.RenderSchedulingPhaseMilliseconds,
            performance.VisualUpdateMilliseconds,
            performance.RetainedShallowSyncMilliseconds,
            performance.RetainedDeepSyncMilliseconds,
            performance.DirtyRootCount,
            render.DrawVisualTreeMilliseconds,
            render.RetainedNodesVisited,
            render.RetainedNodesDrawn,
            render.LastDirtyDrawDecisionReason.ToString(),
            rootMetrics.LastDirtyRectCount,
            rootMetrics.LastDirtyAreaPercentage,
            retained.DirtyRegionAddCount,
            retained.DirtyRegionBoundsDeltaSuppressedByTransformScrollCount,
            UiRoot.GetLastSynchronizedDirtyRootSummaryForTests(limit: 8));

        lock (_sync)
        {
            _frameTimingSerial = sample.Serial;
            _frameTimingSamples.Add(sample);
            if (_frameTimingSamples.Count > MaxFrameTimingSamples)
            {
                _frameTimingSamples.RemoveRange(0, _frameTimingSamples.Count - MaxFrameTimingSamples);
            }
        }
    }

    private IReadOnlyList<FrameTimingSample> GetFrameTimingSamplesSince(int startFrameSerial, int maxFrameCount)
    {
        lock (_sync)
        {
            return _frameTimingSamples
                .Where(sample => sample.Serial > startFrameSerial)
                .Take(Math.Max(1, maxFrameCount))
                .ToArray();
        }
    }

    private static string FormatFrameTimingWindow(string artifactName, int startFrameSerial, IReadOnlyList<FrameTimingSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# InkkOops Frame Timing Window: {artifactName}");
        builder.AppendLine();
        builder.AppendLine($"startFrameSerial={startFrameSerial}");
        builder.AppendLine($"sampleCount={samples.Count}");
        if (samples.Count == 0)
        {
            builder.AppendLine("summary=none");
            return builder.ToString();
        }

        var worstTotal = samples.MaxBy(static sample => sample.TotalMilliseconds);
        var worstUpdate = samples.MaxBy(static sample => sample.UpdateMilliseconds);
        var worstDraw = samples.MaxBy(static sample => sample.DrawMilliseconds);
        var worstLayout = samples.MaxBy(static sample => sample.LayoutMilliseconds);
        var lowestFps = samples
            .Where(static sample => sample.DisplayedFpsValue.HasValue)
            .OrderBy(static sample => sample.DisplayedFpsValue!.Value)
            .FirstOrDefault();

        builder.AppendLine($"maxFrameTotalMs={worstTotal.TotalMilliseconds:0.###}");
        builder.AppendLine($"maxFrameTotalSerial={worstTotal.Serial}");
        builder.AppendLine($"maxUpdateMs={worstUpdate.UpdateMilliseconds:0.###}");
        builder.AppendLine($"maxUpdateSerial={worstUpdate.Serial}");
        builder.AppendLine($"maxDrawMs={worstDraw.DrawMilliseconds:0.###}");
        builder.AppendLine($"maxDrawSerial={worstDraw.Serial}");
        builder.AppendLine($"maxLayoutMs={worstLayout.LayoutMilliseconds:0.###}");
        builder.AppendLine($"maxLayoutSerial={worstLayout.Serial}");
        builder.AppendLine(lowestFps.Serial == 0
            ? "minDisplayedFps=unknown"
            : $"minDisplayedFps={lowestFps.DisplayedFps}");
        builder.AppendLine(lowestFps.Serial == 0
            ? "minDisplayedFpsSerial=none"
            : $"minDisplayedFpsSerial={lowestFps.Serial}");
        builder.AppendLine();
        builder.AppendLine("## Worst Frame");
        AppendFrameTimingSample(builder, worstTotal);
        builder.AppendLine();
        builder.AppendLine("## Samples");
        foreach (var sample in samples)
        {
            builder.Append("- serial=").Append(sample.Serial.ToString(CultureInfo.InvariantCulture));
            builder.Append(" totalMs=").Append(sample.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" updateMs=").Append(sample.UpdateMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" drawMs=").Append(sample.DrawMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" layoutMs=").Append(sample.LayoutMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" dirtyDecision=").Append(sample.DirtyDecision);
            builder.Append(" fps=").AppendLine(sample.DisplayedFps);
        }

        return builder.ToString();
    }

    private static void AppendFrameTimingSample(StringBuilder builder, FrameTimingSample sample)
    {
        builder.AppendLine($"serial={sample.Serial}");
        builder.AppendLine($"timestampUtc={sample.TimestampUtc:O}");
        builder.AppendLine($"displayedFps={sample.DisplayedFps}");
        builder.AppendLine($"totalMs={sample.TotalMilliseconds:0.###}");
        builder.AppendLine($"updateMs={sample.UpdateMilliseconds:0.###}");
        builder.AppendLine($"drawMs={sample.DrawMilliseconds:0.###}");
        builder.AppendLine($"inputMs={sample.InputMilliseconds:0.###}");
        builder.AppendLine($"bindingMs={sample.BindingMilliseconds:0.###}");
        builder.AppendLine($"layoutMs={sample.LayoutMilliseconds:0.###}");
        builder.AppendLine($"animationMs={sample.AnimationMilliseconds:0.###}");
        builder.AppendLine($"layoutMeasureWorkMs={sample.LayoutMeasureWorkMilliseconds:0.###}");
        builder.AppendLine($"layoutMeasureExclusiveWorkMs={sample.LayoutMeasureExclusiveWorkMilliseconds:0.###}");
        builder.AppendLine($"layoutArrangeWorkMs={sample.LayoutArrangeWorkMilliseconds:0.###}");
        builder.AppendLine($"hottestMeasure={sample.HottestMeasureType}#{sample.HottestMeasureName}:{sample.HottestMeasureMilliseconds:0.###}");
        builder.AppendLine($"hottestMeasurePath={sample.HottestMeasurePath}");
        builder.AppendLine($"hottestArrange={sample.HottestArrangeType}#{sample.HottestArrangeName}:{sample.HottestArrangeMilliseconds:0.###}");
        builder.AppendLine($"hottestArrangePath={sample.HottestArrangePath}");
        builder.AppendLine($"renderSchedulingMs={sample.RenderSchedulingMilliseconds:0.###}");
        builder.AppendLine($"visualUpdateMs={sample.VisualUpdateMilliseconds:0.###}");
        builder.AppendLine($"retainedShallowSyncMs={sample.RetainedShallowSyncMilliseconds:0.###}");
        builder.AppendLine($"retainedDeepSyncMs={sample.RetainedDeepSyncMilliseconds:0.###}");
        builder.AppendLine($"dirtyRootCount={sample.DirtyRootCount}");
        builder.AppendLine($"drawVisualTreeMs={sample.DrawVisualTreeMilliseconds:0.###}");
        builder.AppendLine($"retainedNodesVisited={sample.RetainedNodesVisited}");
        builder.AppendLine($"retainedNodesDrawn={sample.RetainedNodesDrawn}");
        builder.AppendLine($"dirtyDecision={sample.DirtyDecision}");
        builder.AppendLine($"dirtyRectCount={sample.DirtyRectCount}");
        builder.AppendLine($"dirtyAreaPercentage={sample.DirtyAreaPercentage:0.###}");
        builder.AppendLine($"dirtyRegionAddCount={sample.DirtyRegionAddCount}");
        builder.AppendLine($"transformScrollBoundsSuppressedCount={sample.TransformScrollBoundsSuppressedCount}");
        builder.AppendLine($"syncedDirtyRoots={sample.SyncedDirtyRoots}");
    }

    private static string FormatDirtyBoundsTrace(IReadOnlyList<string> trace, int limit)
    {
        if (trace.Count == 0)
        {
            return "none";
        }

        var start = Math.Max(0, trace.Count - Math.Max(1, limit));
        return string.Join(" || ", trace.Skip(start));
    }

    private readonly record struct FrameTimingSample(
        int Serial,
        DateTime TimestampUtc,
        string DisplayedFps,
        double UpdateMilliseconds,
        double DrawMilliseconds,
        double InputMilliseconds,
        double BindingMilliseconds,
        double LayoutMilliseconds,
        double AnimationMilliseconds,
        double LayoutMeasureWorkMilliseconds,
        double LayoutMeasureExclusiveWorkMilliseconds,
        double LayoutArrangeWorkMilliseconds,
        string HottestMeasureType,
        string HottestMeasureName,
        string HottestMeasurePath,
        double HottestMeasureMilliseconds,
        string HottestArrangeType,
        string HottestArrangeName,
        string HottestArrangePath,
        double HottestArrangeMilliseconds,
        double RenderSchedulingMilliseconds,
        double VisualUpdateMilliseconds,
        double RetainedShallowSyncMilliseconds,
        double RetainedDeepSyncMilliseconds,
        int DirtyRootCount,
        double DrawVisualTreeMilliseconds,
        int RetainedNodesVisited,
        int RetainedNodesDrawn,
        string DirtyDecision,
        int DirtyRectCount,
        double DirtyAreaPercentage,
        int DirtyRegionAddCount,
        int TransformScrollBoundsSuppressedCount,
        string SyncedDirtyRoots)
    {
        public double TotalMilliseconds => UpdateMilliseconds + DrawMilliseconds;

        public double? DisplayedFpsValue => float.TryParse(DisplayedFps, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps)
            ? fps
            : null;
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
