using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Game1 : Game
{
    private const int IdleThrottleSleepMilliseconds = 8;
    private const string BaseWindowTitle = "InkkSlinger Controls Catalog";
    private const int MaxCalendarHoverDiagnosticsFrames = 480;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
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

    public Game1()
    {
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
            UseConditionalDrawScheduling = true,
            UseSoftwareCursor = false
        };

        base.Initialize();
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

            GraphicsDevice.SetRenderTarget(_uiCompositeTarget);
            _uiRoot.Draw(_spriteBatch, gameTime);
            GraphicsDevice.SetRenderTarget(null);

            if (shouldCaptureCalendarHoverDiagnostics)
            {
                CaptureCalendarHoverDiagnosticsFrame(activeCalendar);
            }
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
        if (accumulatedElapsedSeconds < 1d)
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

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = null;
        UiDrawing.ReleaseDeviceResources(GraphicsDevice);
        _uiRoot.Shutdown();
        _window.Dispose();
        base.OnExiting(sender, args);
    }

    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
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
}
