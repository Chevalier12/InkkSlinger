using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Game1 : Game
{
    private const int IdleThrottleSleepMilliseconds = 8;
    private const double FpsWindowTitleRefreshIntervalSeconds = 0.1d;
    private const string BaseWindowTitle = "InkkSlinger Controls Catalog";
    private const int MaxCalendarHoverDiagnosticsFrames = 480;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private readonly InkkOopsRuntimeOptions _inkkOopsOptions;
    private readonly InkkOopsHostConfiguration _inkkOopsHostConfiguration;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D? _uiCompositeTarget;
    private Panel _root = null!;
    private UiRoot _uiRoot = null!;
    private ControlsCatalogView? _catalogView;
    private WindowThemeBinding? _windowThemeBinding;
    private bool _shouldDrawUiThisFrame = true;
    private int _fpsFrameCount;
    private double _fpsElapsedSeconds;
    private readonly List<CalendarHoverRuntimeFrame> _calendarHoverFrames = new(MaxCalendarHoverDiagnosticsFrames);
    private bool _calendarHoverDiagnosticsSessionStarted;
    private long _lastCalendarHoverFrameTimestamp;
    private RichTextBoxTypingDiagnosticsSession? _richTextBoxTypingDiagnostics;
    private ControlsCatalogSidebarScrollRuntimeDiagnosticsSession? _sidebarScrollRuntimeDiagnostics;
    private InkkOopsGameHost? _inkkOopsHost;
    private InkkOopsRuntimeService? _inkkOopsRuntimeService;

    public Game1()
        : this(new InkkOopsRuntimeOptions())
    {
    }

    public Game1(InkkOopsRuntimeOptions inkkOopsOptions)
    {
        _inkkOopsOptions = inkkOopsOptions ?? throw new ArgumentNullException(nameof(inkkOopsOptions));
        _inkkOopsHostConfiguration = InkkOopsHostConfiguration.CreateDefault(typeof(Game1).Assembly);
        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = true;
        _window.AllowUserResizing = true;
        _window.SetClientSize(1280, 820);
        _window.Title = BaseWindowTitle;
    }

    protected override void Initialize()
    {
        var appMarkupPath = Path.Combine(AppContext.BaseDirectory, "App.xml");
        if (File.Exists(appMarkupPath))
        {
            XamlLoader.LoadApplicationResourcesFromFile(appMarkupPath, clearExisting: true);
        }

        _root = new Panel();
        _windowThemeBinding = new WindowThemeBinding(_window, _root);

        _catalogView = new ControlsCatalogView();
        _root.AddChild(_catalogView);

        _window.ClientSizeChanged += OnClientSizeChanged;
        _window.NativeWindow.TextInput += OnTextInput;

        _uiRoot = new UiRoot(_root)
        {
            UseRetainedRenderList = !_inkkOopsOptions.DisableRetainedRenderList,
            UseDirtyRegionRendering = !_inkkOopsOptions.DisableDirtyRegionRendering,
            UseConditionalDrawScheduling = true,
            UseSoftwareCursor = false
        };
        _richTextBoxTypingDiagnostics = RichTextBoxTypingDiagnosticsSession.TryCreate();
        _sidebarScrollRuntimeDiagnostics = ControlsCatalogSidebarScrollRuntimeDiagnosticsSession.CreateDefault();
        _inkkOopsHost = new InkkOopsGameHost(
            _uiRoot,
            _window,
            EnsureViewportMatchesBackBuffer,
            () => _uiCompositeTarget,
            ResolveArtifactRoot(),
            _inkkOopsHostConfiguration);
        _inkkOopsRuntimeService = new InkkOopsRuntimeService(
            _inkkOopsOptions,
            _inkkOopsHostConfiguration,
            _inkkOopsHost,
            requestAppExit: result =>
            {
                Environment.ExitCode = InkkOopsExitCodes.FromStatus(result.Status);
                _uiRoot.EnqueueDeferredOperation(Exit);
            });

        base.Initialize();
    }

    private string ResolveArtifactRoot()
    {
        return string.IsNullOrWhiteSpace(_inkkOopsOptions.ArtifactRoot)
            ? _inkkOopsHostConfiguration.DefaultArtifactRoot
            : _inkkOopsOptions.ArtifactRoot;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        try
        {
            UiTextRenderer.SetDefaultTypography("Segoe UI", 12f, "Normal", "Normal");
            UiTextRenderer.PrewarmDefaultGlyphs(GraphicsDevice);
        }
        catch
        {
            // Keep running without font asset so the control catalog remains usable.
        }
    }

    protected override void Update(GameTime gameTime)
    {
        EnsureBackBufferMatchesClientSize();
        var viewport = EnsureViewportMatchesBackBuffer();
        if (TryGetActiveCalendar(out _))
        {
            EnsureCalendarHoverDiagnosticsSessionStarted();
        }

        _uiRoot.Update(gameTime, viewport);
        _inkkOopsRuntimeService?.Update();
        _shouldDrawUiThisFrame = _uiRoot.ShouldDrawThisFrame(gameTime, viewport, GraphicsDevice);
        if (!_shouldDrawUiThisFrame)
        {
            SuppressDraw();
            var idleThrottleDelayMilliseconds = GetIdleThrottleDelayMilliseconds(IsActive, _shouldDrawUiThisFrame);
            if (idleThrottleDelayMilliseconds > 0)
            {
                Thread.Sleep(idleThrottleDelayMilliseconds);
            }
        }

        base.Update(gameTime);
    }

    internal static int GetIdleThrottleDelayMilliseconds(bool isActive, bool shouldDrawUiThisFrame)
    {
        return isActive && !shouldDrawUiThisFrame
            ? IdleThrottleSleepMilliseconds
            : 0;
    }

    internal static bool ShouldDrawUiOnCurrentFrame(bool scheduledDraw, bool targetRecreated)
    {
        return scheduledDraw || targetRecreated;
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = EnsureViewportMatchesBackBuffer();
        var targetRecreated = EnsureUiCompositeTarget(viewport);
        var shouldDrawUiThisFrame = ShouldDrawUiOnCurrentFrame(_shouldDrawUiThisFrame, targetRecreated);
        var shouldCaptureCalendarHoverDiagnostics = TryGetActiveCalendar(out var activeCalendar);
        if (targetRecreated)
        {
            _uiRoot.ForceFullRedrawForSurfaceReset();
        }

        if (shouldDrawUiThisFrame)
        {
            if (targetRecreated && !_shouldDrawUiThisFrame)
            {
                _uiRoot.RecordForcedDrawForSurfaceReset();
            }

            if (shouldCaptureCalendarHoverDiagnostics)
            {
                EnsureCalendarHoverDiagnosticsSessionStarted();
            }

            _sidebarScrollRuntimeDiagnostics?.TryCaptureBeforeDraw(gameTime, _uiRoot, _catalogView);
            GraphicsDevice.SetRenderTarget(_uiCompositeTarget);
            _uiRoot.Draw(_spriteBatch, gameTime);
            GraphicsDevice.SetRenderTarget(null);

            if (shouldCaptureCalendarHoverDiagnostics)
            {
                CaptureCalendarHoverDiagnosticsFrame(activeCalendar);
            }

            _richTextBoxTypingDiagnostics?.TryCaptureAfterDraw(gameTime, _uiRoot, _catalogView);
            _sidebarScrollRuntimeDiagnostics?.TryCaptureAfterDraw(gameTime, _uiRoot, _catalogView);
            _inkkOopsRuntimeService?.AfterDraw();
        }

        if (_uiCompositeTarget != null)
        {
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.Opaque,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone);
            _spriteBatch.Draw(
                _uiCompositeTarget,
                new Rectangle(0, 0, viewport.Width, viewport.Height),
                Color.White);
            _spriteBatch.End();
        }

        UpdateWindowTitleWithFps(gameTime);
        base.Draw(gameTime);
    }

    internal static bool TryBuildFpsWindowTitle(
        string baseTitle,
        int accumulatedFrameCount,
        double accumulatedElapsedSeconds,
        out string title)
    {
        if (accumulatedElapsedSeconds < FpsWindowTitleRefreshIntervalSeconds)
        {
            title = baseTitle;
            return false;
        }

        var fps = accumulatedElapsedSeconds <= 0d
            ? 0d
            : accumulatedFrameCount / accumulatedElapsedSeconds;
        title = $"{baseTitle} | FPS: {fps:0.0}";
        return true;
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _window.ClientSizeChanged -= OnClientSizeChanged;
        _window.NativeWindow.TextInput -= OnTextInput;
        _windowThemeBinding?.Dispose();
        _windowThemeBinding = null;
        _richTextBoxTypingDiagnostics?.Dispose();
        _richTextBoxTypingDiagnostics = null;
        _sidebarScrollRuntimeDiagnostics?.Dispose();
        _sidebarScrollRuntimeDiagnostics = null;
        _inkkOopsRuntimeService?.Dispose();
        _inkkOopsRuntimeService = null;
        _inkkOopsHost = null;

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = null;
        UiDrawing.ReleaseDeviceResources(GraphicsDevice);
        _uiRoot.Shutdown();
        _window.Dispose();
        base.OnExiting(sender, args);
    }

    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        _richTextBoxTypingDiagnostics?.TryCaptureBeforeInput(args.Character, _uiRoot, _catalogView);
        _uiRoot.EnqueueTextInput(args.Character);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_uiRoot != null)
        {
            EnsureBackBufferMatchesClientSize();
        }
    }

    private bool EnsureUiCompositeTarget(Viewport viewport)
    {
        if (_uiCompositeTarget != null &&
            !_uiCompositeTarget.IsDisposed &&
            _uiCompositeTarget.Width == viewport.Width &&
            _uiCompositeTarget.Height == viewport.Height)
        {
            return false;
        }

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = new RenderTarget2D(
            GraphicsDevice,
            Math.Max(1, viewport.Width),
            Math.Max(1, viewport.Height),
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
        return true;
    }

    private void EnsureBackBufferMatchesClientSize()
    {
        var clientSize = _window.ClientSize;
        if (clientSize.X <= 0 || clientSize.Y <= 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var backBufferSize = _window.BackBufferSize;
        var preferredMatches = clientSize.X == backBufferSize.X && clientSize.Y == backBufferSize.Y;
        var actualMatches = clientSize.X == viewport.Width && clientSize.Y == viewport.Height;
        if (preferredMatches && actualMatches)
        {
            return;
        }

        _window.SetClientSize(clientSize.X, clientSize.Y, applyChanges: true);
    }

    private Viewport EnsureViewportMatchesBackBuffer()
    {
        var presentation = GraphicsDevice.PresentationParameters;
        var targetWidth = Math.Max(1, presentation.BackBufferWidth);
        var targetHeight = Math.Max(1, presentation.BackBufferHeight);
        var viewport = GraphicsDevice.Viewport;

        if (viewport.X != 0 ||
            viewport.Y != 0 ||
            viewport.Width != targetWidth ||
            viewport.Height != targetHeight)
        {
            viewport = new Viewport(0, 0, targetWidth, targetHeight);
            GraphicsDevice.Viewport = viewport;
        }

        return viewport;
    }

    private void UpdateWindowTitleWithFps(GameTime gameTime)
    {
        _fpsFrameCount++;
        _fpsElapsedSeconds += gameTime.ElapsedGameTime.TotalSeconds;

        if (!TryBuildFpsWindowTitle(BaseWindowTitle, _fpsFrameCount, _fpsElapsedSeconds, out var title))
        {
            return;
        }

        _window.Title = title;
        _fpsFrameCount = 0;
        _fpsElapsedSeconds = 0d;
    }

    private void EnsureCalendarHoverDiagnosticsSessionStarted()
    {
        if (_calendarHoverDiagnosticsSessionStarted)
        {
            return;
        }

        _calendarHoverDiagnosticsSessionStarted = true;
        _lastCalendarHoverFrameTimestamp = 0;
        ResetCalendarHoverDiagnosticsFrameState();
    }

    private void ResetCalendarHoverDiagnosticsFrameState()
    {
        AnimationManager.Current.ResetTelemetryForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Button.ResetTimingForTests();
        CalendarDayButton.ResetTimingForTests();
        DropShadowEffect.ResetTimingForTests();
        UiTextRenderer.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
    }

    private void CaptureCalendarHoverDiagnosticsFrame(Calendar activeCalendar)
    {
        if (_calendarHoverFrames.Count >= MaxCalendarHoverDiagnosticsFrames)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var fps = _lastCalendarHoverFrameTimestamp == 0
            ? 0d
            : (double)Stopwatch.Frequency / (now - _lastCalendarHoverFrameTimestamp);
        _lastCalendarHoverFrameTimestamp = now;

        var hovered = _uiRoot.GetHoveredElementForDiagnostics();
        var hoveredDayButton = FindAncestorOrSelf<CalendarDayButton>(hovered);
        var hoveredButton = FindAncestorOrSelf<Button>(hovered);
        var hoveredPath = BuildTypePath(hovered);
        var animation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var input = _uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        var render = _uiRoot.GetRenderTelemetrySnapshotForTests();
        var performance = _uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var freezable = Freezable.GetTelemetrySnapshotForTests();
        var invalidationBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var hitTestInstrumentation = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var hasHitTestMetrics = _uiRoot.TryGetLastPointerResolveHitTestMetricsForTests(out var hitTestMetrics);
        var button = Button.GetTimingSnapshotForTests();
        var dayButton = CalendarDayButton.GetTimingSnapshotForTests();
        var effect = DropShadowEffect.GetTimingSnapshotForTests();
        var text = UiTextRenderer.GetTimingSnapshotForTests();
        var layout = TextLayout.GetMetricsSnapshot();
        var elementRender = UIElement.GetRenderTimingSnapshotForTests();

        _calendarHoverFrames.Add(new CalendarHoverRuntimeFrame(
            _calendarHoverFrames.Count,
            hovered?.GetType().Name ?? "null",
            hoveredDayButton?.DayText ?? string.Empty,
            GetButtonText(hoveredButton),
            ClassifyHoverPhase(hovered, hoveredDayButton, hoveredButton),
            hoveredPath,
            fps,
            _uiRoot.LastUpdateMs,
            _uiRoot.LastDrawMs,
            performance.InputPhaseMilliseconds,
            performance.BindingPhaseMilliseconds,
            performance.LayoutPhaseMilliseconds,
            performance.AnimationPhaseMilliseconds,
            performance.RenderSchedulingPhaseMilliseconds,
            performance.VisualUpdateMilliseconds,
            input.HoverUpdateMilliseconds,
            input.PointerTargetResolveMilliseconds,
            input.PointerResolvePath,
            input.PointerResolveHoverReuseCheckMilliseconds,
            input.PointerResolveFinalHitTestMilliseconds,
            hasHitTestMetrics ? hitTestMetrics.NodesVisited : 0,
            hasHitTestMetrics ? hitTestMetrics.MaxDepth : 0,
            hasHitTestMetrics ? hitTestMetrics.TotalMilliseconds : 0d,
            hasHitTestMetrics ? hitTestMetrics.TopLevelSubtreeSummary : "none",
            hasHitTestMetrics ? hitTestMetrics.HottestTypeSummary : "none",
            hasHitTestMetrics ? hitTestMetrics.HottestNodeSummary : "none",
            hasHitTestMetrics ? hitTestMetrics.TraversalSummary : "none",
            hasHitTestMetrics ? hitTestMetrics.RejectSummary : "none",
            hitTestInstrumentation.ItemsPresenterNeighborProbes,
            hitTestInstrumentation.ItemsPresenterFullFallbackScans,
            hitTestInstrumentation.LegacyEnumerableFallbacks,
            hitTestInstrumentation.MonotonicPanelFastPathCount,
            hitTestInstrumentation.SimpleSlotHitCount,
            hitTestInstrumentation.TransformedBoundsHitCount,
            hitTestInstrumentation.ClipRejectCount,
            hitTestInstrumentation.VisibilityRejectCount,
            hitTestInstrumentation.PanelTraversalCount,
            hitTestInstrumentation.VisualTraversalZSortCount,
            input.PointerRouteMilliseconds,
            performance.FrameUpdateParticipantRefreshMilliseconds,
            performance.FrameUpdateParticipantUpdateMilliseconds,
            performance.HottestFrameUpdateParticipantType,
            performance.HottestFrameUpdateParticipantMilliseconds,
            performance.LayoutMeasureWorkMilliseconds,
            performance.LayoutMeasureExclusiveWorkMilliseconds,
            performance.LayoutArrangeWorkMilliseconds,
            performance.HottestLayoutMeasureElementType,
            performance.HottestLayoutMeasureElementName,
            performance.HottestLayoutMeasureElementMilliseconds,
            performance.HottestLayoutArrangeElementType,
            performance.HottestLayoutArrangeElementName,
            performance.HottestLayoutArrangeElementMilliseconds,
            animation.BeginStoryboardMilliseconds,
            animation.StoryboardStartMilliseconds,
            animation.ComposeMilliseconds,
            animation.ComposeCollectMilliseconds,
            animation.ComposeSortMilliseconds,
            animation.ComposeMergeMilliseconds,
            animation.ComposeApplyMilliseconds,
            animation.ComposeBatchEndMilliseconds,
            animation.ActiveStoryboardCount,
            animation.ActiveLaneCount,
            animation.HottestSetValuePathSummary,
            animation.HottestSetValueWriteSummary,
            animation.HottestSetValueWriteMilliseconds,
            freezable.OnChangedMilliseconds,
            freezable.EndBatchMilliseconds,
            invalidationBatch.FlushMilliseconds,
            invalidationBatch.FlushCount,
            invalidationBatch.FlushTargetCount,
            invalidationBatch.MaxPendingTargetCount,
            freezable.HottestOnChangedType,
            freezable.HottestOnChangedMilliseconds,
            freezable.HottestEndBatchType,
            freezable.HottestEndBatchMilliseconds,
            TicksToMilliseconds(button.RenderElapsedTicks),
            TicksToMilliseconds(button.RenderChromeElapsedTicks),
            TicksToMilliseconds(button.RenderTextPreparationElapsedTicks),
            TicksToMilliseconds(button.RenderTextDrawDispatchElapsedTicks),
            button.RenderTextPreparationCallCount,
            TicksToMilliseconds(dayButton.RenderElapsedTicks),
            dayButton.RenderCallCount,
            dayButton.NonEmptyRenderCallCount,
            TicksToMilliseconds(effect.RenderElapsedTicks),
            TicksToMilliseconds(effect.BlurPathElapsedTicks),
            TicksToMilliseconds(effect.DrawBlurSlicesElapsedTicks),
            effect.RenderCallCount,
            effect.BlurPathCallCount,
            effect.CalendarDayRenderCallCount,
            effect.CalendarDayBlurPathCallCount,
            TicksToMilliseconds(elementRender.RenderSelfElapsedTicks),
            elementRender.RenderSelfCallCount,
            elementRender.HottestRenderSelfType,
            elementRender.HottestRenderSelfName,
            elementRender.HottestRenderSelfMilliseconds,
            elementRender.HottestRenderSelfTypeSummary,
            TicksToMilliseconds(text.DrawStringElapsedTicks),
            text.DrawStringCallCount,
            TicksToMilliseconds(text.MeasureWidthElapsedTicks),
            text.MeasureWidthCallCount,
            TicksToMilliseconds(text.GetLineHeightElapsedTicks),
            text.GetLineHeightCallCount,
            text.MetricsCacheHitCount,
            text.MetricsCacheMissCount,
            text.HottestDrawStringText,
            text.HottestDrawStringTypography,
            text.HottestDrawStringMilliseconds,
            text.HottestMeasureWidthText,
            text.HottestMeasureWidthTypography,
            text.HottestMeasureWidthMilliseconds,
            text.HottestLineHeightTypography,
            text.HottestLineHeightMilliseconds,
            TicksToMilliseconds(layout.LayoutElapsedTicks),
            TicksToMilliseconds(layout.BuildElapsedTicks),
            layout.BuildCount,
            layout.CacheMissCount,
            render.RetainedNodesVisited,
            render.RetainedNodesDrawn,
            render.RetainedTraversalCount,
            render.DirtyRegionTraversalCount,
            activeCalendar.DayButtonsForTesting.Count));

        ResetCalendarHoverDiagnosticsFrameState();
    }

    private static string FormatRuntimeFrame(CalendarHoverRuntimeFrame frame)
    {
        return
            $"frame={frame.FrameIndex:000} phase={frame.HoverPhase} hovered={frame.HoveredType} day={frame.HoveredDayText} button={frame.HoveredButtonText} hoveredPath={frame.HoveredPath} fps={frame.Fps:0.###} " +
            $"uiUpdateMs={frame.UiUpdateMs:0.###} uiDrawMs={frame.UiDrawMs:0.###} inputPhaseMs={frame.InputPhaseMs:0.###} bindingPhaseMs={frame.BindingPhaseMs:0.###} layoutPhaseMs={frame.LayoutPhaseMs:0.###} animationPhaseMs={frame.AnimationPhaseMs:0.###} renderSchedulingPhaseMs={frame.RenderSchedulingPhaseMs:0.###} visualUpdateMs={frame.VisualUpdateMs:0.###} hoverMs={frame.HoverMs:0.###} resolveMs={frame.ResolveMs:0.###} resolveHoverReuseMs={frame.ResolveHoverReuseMs:0.###} resolveFinalHitTestMs={frame.ResolveFinalHitTestMs:0.###} routeMs={frame.RouteMs:0.###} " +
            $"resolvePath={frame.ResolvePath} hitTestNodes={frame.HitTestNodesVisited} hitTestDepth={frame.HitTestMaxDepth} hitTestMs={frame.HitTestMs:0.###} hitTestTopSubtrees={frame.HitTestTopSubtreeSummary} hitTestHotTypes={frame.HitTestHottestTypeSummary} hitTestHotNode={frame.HitTestHottestNodeSummary} hitTestTraversal={frame.HitTestTraversalSummary} hitTestRejects={frame.HitTestRejectSummary} " +
            $"hitTestNeighborProbes={frame.HitTestItemsPresenterNeighborProbes} hitTestFullFallbacks={frame.HitTestItemsPresenterFullFallbackScans} hitTestLegacyFallbacks={frame.HitTestLegacyEnumerableFallbacks} hitTestMonotonicFastPaths={frame.HitTestMonotonicPanelFastPathCount} hitTestSimpleSlot={frame.HitTestSimpleSlotHitCount} hitTestTransformedBounds={frame.HitTestTransformedBoundsHitCount} hitTestClipRejects={frame.HitTestClipRejectCount} hitTestHiddenRejects={frame.HitTestVisibilityRejectCount} hitTestPanels={frame.HitTestPanelTraversalCount} hitTestZSorts={frame.HitTestVisualTraversalZSortCount} " +
            $"updateParticipantRefreshMs={frame.UpdateParticipantRefreshMs:0.###} updateParticipantUpdateMs={frame.UpdateParticipantUpdateMs:0.###} hottestUpdateParticipant={frame.HottestUpdateParticipantType}:{frame.HottestUpdateParticipantMs:0.###} " +
            $"frameworkMeasureMs={frame.FrameworkMeasureMs:0.###} frameworkMeasureExclusiveMs={frame.FrameworkMeasureExclusiveMs:0.###} frameworkArrangeMs={frame.FrameworkArrangeMs:0.###} hottestMeasureElement={frame.HottestMeasureElementType}({frame.HottestMeasureElementName}):{frame.HottestMeasureElementMs:0.###} hottestArrangeElement={frame.HottestArrangeElementType}({frame.HottestArrangeElementName}):{frame.HottestArrangeElementMs:0.###} " +
            $"beginMs={frame.AnimationBeginMs:0.###} startMs={frame.AnimationStartMs:0.###} composeMs={frame.AnimationComposeMs:0.###} composeCollectMs={frame.AnimationComposeCollectMs:0.###} " +
            $"composeSortMs={frame.AnimationComposeSortMs:0.###} composeMergeMs={frame.AnimationComposeMergeMs:0.###} composeApplyMs={frame.AnimationComposeApplyMs:0.###} composeBatchEndMs={frame.AnimationComposeBatchEndMs:0.###} " +
            $"activeStoryboards={frame.ActiveStoryboards} activeLanes={frame.ActiveLanes} freezableOnChangedMs={frame.FreezableOnChangedMs:0.###} freezableEndBatchMs={frame.FreezableEndBatchMs:0.###} freezableBatchFlushMs={frame.FreezableBatchFlushMs:0.###} freezableBatchFlushes={frame.FreezableBatchFlushCount} freezableBatchFlushTargets={frame.FreezableBatchFlushTargetCount} maxPendingBatchTargets={frame.FreezableBatchMaxPendingTargetCount} hottestFreezableOnChanged={frame.HottestFreezableOnChangedType}:{frame.HottestFreezableOnChangedMs:0.###} hottestFreezableEndBatch={frame.HottestFreezableEndBatchType}:{frame.HottestFreezableEndBatchMs:0.###} hottestSetValuePaths={frame.HottestSetValuePaths} " +
            $"hottestSetValueWrite={frame.HottestSetValueWrite} hottestSetValueWriteMs={frame.HottestSetValueWriteMs:0.###} " +
            $"buttonRenderMs={frame.ButtonRenderMs:0.###} buttonChromeMs={frame.ButtonChromeMs:0.###} buttonTextPrepMs={frame.ButtonTextPreparationMs:0.###} buttonTextDrawMs={frame.ButtonTextDrawMs:0.###} buttonTextPrepCalls={frame.ButtonTextPreparationCalls} " +
            $"calendarDayRenderMs={frame.CalendarDayRenderMs:0.###} calendarDayRenderCalls={frame.CalendarDayRenderCalls} calendarDayNonEmptyCalls={frame.CalendarDayNonEmptyRenderCalls} " +
            $"dropShadowRenderMs={frame.DropShadowRenderMs:0.###} dropShadowBlurPathMs={frame.DropShadowBlurPathMs:0.###} dropShadowBlurSlicesMs={frame.DropShadowBlurSlicesMs:0.###} " +
            $"dropShadowCalls={frame.DropShadowRenderCalls} dropShadowBlurCalls={frame.DropShadowBlurPathCalls} calendarDayShadowCalls={frame.CalendarDayShadowRenderCalls} calendarDayShadowBlurCalls={frame.CalendarDayShadowBlurPathCalls} " +
            $"uiElementRenderMs={frame.UiElementRenderMs:0.###} uiElementRenderCalls={frame.UiElementRenderCalls} hottestUiElement={frame.HottestUiElementType}({frame.HottestUiElementName}):{frame.HottestUiElementMs:0.###} hottestUiElementTypes={frame.HottestUiElementTypes} " +
            $"textDrawMs={frame.TextDrawMs:0.###} textDrawCalls={frame.TextDrawCalls} textMeasureMs={frame.TextMeasureWidthMs:0.###} textMeasureCalls={frame.TextMeasureWidthCalls} lineHeightMs={frame.TextLineHeightMs:0.###} lineHeightCalls={frame.TextLineHeightCalls} textMetricsHits={frame.TextMetricsCacheHits} textMetricsMisses={frame.TextMetricsCacheMisses} hottestTextDraw={frame.HottestTextDrawText}|{frame.HottestTextDrawTypography}:{frame.HottestTextDrawMs:0.###} hottestTextMeasure={frame.HottestTextMeasureText}|{frame.HottestTextMeasureTypography}:{frame.HottestTextMeasureMs:0.###} hottestLineHeight={frame.HottestLineHeightTypography}:{frame.HottestLineHeightMs:0.###} " +
            $"layoutMs={frame.TextLayoutMs:0.###} layoutBuildMs={frame.TextLayoutBuildMs:0.###} layoutBuilds={frame.TextLayoutBuildCount} layoutMisses={frame.TextLayoutCacheMissCount} " +
            $"retainedVisited={frame.RetainedNodesVisited} retainedDrawn={frame.RetainedNodesDrawn} retainedTraversals={frame.RetainedTraversalCount} dirtyTraversals={frame.DirtyRegionTraversalCount} dayButtonCount={frame.CalendarDayButtonCount}";
    }

    private string ClassifyHoverPhase(UIElement? hovered, CalendarDayButton? hoveredDayButton, Button? hoveredButton)
    {
        if (hoveredDayButton != null)
        {
            return "CalendarDay";
        }

        if (hoveredButton != null && IsSidebarCatalogButton(hoveredButton))
        {
            return "SidebarButton";
        }

        return hovered?.GetType().Name ?? "null";
    }

    private bool IsSidebarCatalogButton(Button button)
    {
        if (_catalogView?.FindName("ControlButtonsHost") is not StackPanel sidebarHost)
        {
            return false;
        }

        for (var current = button as UIElement; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, sidebarHost))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetButtonText(Button? button)
    {
        if (button?.Content is string text)
        {
            return text;
        }

        return string.Empty;
    }

    private bool TryGetActiveCalendar(out Calendar calendar)
    {
        calendar = null!;
        if (_catalogView == null)
        {
            return false;
        }

        var previewHost = _catalogView.FindName("PreviewHost") as ContentControl;
        if (previewHost?.Content is not UIElement previewRoot)
        {
            return false;
        }

        var found = FindFirstVisualChild<Calendar>(previewRoot);
        if (found == null)
        {
            return false;
        }

        calendar = found;
        return true;
    }

    private static bool TryGetActiveRichTextBoxContext(
        ControlsCatalogView? catalogView,
        out RichTextBoxView view,
        out RichTextBox editor)
    {
        view = null!;
        editor = null!;
        if (catalogView?.FindName("PreviewHost") is not ContentControl previewHost ||
            previewHost.Content is not RichTextBoxView richTextBoxView)
        {
            return false;
        }

        if (richTextBoxView.FindName("Editor") is not RichTextBox richTextBox)
        {
            return false;
        }

        view = richTextBoxView;
        editor = richTextBox;
        return true;
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindAncestorOrSelf<TElement>(UIElement? element)
        where TElement : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static string BuildTypePath(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        var parts = new List<string>();
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            parts.Add(current.GetType().Name);
        }

        return string.Join(">", parts);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private sealed record CalendarHoverRuntimeFrame(
        int FrameIndex,
        string HoveredType,
        string HoveredDayText,
        string HoveredButtonText,
        string HoverPhase,
        string HoveredPath,
        double Fps,
        double UiUpdateMs,
        double UiDrawMs,
        double InputPhaseMs,
        double BindingPhaseMs,
        double LayoutPhaseMs,
        double AnimationPhaseMs,
        double RenderSchedulingPhaseMs,
        double VisualUpdateMs,
        double HoverMs,
        double ResolveMs,
        string ResolvePath,
        double ResolveHoverReuseMs,
        double ResolveFinalHitTestMs,
        int HitTestNodesVisited,
        int HitTestMaxDepth,
        double HitTestMs,
        string HitTestTopSubtreeSummary,
        string HitTestHottestTypeSummary,
        string HitTestHottestNodeSummary,
        string HitTestTraversalSummary,
        string HitTestRejectSummary,
        int HitTestItemsPresenterNeighborProbes,
        int HitTestItemsPresenterFullFallbackScans,
        int HitTestLegacyEnumerableFallbacks,
        int HitTestMonotonicPanelFastPathCount,
        int HitTestSimpleSlotHitCount,
        int HitTestTransformedBoundsHitCount,
        int HitTestClipRejectCount,
        int HitTestVisibilityRejectCount,
        int HitTestPanelTraversalCount,
        int HitTestVisualTraversalZSortCount,
        double RouteMs,
        double UpdateParticipantRefreshMs,
        double UpdateParticipantUpdateMs,
        string HottestUpdateParticipantType,
        double HottestUpdateParticipantMs,
        double FrameworkMeasureMs,
        double FrameworkMeasureExclusiveMs,
        double FrameworkArrangeMs,
        string HottestMeasureElementType,
        string HottestMeasureElementName,
        double HottestMeasureElementMs,
        string HottestArrangeElementType,
        string HottestArrangeElementName,
        double HottestArrangeElementMs,
        double AnimationBeginMs,
        double AnimationStartMs,
        double AnimationComposeMs,
        double AnimationComposeCollectMs,
        double AnimationComposeSortMs,
        double AnimationComposeMergeMs,
        double AnimationComposeApplyMs,
        double AnimationComposeBatchEndMs,
        int ActiveStoryboards,
        int ActiveLanes,
        string HottestSetValuePaths,
        string HottestSetValueWrite,
        double HottestSetValueWriteMs,
        double FreezableOnChangedMs,
        double FreezableEndBatchMs,
        double FreezableBatchFlushMs,
        int FreezableBatchFlushCount,
        int FreezableBatchFlushTargetCount,
        int FreezableBatchMaxPendingTargetCount,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMs,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMs,
        double ButtonRenderMs,
        double ButtonChromeMs,
        double ButtonTextPreparationMs,
        double ButtonTextDrawMs,
        int ButtonTextPreparationCalls,
        double CalendarDayRenderMs,
        int CalendarDayRenderCalls,
        int CalendarDayNonEmptyRenderCalls,
        double DropShadowRenderMs,
        double DropShadowBlurPathMs,
        double DropShadowBlurSlicesMs,
        int DropShadowRenderCalls,
        int DropShadowBlurPathCalls,
        int CalendarDayShadowRenderCalls,
        int CalendarDayShadowBlurPathCalls,
        double UiElementRenderMs,
        int UiElementRenderCalls,
        string HottestUiElementType,
        string HottestUiElementName,
        double HottestUiElementMs,
        string HottestUiElementTypes,
        double TextDrawMs,
        int TextDrawCalls,
        double TextMeasureWidthMs,
        int TextMeasureWidthCalls,
        double TextLineHeightMs,
        int TextLineHeightCalls,
        int TextMetricsCacheHits,
        int TextMetricsCacheMisses,
        string HottestTextDrawText,
        string HottestTextDrawTypography,
        double HottestTextDrawMs,
        string HottestTextMeasureText,
        string HottestTextMeasureTypography,
        double HottestTextMeasureMs,
        string HottestLineHeightTypography,
        double HottestLineHeightMs,
        double TextLayoutMs,
        double TextLayoutBuildMs,
        int TextLayoutBuildCount,
        int TextLayoutCacheMissCount,
        int RetainedNodesVisited,
        int RetainedNodesDrawn,
        int RetainedTraversalCount,
        int DirtyRegionTraversalCount,
        int CalendarDayButtonCount);

    private sealed class RichTextBoxTypingDiagnosticsSession : IDisposable
    {
        private const string DefaultLogRelativePath = "artifacts/diagnostics/controls-catalog-richtextbox-embedded-ui-manual-hotspot.txt";
        private readonly string _logPath;
        private readonly StreamWriter _writer;
        private readonly Queue<PendingTypingSample> _pendingSamples = new();
        private int _sampleIndex;
        private long _lastFrameTimestamp;

        private RichTextBoxTypingDiagnosticsSession(string logPath)
        {
            _logPath = Path.GetFullPath(logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            _writer = new StreamWriter(_logPath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            _writer.WriteLine($"scenario=Controls Catalog RichTextBox embedded UI manual typing draw hotspot diagnostics");
            _writer.WriteLine($"timestamp_utc={DateTime.UtcNow:O}");
            _writer.WriteLine($"log_path={_logPath}");
            _writer.WriteLine("step_1=open Controls Catalog");
            _writer.WriteLine("step_2=click RichTextBox button in sidebar");
            _writer.WriteLine("step_3=click Embedded UI preset button");
            _writer.WriteLine("step_4=click inside RichTextBox editor");
            _writer.WriteLine("step_5=type one character at a time and let each frame render");
            _writer.WriteLine();
            _writer.WriteLine("samples:");
        }

        public static RichTextBoxTypingDiagnosticsSession? TryCreate()
        {
            var enabled = Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXTBOX_DIAGNOSTICS");
            var logPath = Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXTBOX_DIAGNOSTICS_LOG");
            if (!string.Equals(enabled, "1", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(logPath))
            {
                return null;
            }

            return new RichTextBoxTypingDiagnosticsSession(
                string.IsNullOrWhiteSpace(logPath)
                    ? Path.Combine(Environment.CurrentDirectory, DefaultLogRelativePath)
                    : logPath);
        }

        public void TryCaptureBeforeInput(char character, UiRoot uiRoot, ControlsCatalogView? catalogView)
        {
            if (!TryGetActiveRichTextBoxContext(catalogView, out var view, out var editor))
            {
                return;
            }

            if (!editor.IsFocused)
            {
                return;
            }

            var documentText = DocumentEditing.GetText(editor.Document);
            var hasHostedUi = documentText.Contains('\uFFFC');
            if (!hasHostedUi)
            {
                return;
            }

            _pendingSamples.Enqueue(
                new PendingTypingSample(
                    character,
                    _sampleIndex++,
                    Stopwatch.GetTimestamp(),
                    documentText.Length,
                    editor.CaretIndex,
                    editor.GetPerformanceSnapshot(),
                    view.GetDiagnosticsSnapshotForTests(),
                    uiRoot.GetPerformanceTelemetrySnapshotForTests(),
                    uiRoot.GetRenderTelemetrySnapshotForTests(),
                    UiTextRenderer.GetTimingSnapshotForTests(),
                    UIElement.GetRenderTimingSnapshotForTests(),
                    Button.GetTimingSnapshotForTests(),
                    TextLayout.GetMetricsSnapshot()));
        }

        public void TryCaptureAfterDraw(GameTime gameTime, UiRoot uiRoot, ControlsCatalogView? catalogView)
        {
            if (_pendingSamples.Count == 0)
            {
                return;
            }

            if (!TryGetActiveRichTextBoxContext(catalogView, out var view, out var editor))
            {
                _pendingSamples.Dequeue();
                return;
            }

            var pending = _pendingSamples.Dequeue();
            var now = Stopwatch.GetTimestamp();
            var fps = _lastFrameTimestamp == 0
                ? 0d
                : (double)Stopwatch.Frequency / Math.Max(1L, now - _lastFrameTimestamp);
            _lastFrameTimestamp = now;

            var afterEditor = editor.GetPerformanceSnapshot();
            var afterView = view.GetDiagnosticsSnapshotForTests();
            var afterPerf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
            var afterText = UiTextRenderer.GetTimingSnapshotForTests();
            var afterElementRender = UIElement.GetRenderTimingSnapshotForTests();
            var afterButton = Button.GetTimingSnapshotForTests();
            var afterTextLayout = TextLayout.GetMetricsSnapshot();

            _writer.WriteLine(
                $"sample={pending.Index:000} char={EscapeChar(pending.Character)} fps={fps:0.###} " +
                $"elapsed_since_key_ms={TicksToMilliseconds(now - pending.TimestampTicks):0.###} gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:0.###} " +
                $"textLenBefore={pending.TextLengthBefore} textLenAfter={DocumentEditing.GetText(editor.Document).Length} caretBefore={pending.CaretIndexBefore} caretAfter={editor.CaretIndex} " +
                $"uiUpdateMs={uiRoot.LastUpdateMs:0.###} uiDrawMs={uiRoot.LastDrawMs:0.###} " +
                $"bindingMs={afterPerf.BindingPhaseMilliseconds:0.###} " +
                $"layoutMs={afterPerf.LayoutPhaseMilliseconds:0.###} " +
                $"renderScheduleMs={afterPerf.RenderSchedulingPhaseMilliseconds:0.###} " +
                $"frameworkMeasureWorkMs={afterPerf.LayoutMeasureWorkMilliseconds:0.###} " +
                $"frameworkArrangeWorkMs={afterPerf.LayoutArrangeWorkMilliseconds:0.###} " +
                $"dirtyRoots={afterRender.DirtyRootCount} " +
                $"retainedTraversals={afterRender.RetainedTraversalCount} " +
                $"retainedDrawn={afterRender.RetainedNodesDrawn} " +
                $"textDrawMs={TicksToMilliseconds(afterText.DrawStringElapsedTicks - pending.TextBefore.DrawStringElapsedTicks):0.###} " +
                $"textDrawCalls={afterText.DrawStringCallCount - pending.TextBefore.DrawStringCallCount} " +
                $"textMeasureMs={TicksToMilliseconds(afterText.MeasureWidthElapsedTicks - pending.TextBefore.MeasureWidthElapsedTicks):0.###} " +
                $"textMeasureCalls={afterText.MeasureWidthCallCount - pending.TextBefore.MeasureWidthCallCount} " +
                $"textLayoutMs={TicksToMilliseconds(afterTextLayout.LayoutElapsedTicks - pending.TextLayoutBefore.LayoutElapsedTicks):0.###} " +
                $"textLayoutBuildMs={TicksToMilliseconds(afterTextLayout.BuildElapsedTicks - pending.TextLayoutBefore.BuildElapsedTicks):0.###} " +
                $"textLayoutBuilds={afterTextLayout.BuildCount - pending.TextLayoutBefore.BuildCount} " +
                $"textLayoutMisses={afterTextLayout.CacheMissCount - pending.TextLayoutBefore.CacheMissCount} " +
                $"uiElementRenderMs={TicksToMilliseconds(afterElementRender.RenderSelfElapsedTicks - pending.ElementRenderBefore.RenderSelfElapsedTicks):0.###} " +
                $"uiElementRenderCalls={afterElementRender.RenderSelfCallCount - pending.ElementRenderBefore.RenderSelfCallCount} " +
                $"buttonRenderMs={TicksToMilliseconds(afterButton.RenderElapsedTicks - pending.ButtonBefore.RenderElapsedTicks):0.###} " +
                $"buttonChromeMs={TicksToMilliseconds(afterButton.RenderChromeElapsedTicks - pending.ButtonBefore.RenderChromeElapsedTicks):0.###} " +
                $"buttonTextPrepMs={TicksToMilliseconds(afterButton.RenderTextPreparationElapsedTicks - pending.ButtonBefore.RenderTextPreparationElapsedTicks):0.###} " +
                $"buttonTextDrawMs={TicksToMilliseconds(afterButton.RenderTextDrawDispatchElapsedTicks - pending.ButtonBefore.RenderTextDrawDispatchElapsedTicks):0.###} " +
                $"buttonTextPrepCalls={afterButton.RenderTextPreparationCallCount - pending.ButtonBefore.RenderTextPreparationCallCount} " +
                $"refreshEditorUiMs={afterView.RefreshEditorUiStateTotalMilliseconds - pending.ViewBefore.RefreshEditorUiStateTotalMilliseconds:0.###} " +
                $"documentStatsMs={afterView.DocumentStatsTotalMilliseconds - pending.ViewBefore.DocumentStatsTotalMilliseconds:0.###} " +
                $"updateStatusLabelsMs={afterView.UpdateStatusLabelsTotalMilliseconds - pending.ViewBefore.UpdateStatusLabelsTotalMilliseconds:0.###} " +
                $"editorEditMs={afterEditor.LastEditMilliseconds:0.###} " +
                $"editorRenderMs={afterEditor.LastRenderMilliseconds:0.###} " +
                $"editorRenderLayoutResolveMs={afterEditor.LastRenderLayoutResolveMilliseconds:0.###} " +
                $"editorRenderSelectionMs={afterEditor.LastRenderSelectionMilliseconds:0.###} " +
                $"editorRenderRunsMs={afterEditor.LastRenderRunsMilliseconds:0.###} " +
                $"editorRenderRunCount={afterEditor.LastRenderRunCount} " +
                $"editorRenderRunChars={afterEditor.LastRenderRunCharacterCount} " +
                $"editorRenderTableBordersMs={afterEditor.LastRenderTableBordersMilliseconds:0.###} " +
                $"editorRenderCaretMs={afterEditor.LastRenderCaretMilliseconds:0.###} " +
                $"editorRenderHostedLayoutMs={afterEditor.LastRenderHostedLayoutMilliseconds:0.###} " +
                $"editorRenderHostedDrawMs={afterEditor.LastRenderHostedChildrenDrawMilliseconds:0.###} " +
                $"editorRenderHostedDrawCount={afterEditor.LastRenderHostedChildrenDrawCount} " +
                $"editorLayoutMisses={afterEditor.LayoutCacheMissCount - pending.EditorBefore.LayoutCacheMissCount} " +
                $"editorLayoutBuilds={afterEditor.LayoutBuildSampleCount - pending.EditorBefore.LayoutBuildSampleCount} " +
                $"hottestTextDraw={afterText.HottestDrawStringText}|{afterText.HottestDrawStringTypography}:{afterText.HottestDrawStringMilliseconds:0.###} " +
                $"hottestUiElement={afterElementRender.HottestRenderSelfType}({afterElementRender.HottestRenderSelfName}):{afterElementRender.HottestRenderSelfMilliseconds:0.###} " +
                $"hottestLayoutMeasure={afterPerf.HottestLayoutMeasureElementType}({afterPerf.HottestLayoutMeasureElementName}):{afterPerf.HottestLayoutMeasureElementMilliseconds:0.###}");
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        private static string EscapeChar(char value)
        {
            return value switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                ' ' => "<space>",
                _ => value.ToString()
            };
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return (double)ticks * 1000d / Stopwatch.Frequency;
        }

        private readonly record struct PendingTypingSample(
            char Character,
            int Index,
            long TimestampTicks,
            int TextLengthBefore,
            int CaretIndexBefore,
            RichTextBoxPerformanceSnapshot EditorBefore,
            RichTextBoxViewDiagnosticsSnapshot ViewBefore,
            UiRootPerformanceTelemetrySnapshot RootPerfBefore,
            UiRenderTelemetrySnapshot RenderBefore,
            UiTextRendererTimingSnapshot TextBefore,
            UIElementRenderTimingSnapshot ElementRenderBefore,
            ButtonTimingSnapshot ButtonBefore,
            TextLayoutMetricsSnapshot TextLayoutBefore);
    }

    private sealed class ControlsCatalogSidebarScrollRuntimeDiagnosticsSession : IDisposable
    {
        private const string DefaultLogRelativePath = "artifacts/diagnostics/controls-catalog-sidebar-scroll-manual-runtime-hotspot.txt";
        private const int MaxLoggedFrames = 2400;
        private const double LoggedFrameFpsThreshold = 45d;
        private readonly string _logPath;
        private readonly StreamWriter _writer;
        private int _frameIndex;
        private int _loggedFrameCount;
        private long _lastFrameTimestamp;
        private PendingSidebarDrawSample? _pendingSample;

        private ControlsCatalogSidebarScrollRuntimeDiagnosticsSession(string logPath)
        {
            _logPath = Path.GetFullPath(logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            _writer = new StreamWriter(_logPath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            _writer.WriteLine("scenario=Controls Catalog sidebar scroll live runtime draw diagnostics");
            _writer.WriteLine($"timestamp_utc={DateTime.UtcNow:O}");
            _writer.WriteLine($"log_path={_logPath}");
            _writer.WriteLine("step_1=open the app");
            _writer.WriteLine("step_2=open Controls Catalog");
            _writer.WriteLine("step_3=hover the sidebar vertical scrollbar");
            _writer.WriteLine("step_4=scroll in the sidebar with and without ProgressBar view open");
            _writer.WriteLine("step_5=close the app and inspect this log");
            _writer.WriteLine($"logging_filter=only frames with fps < {LoggedFrameFpsThreshold:0.###}");
            _writer.WriteLine();
            _writer.WriteLine("frames:");
        }

        public static ControlsCatalogSidebarScrollRuntimeDiagnosticsSession CreateDefault()
        {
            return new ControlsCatalogSidebarScrollRuntimeDiagnosticsSession(
                Path.Combine(Environment.CurrentDirectory, DefaultLogRelativePath));
        }

        public void TryCaptureBeforeDraw(GameTime gameTime, UiRoot uiRoot, ControlsCatalogView? catalogView)
        {
            if (_frameIndex >= MaxLoggedFrames ||
                catalogView == null)
            {
                _pendingSample = null;
                return;
            }

            var hovered = uiRoot.GetHoveredElementForDiagnostics();
            var pointer = uiRoot.GetLastPointerPositionForDiagnostics();
            var hoveredPath = BuildTypePath(hovered);
            var selectedControl = catalogView.SelectedControlName;
            var sidebarScrollViewer = FindSidebarScrollViewer(catalogView);
            var hoveredScrollBar = FindAncestorOrSelf<ScrollBar>(hovered);
            var isHoveredSidebarVerticalScrollBar =
                hoveredScrollBar != null &&
                hoveredScrollBar.Orientation == Orientation.Vertical &&
                sidebarScrollViewer != null &&
                IsDescendantOf(hoveredScrollBar, sidebarScrollViewer);

            _pendingSample = new PendingSidebarDrawSample(
                _frameIndex++,
                gameTime.ElapsedGameTime.TotalMilliseconds,
                selectedControl,
                hovered?.GetType().Name ?? "null",
                hoveredPath,
                pointer,
                isHoveredSidebarVerticalScrollBar,
                uiRoot.GetPerformanceTelemetrySnapshotForTests(),
                uiRoot.GetRenderTelemetrySnapshotForTests(),
                UiTextRenderer.GetTimingSnapshotForTests(),
                UIElement.GetRenderTimingSnapshotForTests(),
                Button.GetTimingSnapshotForTests(),
                TextLayout.GetMetricsSnapshot());
        }

        public void TryCaptureAfterDraw(GameTime gameTime, UiRoot uiRoot, ControlsCatalogView? catalogView)
        {
            if (_pendingSample == null || catalogView == null)
            {
                ScrollViewer.GetScrollMetricsAndReset();
                return;
            }

            var pending = _pendingSample.Value;
            _pendingSample = null;

            var scroll = ScrollViewer.GetScrollMetricsAndReset();
            var now = Stopwatch.GetTimestamp();
            var hasPreviousFrame = _lastFrameTimestamp != 0;
            var fps = !hasPreviousFrame
                ? 0d
                : (double)Stopwatch.Frequency / Math.Max(1L, now - _lastFrameTimestamp);
            _lastFrameTimestamp = now;

            if (!hasPreviousFrame || fps >= LoggedFrameFpsThreshold)
            {
                return;
            }

            var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var render = uiRoot.GetRenderTelemetrySnapshotForTests();
            var text = UiTextRenderer.GetTimingSnapshotForTests();
            var elementRender = UIElement.GetRenderTimingSnapshotForTests();
            var button = Button.GetTimingSnapshotForTests();
            var textLayout = TextLayout.GetMetricsSnapshot();
            var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var freezableBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
            var dirtyQueueSummary = uiRoot.GetDirtyRenderQueueSummaryForTests();
            var syncedDirtyRootsSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();
            var dirtyRegionSummary = uiRoot.GetDirtyRegionSummaryForTests();
            var textDrawMs = TicksToMilliseconds(text.DrawStringElapsedTicks - pending.TextBefore.DrawStringElapsedTicks);
            var textMeasureMs = TicksToMilliseconds(text.MeasureWidthElapsedTicks - pending.TextBefore.MeasureWidthElapsedTicks);
            var lineHeightMs = TicksToMilliseconds(text.GetLineHeightElapsedTicks - pending.TextBefore.GetLineHeightElapsedTicks);
            var textLayoutMs = TicksToMilliseconds(textLayout.LayoutElapsedTicks - pending.TextLayoutBefore.LayoutElapsedTicks);
            var textLayoutBuildMs = TicksToMilliseconds(textLayout.BuildElapsedTicks - pending.TextLayoutBefore.BuildElapsedTicks);
            var uiElementRenderMs = TicksToMilliseconds(elementRender.RenderSelfElapsedTicks - pending.ElementRenderBefore.RenderSelfElapsedTicks);
            var buttonRenderMs = TicksToMilliseconds(button.RenderElapsedTicks - pending.ButtonBefore.RenderElapsedTicks);
            var buttonChromeMs = TicksToMilliseconds(button.RenderChromeElapsedTicks - pending.ButtonBefore.RenderChromeElapsedTicks);
            var buttonTextPrepMs = TicksToMilliseconds(button.RenderTextPreparationElapsedTicks - pending.ButtonBefore.RenderTextPreparationElapsedTicks);
            var buttonTextDrawMs = TicksToMilliseconds(button.RenderTextDrawDispatchElapsedTicks - pending.ButtonBefore.RenderTextDrawDispatchElapsedTicks);
            var drawAccountedMs =
                render.DrawClearMilliseconds +
                render.DrawInitialBatchBeginMilliseconds +
                render.DrawVisualTreeMilliseconds +
                render.DrawCursorMilliseconds +
                render.SpriteBatchRestartMilliseconds +
                render.DrawFinalBatchEndMilliseconds +
                render.DrawCleanupMilliseconds;
            var drawUnaccountedMs = Math.Max(0d, uiRoot.LastDrawMs - drawAccountedMs);

            var previewRoot = catalogView.FindName("PreviewHost") as ContentControl;
            var previewType = (previewRoot?.Content as UIElement)?.GetType().Name ?? "null";

            _loggedFrameCount++;
            _writer.WriteLine(
                $"frame={pending.FrameIndex:0000} selected={pending.SelectedControlName} preview={previewType} fps={fps:0.###} drawMs={uiRoot.LastDrawMs:0.###} updateMs={uiRoot.LastUpdateMs:0.###} gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:0.###} " +
                $"pointer=({pending.PointerPosition.X:0.#},{pending.PointerPosition.Y:0.#}) hovered={pending.HoveredType} hoveredPath={pending.HoveredPath} hoveredSidebarVBar={pending.HoveredSidebarVerticalScrollBar} " +
                $"wheelEvents={scroll.WheelEvents} wheelHandled={scroll.WheelHandled} setOffsetCalls={scroll.SetOffsetCalls} setOffsetNoOps={scroll.SetOffsetNoOpCalls} verticalDelta={scroll.TotalVerticalDelta:0.###} " +
                $"inputMs={perf.InputPhaseMilliseconds:0.###} layoutMs={perf.LayoutPhaseMilliseconds:0.###} animationMs={perf.AnimationPhaseMilliseconds:0.###} renderScheduleMs={perf.RenderSchedulingPhaseMilliseconds:0.###} frameParticipants={perf.FrameUpdateParticipantCount} frameParticipantMs={perf.FrameUpdateParticipantUpdateMilliseconds:0.###} hottestParticipant={perf.HottestFrameUpdateParticipantType}:{perf.HottestFrameUpdateParticipantMilliseconds:0.###} " +
                $"dirtyRoots={render.DirtyRootCount} dirtyFallbacks={render.DirtyRegionThresholdFallbackCount} spriteBatchRestarts={render.SpriteBatchRestartCount} spriteBatchRestartMs={render.SpriteBatchRestartMilliseconds:0.###} clipPushes={render.ClipPushCount} retainedVisited={render.RetainedNodesVisited} retainedDrawn={render.RetainedNodesDrawn} retainedTraversals={render.RetainedTraversalCount} dirtyTraversals={render.DirtyRegionTraversalCount} " +
                $"dirtyQueue={dirtyQueueSummary} syncedDirtyRoots={syncedDirtyRootsSummary} dirtyRegions={dirtyRegionSummary} freezableFlushTargets={freezableBatch.LastFlushTargetSummary} " +
                $"drawClearMs={render.DrawClearMilliseconds:0.###} drawBeginMs={render.DrawInitialBatchBeginMilliseconds:0.###} drawVisualTreeMs={render.DrawVisualTreeMilliseconds:0.###} drawCursorMs={render.DrawCursorMilliseconds:0.###} drawEndMs={render.DrawFinalBatchEndMilliseconds:0.###} drawCleanupMs={render.DrawCleanupMilliseconds:0.###} drawAccountedMs={drawAccountedMs:0.###} drawUnaccountedMs={drawUnaccountedMs:0.###} " +
                $"effectiveRenderSource={invalidation.EffectiveSourceType}#{invalidation.EffectiveSourceName} dirtyBoundsVisual={invalidation.DirtyBoundsVisualType}#{invalidation.DirtyBoundsVisualName} dirtyBoundsHint={invalidation.DirtyBoundsUsedHint} " +
                $"textDrawMs={textDrawMs:0.###} textDrawCalls={text.DrawStringCallCount - pending.TextBefore.DrawStringCallCount} textMeasureMs={textMeasureMs:0.###} textMeasureCalls={text.MeasureWidthCallCount - pending.TextBefore.MeasureWidthCallCount} lineHeightMs={lineHeightMs:0.###} lineHeightCalls={text.GetLineHeightCallCount - pending.TextBefore.GetLineHeightCallCount} " +
                $"textLayoutMs={textLayoutMs:0.###} textLayoutBuildMs={textLayoutBuildMs:0.###} textLayoutBuilds={textLayout.BuildCount - pending.TextLayoutBefore.BuildCount} textLayoutMisses={textLayout.CacheMissCount - pending.TextLayoutBefore.CacheMissCount} " +
                $"uiElementRenderMs={uiElementRenderMs:0.###} uiElementRenderCalls={elementRender.RenderSelfCallCount - pending.ElementRenderBefore.RenderSelfCallCount} hottestUiElement={elementRender.HottestRenderSelfType}({elementRender.HottestRenderSelfName}):{elementRender.HottestRenderSelfMilliseconds:0.###} hottestUiElementTypes={elementRender.HottestRenderSelfTypeSummary} " +
                $"buttonRenderMs={buttonRenderMs:0.###} buttonChromeMs={buttonChromeMs:0.###} buttonTextPrepMs={buttonTextPrepMs:0.###} buttonTextDrawMs={buttonTextDrawMs:0.###} buttonTextPrepCalls={button.RenderTextPreparationCallCount - pending.ButtonBefore.RenderTextPreparationCallCount} buttonTextDrawCalls={button.RenderTextDrawDispatchCallCount - pending.ButtonBefore.RenderTextDrawDispatchCallCount} " +
                $"textHotDraw={text.HottestDrawStringText}|{text.HottestDrawStringTypography}:{text.HottestDrawStringMilliseconds:0.###} textHotMeasure={text.HottestMeasureWidthText}|{text.HottestMeasureWidthTypography}:{text.HottestMeasureWidthMilliseconds:0.###}");
        }

        public void Dispose()
        {
            _writer.WriteLine();
            _writer.WriteLine($"logged_low_fps_frames={_loggedFrameCount}");
            _writer.Dispose();
        }

        private readonly record struct PendingSidebarDrawSample(
            int FrameIndex,
            double GameElapsedMilliseconds,
            string SelectedControlName,
            string HoveredType,
            string HoveredPath,
            Vector2 PointerPosition,
            bool HoveredSidebarVerticalScrollBar,
            UiRootPerformanceTelemetrySnapshot PerfBefore,
            UiRenderTelemetrySnapshot RenderBefore,
            UiTextRendererTimingSnapshot TextBefore,
            UIElementRenderTimingSnapshot ElementRenderBefore,
            ButtonTimingSnapshot ButtonBefore,
            TextLayoutMetricsSnapshot TextLayoutBefore);
    }

    private static ScrollViewer? FindSidebarScrollViewer(ControlsCatalogView catalog)
    {
        return FindFirstVisualChild<ScrollViewer>(catalog, viewer =>
            viewer.Content is StackPanel host &&
            string.Equals(host.Name, "ControlButtonsHost", StringComparison.Ordinal));
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool> predicate)
        where TElement : UIElement
    {
        if (root is TElement match && predicate(match))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
