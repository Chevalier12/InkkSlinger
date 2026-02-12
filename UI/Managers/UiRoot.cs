using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class UiRoot
{
    private static readonly RasterizerState UiRasterizerState = new()
    {
        ScissorTestEnable = true
    };

    private readonly LayoutManager _layoutManager;
    private readonly AnimationManager _animationManager;
    private readonly DirtyRegionTracker _dirtyRegions = new(32);
    private bool _isVisualDirty = true;
    private bool _isLayoutDirty = true;
    private int _drawSkippedFrameCount;
    private int _drawExecutedFrameCount;
    private int _layoutExecutedFrameCount;
    private int _layoutSkippedFrameCount;
    private bool _mustDrawNextFrame = true;
    private bool _viewportResizedSinceLastDraw;
    private UiRedrawReason _pendingInvalidationReasons = UiRedrawReason.ExplicitFullInvalidation;
    private bool _hasCompletedInitialLayout;
    private Vector2 _lastLayoutViewportSize = new(float.NaN, float.NaN);
    private RenderTarget2D? _cachedUiFrame;
    private Texture2D? _clearTexture;
    private int _cachedUiFrameWidth;
    private int _cachedUiFrameHeight;
    private bool _hasCachedUiFrame;

    public UiRoot(FrameworkElement rootElement)
    {
        Dispatcher.InitializeForCurrentThread();
        Current = this;
        RootElement = rootElement;
        _layoutManager = new LayoutManager(rootElement);
        _animationManager = AnimationManager.Current;

        RootElement.RaiseInitialized();
        RootElement.RaiseLoaded();
    }

    public FrameworkElement RootElement { get; }
    public static UiRoot? Current { get; private set; }
    public UiRootUpdateTiming LastUpdateTiming { get; private set; }
    public UiRootDrawTiming LastDrawTiming { get; private set; }
    public bool LastLayoutPassExecuted { get; private set; }
    public UiRedrawReason LastDrawReasons { get; private set; }
    public UiRedrawReason LastForceRedrawReasons { get; private set; }
    public UiRedrawScope LastForceRedrawScope { get; private set; }
    public int DrawSkippedFrameCount => _drawSkippedFrameCount;
    public int DrawExecutedFrameCount => _drawExecutedFrameCount;
    public bool IsVisualDirty => _isVisualDirty;
    public bool IsLayoutDirty => _isLayoutDirty;
    public bool IsFullFrameVisualDirty => _dirtyRegions.IsFullFrameDirty;
    public int DirtyVisualRegionCount => _dirtyRegions.RegionCount;

    internal void MarkVisualDirty(UiRedrawReason reason = UiRedrawReason.ExplicitFullInvalidation)
    {
        _isVisualDirty = true;
        _pendingInvalidationReasons |= reason;
        _dirtyRegions.MarkFullFrameDirty();
    }

    internal void MarkVisualDirty(LayoutRect bounds, UiRedrawReason reason = UiRedrawReason.None)
    {
        _isVisualDirty = true;
        _pendingInvalidationReasons |= reason;
        _dirtyRegions.AddDirtyRegion(bounds);
    }

    internal void MarkLayoutDirty()
    {
        _isLayoutDirty = true;
    }

    internal void MarkAllDirty()
    {
        _isLayoutDirty = true;
        MarkVisualDirty(UiRedrawReason.ExplicitFullInvalidation);
    }

    public void Update(GameTime gameTime, Vector2 viewportSize)
    {
        var updateStartTicks = Stopwatch.GetTimestamp();

        var inputStartTicks = Stopwatch.GetTimestamp();
        InputManager.Update(RootElement, gameTime);
        var inputTicks = Stopwatch.GetTimestamp() - inputStartTicks;

        var animationStartTicks = Stopwatch.GetTimestamp();
        _animationManager.Update(gameTime);
        var animationTicks = Stopwatch.GetTimestamp() - animationStartTicks;

        var layoutTicks = 0L;
        if (ShouldRunLayoutThisFrame(viewportSize))
        {
            LastLayoutPassExecuted = true;
            _layoutExecutedFrameCount++;
            var layoutStartTicks = Stopwatch.GetTimestamp();
            _layoutManager.UpdateLayout(viewportSize);
            _isLayoutDirty = false;
            _hasCompletedInitialLayout = true;
            _lastLayoutViewportSize = viewportSize;
            layoutTicks = Stopwatch.GetTimestamp() - layoutStartTicks;
        }
        else
        {
            LastLayoutPassExecuted = false;
            _layoutSkippedFrameCount++;
        }

        var elementUpdateStartTicks = Stopwatch.GetTimestamp();
        RootElement.Update(gameTime);
        var elementUpdateTicks = Stopwatch.GetTimestamp() - elementUpdateStartTicks;

        var totalTicks = Stopwatch.GetTimestamp() - updateStartTicks;
        LastUpdateTiming = new UiRootUpdateTiming(
            TicksToMilliseconds(inputTicks),
            TicksToMilliseconds(animationTicks),
            TicksToMilliseconds(layoutTicks),
            TicksToMilliseconds(elementUpdateTicks),
            TicksToMilliseconds(totalTicks),
            _layoutExecutedFrameCount,
            _layoutSkippedFrameCount,
            ComputeLayoutSkipRatio());
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var drawStartTicks = Stopwatch.GetTimestamp();
        EnsureCachedUiFrame(spriteBatch.GraphicsDevice);

        var renderPassStats = RenderPassStats.Empty;
        var drew = ExecuteDrawPassIfNeeded(() =>
        {
            renderPassStats = RenderUiTreeToCache(spriteBatch);
        });

        CompositeCachedUiFrame(spriteBatch);

        var drawReasons = drew ? LastDrawReasons : UiRedrawReason.None;
        var totalTicks = Stopwatch.GetTimestamp() - drawStartTicks;
        LastDrawTiming = new UiRootDrawTiming(
            TicksToMilliseconds(renderPassStats.ResetTicks),
            TicksToMilliseconds(renderPassStats.ElementDrawTicks),
            TicksToMilliseconds(totalTicks),
            _drawExecutedFrameCount,
            _drawSkippedFrameCount,
            ComputeDrawSkipRatio(),
            renderPassStats.DirtyRectCount,
            renderPassStats.DirtyPixelArea,
            renderPassStats.DirtyViewportCoverage,
            _dirtyRegions.FullFrameFallbackCount,
            drawReasons);
    }

    internal bool ExecuteDrawPassForTesting()
    {
        return ExecuteDrawPassIfNeeded(static () => { });
    }

    internal void MarkViewportResizedForTesting()
    {
        _viewportResizedSinceLastDraw = true;
    }

    public void Shutdown()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        _cachedUiFrame?.Dispose();
        _cachedUiFrame = null;
        _clearTexture?.Dispose();
        _clearTexture = null;
        _hasCachedUiFrame = false;

        RootElement.RaiseUnloaded();
    }

    private static double TicksToMilliseconds(long ticks)
    {
        if (ticks <= 0)
        {
            return 0d;
        }

        return ticks * 1000d / Stopwatch.Frequency;
    }

    private double ComputeDrawSkipRatio()
    {
        var total = _drawExecutedFrameCount + _drawSkippedFrameCount;
        if (total <= 0)
        {
            return 0d;
        }

        return (double)_drawSkippedFrameCount / total;
    }

    private double ComputeLayoutSkipRatio()
    {
        var total = _layoutExecutedFrameCount + _layoutSkippedFrameCount;
        if (total <= 0)
        {
            return 0d;
        }

        return (double)_layoutSkippedFrameCount / total;
    }

    private bool ShouldRunLayoutThisFrame(Vector2 viewportSize)
    {
        if (!_hasCompletedInitialLayout)
        {
            return true;
        }

        if (_isLayoutDirty)
        {
            return true;
        }

        return _lastLayoutViewportSize != viewportSize;
    }

    private bool ExecuteDrawPassIfNeeded(System.Action drawAction)
    {
        var forceDecision = ResolveForceRedrawDecision();
        LastForceRedrawReasons = forceDecision.Reasons;
        LastForceRedrawScope = forceDecision.Scope;

        if (!_isVisualDirty && !forceDecision.ShouldRedraw)
        {
            _drawSkippedFrameCount++;
            LastDrawReasons = UiRedrawReason.None;
            return false;
        }

        drawAction();
        LastDrawReasons = _pendingInvalidationReasons | forceDecision.Reasons;
        _isVisualDirty = false;
        _dirtyRegions.Clear();
        _pendingInvalidationReasons = UiRedrawReason.None;
        _mustDrawNextFrame = false;
        _viewportResizedSinceLastDraw = false;
        _drawExecutedFrameCount++;
        return true;
    }

    private void EnsureCachedUiFrame(GraphicsDevice graphicsDevice)
    {
        var width = Math.Max(1, graphicsDevice.Viewport.Width);
        var height = Math.Max(1, graphicsDevice.Viewport.Height);
        if (_cachedUiFrame != null &&
            _cachedUiFrameWidth == width &&
            _cachedUiFrameHeight == height)
        {
            return;
        }

        _cachedUiFrame?.Dispose();
        _cachedUiFrame = new RenderTarget2D(
            graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);

        _clearTexture ??= CreateClearTexture(graphicsDevice);

        _cachedUiFrameWidth = width;
        _cachedUiFrameHeight = height;
        _dirtyRegions.SetViewport(new LayoutRect(0f, 0f, width, height));
        _hasCachedUiFrame = false;
        _viewportResizedSinceLastDraw = true;
        MarkAllDirty();
        _mustDrawNextFrame = true;
    }

    private RenderPassStats RenderUiTreeToCache(SpriteBatch spriteBatch)
    {
        if (_cachedUiFrame == null)
        {
            return RenderPassStats.Empty;
        }

        if (_hasCachedUiFrame &&
            !_dirtyRegions.IsFullFrameDirty &&
            _dirtyRegions.RegionCount > 0)
        {
            return RenderDirtyRegionsToCache(spriteBatch);
        }

        return RenderFullUiTreeToCache(spriteBatch);
    }

    private RenderPassStats RenderFullUiTreeToCache(SpriteBatch spriteBatch)
    {
        if (_cachedUiFrame == null)
        {
            return RenderPassStats.Empty;
        }

        var graphicsDevice = spriteBatch.GraphicsDevice;
        var resetTicks = 0L;
        var elementDrawTicks = 0L;
        graphicsDevice.SetRenderTarget(_cachedUiFrame);
        try
        {
            graphicsDevice.Clear(Color.Transparent);
            RenderRootWithOptionalClip(spriteBatch, clipRegion: null, out resetTicks, out elementDrawTicks);
        }
        finally
        {
            graphicsDevice.SetRenderTarget(null);
        }

        _hasCachedUiFrame = true;
        var viewportArea = Math.Max(1d, _cachedUiFrameWidth * _cachedUiFrameHeight);
        return new RenderPassStats(
            resetTicks,
            elementDrawTicks,
            1,
            viewportArea,
            1d);
    }

    private RenderPassStats RenderDirtyRegionsToCache(SpriteBatch spriteBatch)
    {
        if (_cachedUiFrame == null || _clearTexture == null)
        {
            return RenderPassStats.Empty;
        }

        var graphicsDevice = spriteBatch.GraphicsDevice;
        long resetTicksTotal = 0L;
        long elementDrawTicksTotal = 0L;
        var dirtyRectCount = 0;
        double dirtyArea = 0d;

        graphicsDevice.SetRenderTarget(_cachedUiFrame);
        try
        {
            var regions = _dirtyRegions.Regions;
            for (var i = 0; i < regions.Count; i++)
            {
                var regionRect = ToRectangle(regions[i]);
                if (regionRect.Width <= 0 || regionRect.Height <= 0)
                {
                    continue;
                }

                dirtyRectCount++;
                dirtyArea += regionRect.Width * (double)regionRect.Height;
                ClearRegion(spriteBatch, regionRect);
                RenderRootWithOptionalClip(
                    spriteBatch,
                    regions[i],
                    out var resetTicks,
                    out var elementDrawTicks);

                resetTicksTotal += resetTicks;
                elementDrawTicksTotal += elementDrawTicks;
            }
        }
        finally
        {
            graphicsDevice.SetRenderTarget(null);
        }

        _hasCachedUiFrame = true;
        var viewportArea = Math.Max(1d, _cachedUiFrameWidth * _cachedUiFrameHeight);
        var coverage = Math.Clamp(dirtyArea / viewportArea, 0d, 1d);
        return new RenderPassStats(
            resetTicksTotal,
            elementDrawTicksTotal,
            dirtyRectCount,
            dirtyArea,
            coverage);
    }

    private void RenderRootWithOptionalClip(
        SpriteBatch spriteBatch,
        LayoutRect? clipRegion,
        out long resetTicks,
        out long elementDrawTicks)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Immediate,
            rasterizerState: UiRasterizerState);

        try
        {
            var resetStartTicks = Stopwatch.GetTimestamp();
            UiDrawing.ResetState(graphicsDevice);
            resetTicks = Stopwatch.GetTimestamp() - resetStartTicks;

            var shouldPopClip = clipRegion.HasValue;
            if (shouldPopClip)
            {
                UiDrawing.PushClip(spriteBatch, clipRegion!.Value);
            }

            try
            {
                var elementDrawStartTicks = Stopwatch.GetTimestamp();
                RootElement.Draw(spriteBatch);
                elementDrawTicks = Stopwatch.GetTimestamp() - elementDrawStartTicks;
            }
            finally
            {
                if (shouldPopClip)
                {
                    UiDrawing.PopClip(spriteBatch);
                }
            }
        }
        finally
        {
            spriteBatch.End();
        }
    }

    private void ClearRegion(SpriteBatch spriteBatch, Rectangle regionRect)
    {
        if (_clearTexture == null)
        {
            return;
        }

        var graphicsDevice = spriteBatch.GraphicsDevice;
        var previousScissor = graphicsDevice.ScissorRectangle;
        graphicsDevice.ScissorRectangle = regionRect;
        try
        {
            spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.Opaque,
                samplerState: SamplerState.PointClamp,
                rasterizerState: UiRasterizerState);
            try
            {
                spriteBatch.Draw(_clearTexture, regionRect, Color.Transparent);
            }
            finally
            {
                spriteBatch.End();
            }
        }
        finally
        {
            graphicsDevice.ScissorRectangle = previousScissor;
        }
    }

    private static Texture2D CreateClearTexture(GraphicsDevice graphicsDevice)
    {
        var texture = new Texture2D(graphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    private static Rectangle ToRectangle(LayoutRect rect)
    {
        var x = (int)MathF.Round(rect.X);
        var y = (int)MathF.Round(rect.Y);
        var width = Math.Max(0, (int)MathF.Round(rect.Width));
        var height = Math.Max(0, (int)MathF.Round(rect.Height));
        return new Rectangle(x, y, width, height);
    }

    private ForceRedrawDecision ResolveForceRedrawDecision()
    {
        var reasons = UiRedrawReason.None;
        var scope = UiRedrawScope.None;

        if (_mustDrawNextFrame)
        {
            reasons |= UiRedrawReason.ExplicitFullInvalidation;
            scope = MaxScope(scope, UiRedrawScope.Full);
        }

        if (_viewportResizedSinceLastDraw)
        {
            reasons |= UiRedrawReason.ViewportResized;
            scope = MaxScope(scope, UiRedrawScope.Full);
        }

        if (_animationManager.HasRunningAnimations)
        {
            reasons |= UiRedrawReason.AnimationActive;
            scope = MaxScope(scope, UiRedrawScope.Full);
        }

        var inputFlags = InputManager.VisualStateChangeFlagsThisFrame;
        if ((inputFlags & InputManager.InputVisualStateChangeFlags.HoverChanged) != 0)
        {
            reasons |= UiRedrawReason.HoverChanged;
            scope = MaxScope(scope, UiRedrawScope.Region);
        }

        if ((inputFlags & InputManager.InputVisualStateChangeFlags.FocusChanged) != 0)
        {
            reasons |= UiRedrawReason.FocusChanged;
            scope = MaxScope(scope, UiRedrawScope.Region);
        }

        if ((inputFlags & InputManager.InputVisualStateChangeFlags.CursorChanged) != 0)
        {
            reasons |= UiRedrawReason.CursorChanged;
            scope = MaxScope(scope, UiRedrawScope.Region);
        }

        return new ForceRedrawDecision(
            reasons != UiRedrawReason.None,
            scope,
            reasons);
    }

    private static UiRedrawScope MaxScope(UiRedrawScope current, UiRedrawScope candidate)
    {
        return (UiRedrawScope)Math.Max((int)current, (int)candidate);
    }

    private readonly record struct ForceRedrawDecision(
        bool ShouldRedraw,
        UiRedrawScope Scope,
        UiRedrawReason Reasons);

    private readonly record struct RenderPassStats(
        long ResetTicks,
        long ElementDrawTicks,
        int DirtyRectCount,
        double DirtyPixelArea,
        double DirtyViewportCoverage)
    {
        public static RenderPassStats Empty => new(0L, 0L, 0, 0d, 0d);
    }

    private void CompositeCachedUiFrame(SpriteBatch spriteBatch)
    {
        if (_cachedUiFrame == null || !_hasCachedUiFrame)
        {
            return;
        }

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Immediate,
            samplerState: SamplerState.PointClamp);
        try
        {
            spriteBatch.Draw(
                _cachedUiFrame,
                new Rectangle(0, 0, _cachedUiFrameWidth, _cachedUiFrameHeight),
                Color.White);
        }
        finally
        {
            spriteBatch.End();
        }
    }
}

public readonly record struct UiRootUpdateTiming(
    double InputMilliseconds,
    double AnimationMilliseconds,
    double LayoutMilliseconds,
    double ElementUpdateMilliseconds,
    double TotalMilliseconds,
    int LayoutExecutedFrames,
    int LayoutSkippedFrames,
    double LayoutSkipRatio);

public readonly record struct UiRootDrawTiming(
    double ResetStateMilliseconds,
    double ElementDrawMilliseconds,
    double TotalMilliseconds,
    int DrawExecutedFrames,
    int DrawSkippedFrames,
    double DrawSkipRatio,
    int DirtyRectCount,
    double DirtyPixelArea,
    double DirtyViewportCoverage,
    int FullRedrawFallbackCount,
    UiRedrawReason DrawReasons);
