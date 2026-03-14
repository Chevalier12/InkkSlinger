using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public delegate void RenderSurfaceDrawEventHandler(SpriteBatch spriteBatch, Rectangle bounds);

public class RenderSurface : SurfacePresenterBase, IUiRootUpdateParticipant
{
    public static readonly DependencyProperty SurfaceProperty =
        DependencyProperty.Register(
            nameof(Surface),
            typeof(ImageSource),
            typeof(RenderSurface),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is RenderSurface renderSurface)
                    {
                        renderSurface.InvalidateResolvedSurfaceCache();
                    }
                }));

    private static readonly object DrawOverrideCacheLock = new();
    private static readonly Dictionary<Type, bool> DrawOverrideCache = new();
    private static IRenderSurfaceManagedBackend _managedBackend = new DefaultRenderSurfaceManagedBackend();

    private readonly bool _hasDrawSurfaceOverride;
    private RenderSurfaceDrawEventHandler? _drawSurface;
    private IRenderSurfaceManagedSession? _managedSession;
    private ImageSource? _managedSurface;
    private GraphicsDevice? _managedGraphicsDevice;
    private Point _managedPixelSize;
    private bool _managedSurfaceDirty;

    public RenderSurface()
    {
        _hasDrawSurfaceOverride = DetectDrawSurfaceOverride(GetType());
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ImageSource? Surface
    {
        get => GetValue<ImageSource>(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    public event RenderSurfaceDrawEventHandler? DrawSurface
    {
        add
        {
            var wasManagedModeActive = IsManagedModeActive;
            _drawSurface += value;
            HandleDrawSurfaceSubscriptionMutation(wasManagedModeActive);
        }
        remove
        {
            var wasManagedModeActive = IsManagedModeActive;
            _drawSurface -= value;
            HandleDrawSurfaceSubscriptionMutation(wasManagedModeActive);
        }
    }

    protected virtual bool IsFrameUpdateActive => false;

    protected override ImageSource? RequestedSurface => IsManagedModeActive ? _managedSurface : Surface;

    public void Present(ImageSource? surface)
    {
        if (ReferenceEquals(Surface, surface))
        {
            if (surface != null)
            {
                InvalidateVisual();
            }

            return;
        }

        Surface = surface;
    }

    public void Present(Texture2D surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var currentSurface = Surface;
        if (currentSurface?.Texture == surface &&
            currentSurface.PixelWidth == surface.Width &&
            currentSurface.PixelHeight == surface.Height)
        {
            InvalidateVisual();
            return;
        }

        Surface = ImageSource.FromTexture(surface);
    }

    public void RefreshSurface()
    {
        if (IsManagedModeActive || ResolveManualSurface() != null)
        {
            InvalidateVisual();
        }
    }

    public void ClearSurface()
    {
        Surface = null;
    }

    public override void InvalidateVisual()
    {
        Dispatcher.VerifyAccess();
        if (IsManagedModeActive)
        {
            _managedSurfaceDirty = true;
        }

        base.InvalidateVisual();
    }

    protected virtual void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
    {
    }

    protected virtual void OnFrameUpdate(GameTime gameTime)
    {
        _ = gameTime;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return IsManagedModeActive
            ? Vector2.Zero
            : base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (IsManagedModeActive)
        {
            UpdateManagedPixelSize(finalSize);
        }

        return base.ArrangeOverride(finalSize);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (IsManagedModeActive)
        {
            _ = EnsureManagedSurfaceRendered(spriteBatch);
        }

        base.OnRender(spriteBatch);
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => IsManagedModeActive && IsFrameUpdateActive;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        if (!IsManagedModeActive || !IsFrameUpdateActive)
        {
            return;
        }

        RecordUpdateCallFromUiRoot();
        OnFrameUpdate(gameTime);
    }

    internal static IRenderSurfaceManagedBackend ManagedBackend
    {
        get => _managedBackend;
        set => _managedBackend = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal bool IsManagedModeActiveForTests => IsManagedModeActive;

    internal ImageSource? GetDisplayedSurfaceForTests()
    {
        return RequestedSurface;
    }

    internal Point GetManagedPixelSizeForTests()
    {
        return _managedPixelSize;
    }

    internal bool HasManagedSessionForTests()
    {
        return _managedSession != null;
    }

    internal bool EnsureManagedSurfaceRenderedForTests(GraphicsDevice graphicsDevice)
    {
        return EnsureManagedSurfaceRendered(uiSpriteBatch: null, graphicsDeviceOverride: graphicsDevice);
    }

    private bool IsManagedModeActive => _drawSurface != null || _hasDrawSurfaceOverride;

    private void HandleDrawSurfaceSubscriptionMutation(bool wasManagedModeActive)
    {
        var isManagedModeActive = IsManagedModeActive;
        if (wasManagedModeActive != isManagedModeActive)
        {
            if (isManagedModeActive)
            {
                SyncManagedSurfacePresentation();
                _managedSurfaceDirty = true;
            }
            else
            {
                _managedSurfaceDirty = false;
                DisposeManagedGraphicsResources(updatePresentationSurface: false);
                if (_managedSurface != null)
                {
                    _managedSurface = null;
                    InvalidateResolvedSurfaceCache();
                }
            }

            InvalidateMeasure();
            return;
        }

        if (isManagedModeActive)
        {
            _managedSurfaceDirty = true;
            InvalidateVisual();
        }
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (IsManagedModeActive)
        {
            _managedSurfaceDirty = true;
            SyncManagedSurfacePresentation();
            InvalidateVisual();
        }
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        DisposeManagedGraphicsResources(updatePresentationSurface: true);
    }

    private void UpdateManagedPixelSize(Vector2 finalSize)
    {
        var nextPixelSize = new Point(
            Math.Max(0, (int)MathF.Round(finalSize.X)),
            Math.Max(0, (int)MathF.Round(finalSize.Y)));

        if (_managedPixelSize != nextPixelSize)
        {
            _managedPixelSize = nextPixelSize;
            DisposeManagedGraphicsResources(updatePresentationSurface: false);
            _managedSurfaceDirty = nextPixelSize.X > 0 && nextPixelSize.Y > 0;
        }

        SyncManagedSurfacePresentation();
    }

    private bool EnsureManagedSurfaceRendered(SpriteBatch? uiSpriteBatch, GraphicsDevice? graphicsDeviceOverride = null)
    {
        if (!IsManagedModeActive)
        {
            return false;
        }

        if (_managedPixelSize.X <= 0 || _managedPixelSize.Y <= 0)
        {
            DisposeManagedGraphicsResources(updatePresentationSurface: true);
            _managedSurfaceDirty = false;
            return false;
        }

        var graphicsDevice = graphicsDeviceOverride ?? uiSpriteBatch?.GraphicsDevice;
        if (graphicsDevice == null)
        {
            return false;
        }

        if (_managedSession == null ||
            _managedSession.IsDisposed ||
            !ReferenceEquals(_managedGraphicsDevice, graphicsDevice) ||
            _managedSession.PixelWidth != _managedPixelSize.X ||
            _managedSession.PixelHeight != _managedPixelSize.Y)
        {
            DisposeManagedGraphicsResources(updatePresentationSurface: false);
            _managedSession = ManagedBackend.Create(graphicsDevice, _managedPixelSize.X, _managedPixelSize.Y);
            _managedGraphicsDevice = graphicsDevice;
            _managedSurfaceDirty = true;
            SyncManagedSurfacePresentation();
        }

        if (!_managedSurfaceDirty)
        {
            return false;
        }

        _managedSession.Render(uiSpriteBatch, Color.Transparent, InvokeManagedDraw);
        _managedSurfaceDirty = false;
        SyncManagedSurfacePresentation();
        return true;
    }

    private void InvokeManagedDraw(SpriteBatch spriteBatch, Rectangle bounds)
    {
        OnDrawSurface(spriteBatch, bounds);
        _drawSurface?.Invoke(spriteBatch, bounds);
    }

    private void SyncManagedSurfacePresentation()
    {
        var nextSurface = ResolveManagedPresentationSurface();
        if (ReferenceEquals(_managedSurface, nextSurface))
        {
            return;
        }

        _managedSurface = nextSurface;
        InvalidateResolvedSurfaceCache();
    }

    private ImageSource? ResolveManagedPresentationSurface()
    {
        if (!IsManagedModeActive || _managedPixelSize.X <= 0 || _managedPixelSize.Y <= 0)
        {
            return null;
        }

        if (_managedSession != null && !_managedSession.IsDisposed)
        {
            return _managedSession.Surface;
        }

        if (_managedSurface != null &&
            _managedSurface.Texture == null &&
            _managedSurface.PixelWidth == _managedPixelSize.X &&
            _managedSurface.PixelHeight == _managedPixelSize.Y)
        {
            return _managedSurface;
        }

        return ImageSource.FromPixels(_managedPixelSize.X, _managedPixelSize.Y);
    }

    private ImageSource? ResolveManualSurface()
    {
        var manualSurface = Surface;
        if (manualSurface == null)
        {
            return null;
        }

        if (manualSurface.HasPixelSize)
        {
            return manualSurface;
        }

        return manualSurface.Resolve();
    }

    private void DisposeManagedGraphicsResources(bool updatePresentationSurface)
    {
        _managedSession?.Dispose();
        _managedSession = null;
        _managedGraphicsDevice = null;

        if (updatePresentationSurface)
        {
            SyncManagedSurfacePresentation();
        }
    }

    private static bool DetectDrawSurfaceOverride(Type type)
    {
        lock (DrawOverrideCacheLock)
        {
            if (DrawOverrideCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var method = type.GetMethod(
                nameof(OnDrawSurface),
                BindingFlags.Instance | BindingFlags.NonPublic);
            var hasOverride = method?.GetBaseDefinition().DeclaringType != method?.DeclaringType;
            DrawOverrideCache[type] = hasOverride;
            return hasOverride;
        }
    }
}
