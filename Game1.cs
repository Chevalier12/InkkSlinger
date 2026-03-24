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
        WriteCalendarHoverDiagnosticsLog();
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
        var hoveredPath = BuildTypePath(hovered);
        var animation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var input = _uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        var render = _uiRoot.GetRenderTelemetrySnapshotForTests();
        var performance = _uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var button = Button.GetTimingSnapshotForTests();
        var dayButton = CalendarDayButton.GetTimingSnapshotForTests();
        var effect = DropShadowEffect.GetTimingSnapshotForTests();
        var text = UiTextRenderer.GetTimingSnapshotForTests();
        var layout = TextLayout.GetMetricsSnapshot();

        _calendarHoverFrames.Add(new CalendarHoverRuntimeFrame(
            _calendarHoverFrames.Count,
            hovered?.GetType().Name ?? "null",
            hoveredDayButton?.DayText ?? string.Empty,
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
            TicksToMilliseconds(text.DrawStringElapsedTicks),
            text.DrawStringCallCount,
            TicksToMilliseconds(text.MeasureWidthElapsedTicks),
            text.MeasureWidthCallCount,
            TicksToMilliseconds(text.GetLineHeightElapsedTicks),
            text.GetLineHeightCallCount,
            text.MetricsCacheHitCount,
            text.MetricsCacheMissCount,
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

    private void WriteCalendarHoverDiagnosticsLog()
    {
        if (_calendarHoverFrames.Count == 0)
        {
            return;
        }

        var logPath = GetCalendarHoverDiagnosticsLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var hottestDraw = _calendarHoverFrames.OrderByDescending(static frame => frame.UiDrawMs).First();
        var hottestEffect = _calendarHoverFrames.OrderByDescending(static frame => frame.DropShadowRenderMs).First();
        var hottestAnimation = _calendarHoverFrames.OrderByDescending(static frame => frame.AnimationComposeMs).First();
        var lowestFps = _calendarHoverFrames
            .Where(static frame => frame.Fps > 0d)
            .OrderBy(static frame => frame.Fps)
            .FirstOrDefault();
        var averageFpsFrames = _calendarHoverFrames.Where(static frame => frame.Fps > 0d).ToList();
        var averageFps = averageFpsFrames.Count == 0
            ? 0d
            : averageFpsFrames.Average(static frame => frame.Fps);
        var minFps = lowestFps?.Fps ?? 0d;

        var lines = new List<string>
        {
            "scenario=ControlsCatalog Calendar manual hover runtime diagnostics",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={logPath}",
            "step_1=open Controls Catalog",
            "step_2=click Calendar button",
            "step_3=move pointer over CalendarDayButton cells and reproduce FPS drop",
            $"captured_frames={_calendarHoverFrames.Count}",
            $"hovered_calendar_day_frames={_calendarHoverFrames.Count(static frame => string.Equals(frame.HoveredType, nameof(CalendarDayButton), StringComparison.Ordinal))}",
            $"min_fps={minFps:0.###}",
            $"avg_fps={averageFps:0.###}",
            $"max_ui_update_ms={_calendarHoverFrames.Max(static frame => frame.UiUpdateMs):0.###}",
            $"max_ui_draw_ms={_calendarHoverFrames.Max(static frame => frame.UiDrawMs):0.###}",
            $"max_input_phase_ms={_calendarHoverFrames.Max(static frame => frame.InputPhaseMs):0.###}",
            $"max_binding_phase_ms={_calendarHoverFrames.Max(static frame => frame.BindingPhaseMs):0.###}",
            $"max_layout_phase_ms={_calendarHoverFrames.Max(static frame => frame.LayoutPhaseMs):0.###}",
            $"max_animation_phase_ms={_calendarHoverFrames.Max(static frame => frame.AnimationPhaseMs):0.###}",
            $"max_render_scheduling_phase_ms={_calendarHoverFrames.Max(static frame => frame.RenderSchedulingPhaseMs):0.###}",
            $"max_visual_update_ms={_calendarHoverFrames.Max(static frame => frame.VisualUpdateMs):0.###}",
            $"max_update_participant_refresh_ms={_calendarHoverFrames.Max(static frame => frame.UpdateParticipantRefreshMs):0.###}",
            $"max_update_participant_update_ms={_calendarHoverFrames.Max(static frame => frame.UpdateParticipantUpdateMs):0.###}",
            $"max_framework_measure_ms={_calendarHoverFrames.Max(static frame => frame.FrameworkMeasureMs):0.###}",
            $"max_framework_measure_exclusive_ms={_calendarHoverFrames.Max(static frame => frame.FrameworkMeasureExclusiveMs):0.###}",
            $"max_framework_arrange_ms={_calendarHoverFrames.Max(static frame => frame.FrameworkArrangeMs):0.###}",
            $"max_animation_compose_ms={_calendarHoverFrames.Max(static frame => frame.AnimationComposeMs):0.###}",
            $"max_animation_compose_apply_ms={_calendarHoverFrames.Max(static frame => frame.AnimationComposeApplyMs):0.###}",
            $"max_drop_shadow_render_ms={_calendarHoverFrames.Max(static frame => frame.DropShadowRenderMs):0.###}",
            $"max_drop_shadow_blur_path_ms={_calendarHoverFrames.Max(static frame => frame.DropShadowBlurPathMs):0.###}",
            $"max_calendar_day_render_ms={_calendarHoverFrames.Max(static frame => frame.CalendarDayRenderMs):0.###}",
            $"max_button_render_ms={_calendarHoverFrames.Max(static frame => frame.ButtonRenderMs):0.###}",
            $"max_text_draw_ms={_calendarHoverFrames.Max(static frame => frame.TextDrawMs):0.###}",
            $"max_text_measure_ms={_calendarHoverFrames.Max(static frame => frame.TextMeasureWidthMs):0.###}",
            $"hottest_draw_frame={FormatRuntimeFrame(hottestDraw)}",
            $"hottest_effect_frame={FormatRuntimeFrame(hottestEffect)}",
            $"hottest_animation_frame={FormatRuntimeFrame(hottestAnimation)}"
        };

        lines.Add(string.Empty);
        lines.Add("frames:");
        lines.AddRange(_calendarHoverFrames.Select(FormatRuntimeFrame));

        File.WriteAllLines(logPath, lines);
    }

    private static string FormatRuntimeFrame(CalendarHoverRuntimeFrame frame)
    {
        return
            $"frame={frame.FrameIndex:000} hovered={frame.HoveredType} day={frame.HoveredDayText} hoveredPath={frame.HoveredPath} fps={frame.Fps:0.###} " +
            $"uiUpdateMs={frame.UiUpdateMs:0.###} uiDrawMs={frame.UiDrawMs:0.###} inputPhaseMs={frame.InputPhaseMs:0.###} bindingPhaseMs={frame.BindingPhaseMs:0.###} layoutPhaseMs={frame.LayoutPhaseMs:0.###} animationPhaseMs={frame.AnimationPhaseMs:0.###} renderSchedulingPhaseMs={frame.RenderSchedulingPhaseMs:0.###} visualUpdateMs={frame.VisualUpdateMs:0.###} hoverMs={frame.HoverMs:0.###} resolveMs={frame.ResolveMs:0.###} routeMs={frame.RouteMs:0.###} " +
            $"updateParticipantRefreshMs={frame.UpdateParticipantRefreshMs:0.###} updateParticipantUpdateMs={frame.UpdateParticipantUpdateMs:0.###} hottestUpdateParticipant={frame.HottestUpdateParticipantType}:{frame.HottestUpdateParticipantMs:0.###} " +
            $"frameworkMeasureMs={frame.FrameworkMeasureMs:0.###} frameworkMeasureExclusiveMs={frame.FrameworkMeasureExclusiveMs:0.###} frameworkArrangeMs={frame.FrameworkArrangeMs:0.###} hottestMeasureElement={frame.HottestMeasureElementType}({frame.HottestMeasureElementName}):{frame.HottestMeasureElementMs:0.###} hottestArrangeElement={frame.HottestArrangeElementType}({frame.HottestArrangeElementName}):{frame.HottestArrangeElementMs:0.###} " +
            $"beginMs={frame.AnimationBeginMs:0.###} startMs={frame.AnimationStartMs:0.###} composeMs={frame.AnimationComposeMs:0.###} composeCollectMs={frame.AnimationComposeCollectMs:0.###} " +
            $"composeSortMs={frame.AnimationComposeSortMs:0.###} composeMergeMs={frame.AnimationComposeMergeMs:0.###} composeApplyMs={frame.AnimationComposeApplyMs:0.###} composeBatchEndMs={frame.AnimationComposeBatchEndMs:0.###} " +
            $"activeStoryboards={frame.ActiveStoryboards} activeLanes={frame.ActiveLanes} hottestSetValuePaths={frame.HottestSetValuePaths} " +
            $"buttonRenderMs={frame.ButtonRenderMs:0.###} buttonChromeMs={frame.ButtonChromeMs:0.###} buttonTextPrepMs={frame.ButtonTextPreparationMs:0.###} buttonTextDrawMs={frame.ButtonTextDrawMs:0.###} buttonTextPrepCalls={frame.ButtonTextPreparationCalls} " +
            $"calendarDayRenderMs={frame.CalendarDayRenderMs:0.###} calendarDayRenderCalls={frame.CalendarDayRenderCalls} calendarDayNonEmptyCalls={frame.CalendarDayNonEmptyRenderCalls} " +
            $"dropShadowRenderMs={frame.DropShadowRenderMs:0.###} dropShadowBlurPathMs={frame.DropShadowBlurPathMs:0.###} dropShadowBlurSlicesMs={frame.DropShadowBlurSlicesMs:0.###} " +
            $"dropShadowCalls={frame.DropShadowRenderCalls} dropShadowBlurCalls={frame.DropShadowBlurPathCalls} calendarDayShadowCalls={frame.CalendarDayShadowRenderCalls} calendarDayShadowBlurCalls={frame.CalendarDayShadowBlurPathCalls} " +
            $"textDrawMs={frame.TextDrawMs:0.###} textDrawCalls={frame.TextDrawCalls} textMeasureMs={frame.TextMeasureWidthMs:0.###} textMeasureCalls={frame.TextMeasureWidthCalls} lineHeightMs={frame.TextLineHeightMs:0.###} lineHeightCalls={frame.TextLineHeightCalls} textMetricsHits={frame.TextMetricsCacheHits} textMetricsMisses={frame.TextMetricsCacheMisses} " +
            $"layoutMs={frame.TextLayoutMs:0.###} layoutBuildMs={frame.TextLayoutBuildMs:0.###} layoutBuilds={frame.TextLayoutBuildCount} layoutMisses={frame.TextLayoutCacheMissCount} " +
            $"retainedVisited={frame.RetainedNodesVisited} retainedDrawn={frame.RetainedNodesDrawn} retainedTraversals={frame.RetainedTraversalCount} dirtyTraversals={frame.DirtyRegionTraversalCount} dayButtonCount={frame.CalendarDayButtonCount}";
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

    private static string GetCalendarHoverDiagnosticsLogPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", "controls-catalog-calendar-hover-runtime.txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record CalendarHoverRuntimeFrame(
        int FrameIndex,
        string HoveredType,
        string HoveredDayText,
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
        double TextDrawMs,
        int TextDrawCalls,
        double TextMeasureWidthMs,
        int TextMeasureWidthCalls,
        double TextLineHeightMs,
        int TextLineHeightCalls,
        int TextMetricsCacheHits,
        int TextMetricsCacheMisses,
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
