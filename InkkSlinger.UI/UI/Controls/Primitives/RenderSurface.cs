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
    private readonly SubspaceViewport2DCollection _subspaceViewport2Ds;

    public RenderSurface()
    {
        _hasDrawSurfaceOverride = DetectDrawSurfaceOverride(GetType());
        _subspaceViewport2Ds = new SubspaceViewport2DCollection(this);
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

    public IList<SubspaceViewport2D> SubspaceViewport2Ds => _subspaceViewport2Ds;

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
            ArrangeSubspaceViewport2Ds(finalSize);
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

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => IsManagedModeActive && (IsFrameUpdateActive || _subspaceViewport2Ds.Count > 0);

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        if (!IsManagedModeActive || (!IsFrameUpdateActive && _subspaceViewport2Ds.Count == 0))
        {
            return;
        }

        RecordUpdateCallFromUiRoot();
        if (IsFrameUpdateActive)
        {
            OnFrameUpdate(gameTime);
        }

        UpdateSubspaceViewport2Ds(gameTime);
        RefreshManagedSurfaceWhenSubspaceViewport2DsAreDirty();
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

    internal bool IsManagedSurfaceDirtyForTests()
    {
        return _managedSurfaceDirty;
    }

    internal void ClearManagedSurfaceDirtyForTests()
    {
        _managedSurfaceDirty = false;
    }

    internal bool TryHitTestSubspaceViewport2Ds(Vector2 rootPointerPosition, out UIElement? target)
    {
        target = null;
        if (_subspaceViewport2Ds.Count == 0 || !HitTest(rootPointerPosition))
        {
            return false;
        }

        var localPointerPosition = new Vector2(
            rootPointerPosition.X - LayoutSlot.X,
            rootPointerPosition.Y - LayoutSlot.Y);

        for (var index = _subspaceViewport2Ds.Count - 1; index >= 0; index--)
        {
            if (_subspaceViewport2Ds[index].Content is not UIElement content)
            {
                continue;
            }

            var hit = VisualTreeHelper.HitTest(content, localPointerPosition);
            if (hit != null)
            {
                target = hit;
                return true;
            }
        }

        return false;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            if (_subspaceViewport2Ds[index].Content is UIElement content)
            {
                yield return content;
            }
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        var count = 0;
        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            if (_subspaceViewport2Ds[index].Content != null)
            {
                count++;
            }
        }

        return count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var visibleIndex = 0;
            for (var viewportIndex = 0; viewportIndex < _subspaceViewport2Ds.Count; viewportIndex++)
        {
                if (_subspaceViewport2Ds[viewportIndex].Content is not UIElement content)
            {
                continue;
            }

            if (visibleIndex == index)
            {
                return content;
            }

            visibleIndex++;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private bool IsManagedModeActive => _drawSurface != null || _hasDrawSurfaceOverride || _subspaceViewport2Ds.Count > 0;

    internal void AttachSubspaceViewport2D(SubspaceViewport2D viewport)
    {
        var wasManagedModeActive = IsManagedModeActive;
        if (viewport.Host != null && !ReferenceEquals(viewport.Host, this))
        {
            throw new InvalidOperationException("SubspaceViewport2D is already attached to another RenderSurface.");
        }

        viewport.Host = this;
        AttachSubspaceViewport2DContent(viewport.Content);
        HandleSubspaceViewport2DMutation(wasManagedModeActive);
    }

    internal void DetachSubspaceViewport2D(SubspaceViewport2D viewport)
    {
        var wasManagedModeActive = IsManagedModeActive;
        DetachSubspaceViewport2DContent(viewport.Content);
        if (ReferenceEquals(viewport.Host, this))
        {
            viewport.Host = null;
        }

        HandleSubspaceViewport2DMutation(wasManagedModeActive);
    }

    internal void OnSubspaceViewport2DContentChanged(SubspaceViewport2D viewport, UIElement? oldContent, UIElement? newContent)
    {
        if (!ReferenceEquals(viewport.Host, this))
        {
            return;
        }

        DetachSubspaceViewport2DContent(oldContent);
        AttachSubspaceViewport2DContent(newContent);
        InvalidateSubspaceViewport2DVisuals();
    }

    private void HandleSubspaceViewport2DMutation(bool wasManagedModeActive)
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

    private void AttachSubspaceViewport2DContent(UIElement? content)
    {
        if (content == null)
        {
            return;
        }

        content.SetVisualParent(this);
        content.SetLogicalParent(this);
    }

    private void DetachSubspaceViewport2DContent(UIElement? content)
    {
        if (content == null)
        {
            return;
        }

        if (ReferenceEquals(content.VisualParent, this))
        {
            content.SetVisualParent(null);
        }

        if (ReferenceEquals(content.LogicalParent, this))
        {
            content.SetLogicalParent(null);
        }
    }

    private void InvalidateSubspaceViewport2DVisuals()
    {
        if (!IsManagedModeActive)
        {
            return;
        }

        _managedSurfaceDirty = true;
        InvalidateVisual();
    }

    private void UpdateSubspaceViewport2Ds(GameTime gameTime)
    {
        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            _subspaceViewport2Ds[index].Content?.Update(gameTime);
        }
    }

    private void RefreshManagedSurfaceWhenSubspaceViewport2DsAreDirty()
    {
        if (_managedSurfaceDirty)
        {
            return;
        }

        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            if (!IsSubspaceViewport2DSubtreeDirty(_subspaceViewport2Ds[index].Content))
            {
                continue;
            }

            _managedSurfaceDirty = true;
            InvalidateVisual();
            return;
        }
    }

    private static bool IsSubspaceViewport2DSubtreeDirty(UIElement? element)
    {
        if (element == null)
        {
            return false;
        }

        if (element.NeedsMeasure || element.NeedsArrange || element.NeedsRender)
        {
            return true;
        }

        var childCount = element.GetVisualChildCountForTraversal();
        for (var index = 0; index < childCount; index++)
        {
            if (IsSubspaceViewport2DSubtreeDirty(element.GetVisualChildAtForTraversal(index)))
            {
                return true;
            }
        }

        return false;
    }

    private void ArrangeSubspaceViewport2Ds(Vector2 finalSize)
    {
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(0, (int)MathF.Round(finalSize.X)),
            Math.Max(0, (int)MathF.Round(finalSize.Y)));

        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            ArrangeSubspaceViewport2D(bounds, _subspaceViewport2Ds[index]);
        }
    }

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
        DrawSubspaceViewport2Ds(spriteBatch, bounds);
    }

    private void DrawSubspaceViewport2Ds(SpriteBatch spriteBatch, Rectangle bounds)
    {
        for (var index = 0; index < _subspaceViewport2Ds.Count; index++)
        {
            DrawSubspaceViewport2D(spriteBatch, bounds, _subspaceViewport2Ds[index]);
        }
    }

    private static void DrawSubspaceViewport2D(SpriteBatch spriteBatch, Rectangle bounds, SubspaceViewport2D viewport)
    {
        var content = viewport.Content;
        if (content == null)
        {
            return;
        }

        var clipRect = ArrangeSubspaceViewport2D(bounds, viewport);
        UiDrawing.PushLocalState(spriteBatch, hasTransform: false, localTransform: Matrix.Identity, hasClip: true, clipRect);
        try
        {
            content.Draw(spriteBatch);
        }
        finally
        {
            UiDrawing.PopLocalState(spriteBatch, hasTransform: false, hasClip: true);
        }
    }

    private static LayoutRect ArrangeSubspaceViewport2D(Rectangle bounds, SubspaceViewport2D viewport)
    {
        var content = viewport.Content;
        var availableWidth = Math.Max(0f, bounds.Width - viewport.X);
        var availableHeight = Math.Max(0f, bounds.Height - viewport.Y);
        var layoutWidth = float.IsNaN(viewport.Width) ? availableWidth : Math.Max(0f, viewport.Width);
        var layoutHeight = float.IsNaN(viewport.Height) ? availableHeight : Math.Max(0f, viewport.Height);

        if (content is FrameworkElement frameworkContent)
        {
            frameworkContent.Measure(new Vector2(layoutWidth, layoutHeight));

            if (float.IsNaN(viewport.Width))
            {
                layoutWidth = Math.Min(availableWidth, frameworkContent.DesiredSize.X);
            }

            if (float.IsNaN(viewport.Height))
            {
                layoutHeight = Math.Min(availableHeight, frameworkContent.DesiredSize.Y);
            }

            frameworkContent.Arrange(new LayoutRect(viewport.X, viewport.Y, layoutWidth, layoutHeight));
        }

        return new LayoutRect(viewport.X, viewport.Y, Math.Max(0f, layoutWidth), Math.Max(0f, layoutHeight));
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
