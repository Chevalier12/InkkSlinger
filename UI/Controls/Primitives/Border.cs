using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Border : Decorator
{
    private static readonly Dictionary<GraphicsDevice, Dictionary<RoundedTextureCacheKey, Texture2D>> RoundedTextureCaches = new();
    private BorderRenderState _renderStateCache;
    private bool _hasRenderStateCache;
    private RoundedRectRadii _outerRadiiCache;
    private float _outerRadiiCacheWidth = float.NaN;
    private float _outerRadiiCacheHeight = float.NaN;
    private bool _hasOuterRadiiCache;
    private RoundedGeometryCacheKey _roundedGeometryCacheKey;
    private bool _hasRoundedGeometryCache;
    private Vector2[] _roundedFillPolygon = Array.Empty<Vector2>();
    private int _roundedFillPolygonPointCount;
    private Vector2[] _topLeftCornerBorderPolygon = Array.Empty<Vector2>();
    private int _topLeftCornerBorderPolygonPointCount;
    private Vector2[] _topRightCornerBorderPolygon = Array.Empty<Vector2>();
    private int _topRightCornerBorderPolygonPointCount;
    private Vector2[] _bottomRightCornerBorderPolygon = Array.Empty<Vector2>();
    private int _bottomRightCornerBorderPolygonPointCount;
    private Vector2[] _bottomLeftCornerBorderPolygon = Array.Empty<Vector2>();
    private int _bottomLeftCornerBorderPolygonPointCount;
    private static int _roundedGeometryCacheBuildCount;
    private static int _renderStateCacheBuildCount;

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Brush),
            typeof(Border),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                static (dependencyObject, args) =>
                {
                    if (dependencyObject is Border border)
                    {
                        border.OnBackgroundBrushChanged(args);
                    }
                }));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Brush),
            typeof(Border),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                static (dependencyObject, args) =>
                {
                    if (dependencyObject is Border border)
                    {
                        border.OnBorderBrushChanged(args);
                    }
                }));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(Border),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Border),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(Border),
            new FrameworkPropertyMetadata(
                CornerRadius.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) =>
                {
                    return value is CornerRadius cornerRadius
                        ? cornerRadius.ClampNonNegative()
                        : CornerRadius.Empty;
                }));

    public Brush? Background
    {
        get => GetValue<Brush>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush? BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    internal static int GetRoundedGeometryCacheBuildCountForTests()
    {
        return _roundedGeometryCacheBuildCount;
    }

    internal static void ResetRoundedGeometryCacheBuildCountForTests()
    {
        _roundedGeometryCacheBuildCount = 0;
    }

    internal static int GetRenderStateCacheBuildCountForTests()
    {
        return _renderStateCacheBuildCount;
    }

    internal static void ResetRenderStateCacheBuildCountForTests()
    {
        _renderStateCacheBuildCount = 0;
    }

    internal void BuildRoundedGeometryCacheForTests()
    {
        var slot = LayoutSlot;
        var radii = ResolveOuterRadii(slot);
        EnsureRoundedGeometryCache(slot, radii, ResolveRenderState().BorderThickness);
    }

    internal void ResolveRenderStateForTests()
    {
        _ = ResolveRenderState();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var chrome = GetChromeThickness();
        var innerAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - chrome.Horizontal),
            MathF.Max(0f, availableSize.Y - chrome.Vertical));

        if (Child is not FrameworkElement childElement)
        {
            return new Vector2(chrome.Horizontal, chrome.Vertical);
        }

        childElement.Measure(innerAvailable);
        return new Vector2(
            childElement.DesiredSize.X + chrome.Horizontal,
            childElement.DesiredSize.Y + chrome.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (Child is not FrameworkElement childElement)
        {
            return finalSize;
        }

        var border = BorderThickness;
        var padding = Padding;
        var left = border.Left + padding.Left;
        var top = border.Top + padding.Top;
        var right = border.Right + padding.Right;
        var bottom = border.Bottom + padding.Bottom;

        var childRect = new LayoutRect(
            LayoutSlot.X + left,
            LayoutSlot.Y + top,
            MathF.Max(0f, finalSize.X - left - right),
            MathF.Max(0f, finalSize.Y - top - bottom));

        childElement.Arrange(childRect);
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return;
        }

        var renderState = ResolveRenderState();
        var backgroundColor = renderState.BackgroundColor;
        var borderColor = renderState.BorderColor;
        var borderThickness = renderState.BorderThickness;
        var hasVisibleBackground = renderState.HasVisibleBackground;
        var hasVisibleBorder = renderState.HasVisibleBorder;
        if (!hasVisibleBackground && !hasVisibleBorder)
        {
            return;
        }

        var cornerRadius = CornerRadius;
        if (!HasAnyCornerRadius(cornerRadius))
        {
            DrawRectangularBorder(spriteBatch, slot, borderThickness, backgroundColor, borderColor);
            return;
        }

        var outerRadii = ResolveOuterRadii(slot);
        if (!outerRadii.HasAnyRadius)
        {
            DrawRectangularBorder(spriteBatch, slot, borderThickness, backgroundColor, borderColor);
            return;
        }

        if (TryDrawCachedRoundedBorderTexture(spriteBatch, slot, outerRadii, borderThickness, backgroundColor, borderColor, hasVisibleBackground, hasVisibleBorder))
        {
            return;
        }

        if (hasVisibleBackground)
        {
            DrawRoundedRectFill(spriteBatch, slot, outerRadii, borderThickness, backgroundColor);
        }

        if (!hasVisibleBorder)
        {
            return;
        }

        DrawRoundedBorder(spriteBatch, slot, borderThickness, outerRadii, borderColor);
    }

    private Thickness GetChromeThickness()
    {
        var border = BorderThickness;
        var padding = Padding;
        return new Thickness(
            border.Left + padding.Left,
            border.Top + padding.Top,
            border.Right + padding.Right,
            border.Bottom + padding.Bottom);
    }

    private Thickness GetRenderBorderThickness()
    {
        var border = BorderThickness;
        return new Thickness(
            MathF.Max(0f, border.Left),
            MathF.Max(0f, border.Top),
            MathF.Max(0f, border.Right),
            MathF.Max(0f, border.Bottom));
    }

    private void OnBackgroundBrushChanged(DependencyPropertyChangedEventArgs args)
    {
        InvalidateRenderStateCache();
        if (args.OldValue is Brush oldBrush)
        {
            oldBrush.Changed -= OnBackgroundBrushMutated;
        }

        if (args.NewValue is Brush newBrush)
        {
            newBrush.Changed += OnBackgroundBrushMutated;
        }
    }

    private void OnBorderBrushChanged(DependencyPropertyChangedEventArgs args)
    {
        InvalidateRenderStateCache();
        if (args.OldValue is Brush oldBrush)
        {
            oldBrush.Changed -= OnBorderBrushMutated;
        }

        if (args.NewValue is Brush newBrush)
        {
            newBrush.Changed += OnBorderBrushMutated;
        }
    }

    private void OnBackgroundBrushMutated()
    {
        InvalidateRenderStateCache();
        InvalidateVisual();
    }

    private void OnBorderBrushMutated()
    {
        InvalidateRenderStateCache();
        InvalidateVisual();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, BorderThicknessProperty))
        {
            InvalidateRenderStateCache();
            InvalidateOuterRadiiCache();
            return;
        }

        if (ReferenceEquals(args.Property, CornerRadiusProperty))
        {
            InvalidateOuterRadiiCache();
        }
    }

    private void DrawRectangularBorder(
        SpriteBatch spriteBatch,
        LayoutRect slot,
        Thickness borderThickness,
        Color backgroundColor,
        Color borderColor)
    {
        if (TryDrawAxisAlignedRectangularBorder(spriteBatch, slot, borderThickness, backgroundColor, borderColor))
        {
            return;
        }

        if (backgroundColor.A > 0)
        {
            UiDrawing.DrawFilledRect(spriteBatch, slot, backgroundColor, Opacity);
        }

        if (!HasVisibleBorder(borderThickness, borderColor))
        {
            return;
        }

        if (borderThickness.Left > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, borderThickness.Left, slot.Height),
                borderColor,
                Opacity);
        }

        if (borderThickness.Right > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - borderThickness.Right, slot.Y, borderThickness.Right, slot.Height),
                borderColor,
                Opacity);
        }

        if (borderThickness.Top > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, slot.Width, borderThickness.Top),
                borderColor,
                Opacity);
        }

        if (borderThickness.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - borderThickness.Bottom, slot.Width, borderThickness.Bottom),
                borderColor,
                Opacity);
        }
    }

    private bool TryDrawAxisAlignedRectangularBorder(
        SpriteBatch spriteBatch,
        LayoutRect slot,
        Thickness borderThickness,
        Color backgroundColor,
        Color borderColor)
    {
        if (!UiDrawing.TryGetAxisAligned2DTransformInfo(spriteBatch, out var scaleX, out var scaleY, out var offsetX, out var offsetY))
        {
            return false;
        }

        var pixelRect = UiDrawing.TransformRectToPixelBounds(spriteBatch, slot);
        if (pixelRect.Width <= 0 || pixelRect.Height <= 0)
        {
            return true;
        }

        if (backgroundColor.A > 0)
        {
            UiDrawing.DrawFilledRectPixels(spriteBatch, pixelRect, backgroundColor, Opacity);
        }

        if (!HasVisibleBorder(borderThickness, borderColor))
        {
            return true;
        }

        var leftWidth = GetAxisAlignedPixelThickness(slot.X, slot.X + borderThickness.Left, scaleX, offsetX);
        if (leftWidth > 0)
        {
            UiDrawing.DrawFilledRectPixels(
                spriteBatch,
                new Rectangle(pixelRect.X, pixelRect.Y, leftWidth, pixelRect.Height),
                borderColor,
                Opacity);
        }

        var rightWidth = GetAxisAlignedPixelThickness(slot.X + slot.Width - borderThickness.Right, slot.X + slot.Width, scaleX, offsetX);
        if (rightWidth > 0)
        {
            UiDrawing.DrawFilledRectPixels(
                spriteBatch,
                new Rectangle(pixelRect.Right - rightWidth, pixelRect.Y, rightWidth, pixelRect.Height),
                borderColor,
                Opacity);
        }

        var topHeight = GetAxisAlignedPixelThickness(slot.Y, slot.Y + borderThickness.Top, scaleY, offsetY);
        if (topHeight > 0)
        {
            UiDrawing.DrawFilledRectPixels(
                spriteBatch,
                new Rectangle(pixelRect.X, pixelRect.Y, pixelRect.Width, topHeight),
                borderColor,
                Opacity);
        }

        var bottomHeight = GetAxisAlignedPixelThickness(slot.Y + slot.Height - borderThickness.Bottom, slot.Y + slot.Height, scaleY, offsetY);
        if (bottomHeight > 0)
        {
            UiDrawing.DrawFilledRectPixels(
                spriteBatch,
                new Rectangle(pixelRect.X, pixelRect.Bottom - bottomHeight, pixelRect.Width, bottomHeight),
                borderColor,
                Opacity);
        }

        return true;
    }

    private void DrawRoundedRectFill(
        SpriteBatch spriteBatch,
        LayoutRect rect,
        RoundedRectRadii radii,
        Thickness borderThickness,
        Color color)
    {
        EnsureRoundedGeometryCache(rect, radii, borderThickness);
        var polygon = _roundedFillPolygon;
        var pointCount = _roundedFillPolygonPointCount;
        if (pointCount >= 3)
        {
            UiDrawing.DrawFilledPolygon(spriteBatch, polygon.AsSpan(0, pointCount), color, Opacity);
        }
    }

    private void DrawRoundedBorder(
        SpriteBatch spriteBatch,
        LayoutRect outerRect,
        Thickness borderThickness,
        RoundedRectRadii outerRadii,
        Color borderColor)
    {
        EnsureRoundedGeometryCache(outerRect, outerRadii, borderThickness);
        var innerRect = new LayoutRect(
            outerRect.X + borderThickness.Left,
            outerRect.Y + borderThickness.Top,
            MathF.Max(0f, outerRect.Width - borderThickness.Left - borderThickness.Right),
            MathF.Max(0f, outerRect.Height - borderThickness.Top - borderThickness.Bottom));

        if (innerRect.Width <= 0f || innerRect.Height <= 0f)
        {
            DrawRoundedRectFill(spriteBatch, outerRect, outerRadii, borderThickness, borderColor);
            return;
        }

        DrawBorderBand(
            spriteBatch,
            outerRect.X + outerRadii.TopLeftX,
            outerRect.Y,
            outerRect.Width - outerRadii.TopLeftX - outerRadii.TopRightX,
            borderThickness.Top,
            borderColor);
        DrawBorderBand(
            spriteBatch,
            outerRect.X + outerRadii.BottomLeftX,
            outerRect.Y + outerRect.Height - borderThickness.Bottom,
            outerRect.Width - outerRadii.BottomLeftX - outerRadii.BottomRightX,
            borderThickness.Bottom,
            borderColor);
        DrawBorderBand(
            spriteBatch,
            outerRect.X,
            outerRect.Y + outerRadii.TopLeftY,
            borderThickness.Left,
            outerRect.Height - outerRadii.TopLeftY - outerRadii.BottomLeftY,
            borderColor);
        DrawBorderBand(
            spriteBatch,
            outerRect.X + outerRect.Width - borderThickness.Right,
            outerRect.Y + outerRadii.TopRightY,
            borderThickness.Right,
            outerRect.Height - outerRadii.TopRightY - outerRadii.BottomRightY,
            borderColor);

        DrawCachedCornerBorderSegment(spriteBatch, _topLeftCornerBorderPolygon, _topLeftCornerBorderPolygonPointCount, borderColor);
        DrawCachedCornerBorderSegment(spriteBatch, _topRightCornerBorderPolygon, _topRightCornerBorderPolygonPointCount, borderColor);
        DrawCachedCornerBorderSegment(spriteBatch, _bottomRightCornerBorderPolygon, _bottomRightCornerBorderPolygonPointCount, borderColor);
        DrawCachedCornerBorderSegment(spriteBatch, _bottomLeftCornerBorderPolygon, _bottomLeftCornerBorderPolygonPointCount, borderColor);
    }

    private BorderRenderState ResolveRenderState()
    {
        if (_hasRenderStateCache)
        {
            return _renderStateCache;
        }

        _renderStateCacheBuildCount++;
        var backgroundColor = Background?.ToColor() ?? Color.Transparent;
        var borderColor = BorderBrush?.ToColor() ?? Color.Transparent;
        var borderThickness = GetRenderBorderThickness();
        _renderStateCache = new BorderRenderState(
            backgroundColor,
            borderColor,
            borderThickness,
            backgroundColor.A > 0,
            HasVisibleBorder(borderThickness, borderColor));
        _hasRenderStateCache = true;
        return _renderStateCache;
    }

    private RoundedRectRadii ResolveOuterRadii(LayoutRect slot)
    {
        if (_hasOuterRadiiCache &&
            AreClose(_outerRadiiCacheWidth, slot.Width) &&
            AreClose(_outerRadiiCacheHeight, slot.Height))
        {
            return _outerRadiiCache;
        }

        _outerRadiiCache = CreateOuterRadii(CornerRadius, slot.Width, slot.Height);
        _outerRadiiCacheWidth = slot.Width;
        _outerRadiiCacheHeight = slot.Height;
        _hasOuterRadiiCache = true;
        return _outerRadiiCache;
    }

    private void InvalidateRenderStateCache()
    {
        _hasRenderStateCache = false;
    }

    private void InvalidateOuterRadiiCache()
    {
        _hasOuterRadiiCache = false;
        _outerRadiiCacheWidth = float.NaN;
        _outerRadiiCacheHeight = float.NaN;
        _outerRadiiCache = RoundedRectRadii.Empty;
    }

    private bool TryDrawCachedRoundedBorderTexture(
        SpriteBatch spriteBatch,
        LayoutRect slot,
        RoundedRectRadii outerRadii,
        Thickness borderThickness,
        Color backgroundColor,
        Color borderColor,
        bool hasVisibleBackground,
        bool hasVisibleBorder)
    {
        var pixelWidth = Math.Max(1, (int)MathF.Round(slot.Width));
        var pixelHeight = Math.Max(1, (int)MathF.Round(slot.Height));
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return false;
        }

        if ((long)pixelWidth * pixelHeight > 1_048_576L)
        {
            return false;
        }

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!RoundedTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<RoundedTextureCacheKey, Texture2D>();
            RoundedTextureCaches[graphicsDevice] = cache;
        }

        var cacheKey = new RoundedTextureCacheKey(
            pixelWidth,
            pixelHeight,
            outerRadii,
            borderThickness,
            backgroundColor,
            borderColor,
            hasVisibleBackground,
            hasVisibleBorder);
        if (!cache.TryGetValue(cacheKey, out var texture))
        {
            texture = BuildRoundedBorderTexture(graphicsDevice, pixelWidth, pixelHeight, outerRadii, borderThickness, backgroundColor, borderColor, hasVisibleBackground, hasVisibleBorder);
            cache[cacheKey] = texture;
        }

        UiDrawing.DrawTexture(spriteBatch, texture, slot, color: Color.White, opacity: Opacity);
        return true;
    }

    private static Texture2D BuildRoundedBorderTexture(
        GraphicsDevice graphicsDevice,
        int width,
        int height,
        RoundedRectRadii outerRadii,
        Thickness borderThickness,
        Color backgroundColor,
        Color borderColor,
        bool hasVisibleBackground,
        bool hasVisibleBorder)
    {
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        var outerRect = new LayoutRect(0f, 0f, width, height);
        var innerRect = new LayoutRect(
            borderThickness.Left,
            borderThickness.Top,
            MathF.Max(0f, width - borderThickness.Left - borderThickness.Right),
            MathF.Max(0f, height - borderThickness.Top - borderThickness.Bottom));
        var innerRadii = innerRect.Width <= 0f || innerRect.Height <= 0f
            ? RoundedRectRadii.Empty
            : NormalizeRadii(
                new RoundedRectRadii(
                    MathF.Max(0f, outerRadii.TopLeftX - borderThickness.Left),
                    MathF.Max(0f, outerRadii.TopLeftY - borderThickness.Top),
                    MathF.Max(0f, outerRadii.TopRightX - borderThickness.Right),
                    MathF.Max(0f, outerRadii.TopRightY - borderThickness.Top),
                    MathF.Max(0f, outerRadii.BottomRightX - borderThickness.Right),
                    MathF.Max(0f, outerRadii.BottomRightY - borderThickness.Bottom),
                    MathF.Max(0f, outerRadii.BottomLeftX - borderThickness.Left),
                    MathF.Max(0f, outerRadii.BottomLeftY - borderThickness.Bottom)),
                innerRect.Width,
                innerRect.Height);

        for (var y = 0; y < height; y++)
        {
            var sampleY = y + 0.5f;
            for (var x = 0; x < width; x++)
            {
                var sampleX = x + 0.5f;
                if (!ContainsRoundedRectPoint(outerRect, outerRadii, sampleX, sampleY))
                {
                    pixels[(y * width) + x] = Color.Transparent;
                    continue;
                }

                if (hasVisibleBorder &&
                    innerRect.Width > 0f &&
                    innerRect.Height > 0f &&
                    !ContainsRoundedRectPoint(innerRect, innerRadii, sampleX, sampleY))
                {
                    pixels[(y * width) + x] = borderColor;
                    continue;
                }

                pixels[(y * width) + x] = hasVisibleBackground
                    ? backgroundColor
                    : Color.Transparent;
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private void DrawBorderBand(SpriteBatch spriteBatch, float x, float y, float width, float height, Color color)
    {
        if (width <= 0f || height <= 0f || color.A == 0)
        {
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, width, height), color, Opacity);
    }

    private void DrawCachedCornerBorderSegment(
        SpriteBatch spriteBatch,
        Vector2[] polygon,
        int pointCount,
        Color color)
    {
        if (pointCount >= 3)
        {
            UiDrawing.DrawFilledPolygon(spriteBatch, polygon.AsSpan(0, pointCount), color, Opacity);
        }
    }

    private void EnsureRoundedGeometryCache(LayoutRect outerRect, RoundedRectRadii outerRadii, Thickness borderThickness)
    {
        var cacheKey = new RoundedGeometryCacheKey(outerRect, borderThickness, CornerRadius);
        if (_hasRoundedGeometryCache &&
            RoundedGeometryCacheKeyClose(_roundedGeometryCacheKey, cacheKey))
        {
            return;
        }

        _roundedGeometryCacheBuildCount++;
        _roundedGeometryCacheKey = cacheKey;
        _hasRoundedGeometryCache = true;

        Span<Vector2> fillPolygon = stackalloc Vector2[96];
        _roundedFillPolygonPointCount = BuildRoundedRectPolygon(outerRect, outerRadii, fillPolygon);
        _roundedFillPolygon = CopyPolygon(fillPolygon, _roundedFillPolygonPointCount);

        var innerRect = new LayoutRect(
            outerRect.X + borderThickness.Left,
            outerRect.Y + borderThickness.Top,
            MathF.Max(0f, outerRect.Width - borderThickness.Left - borderThickness.Right),
            MathF.Max(0f, outerRect.Height - borderThickness.Top - borderThickness.Bottom));
        if (innerRect.Width <= 0f || innerRect.Height <= 0f)
        {
            _topLeftCornerBorderPolygon = Array.Empty<Vector2>();
            _topRightCornerBorderPolygon = Array.Empty<Vector2>();
            _bottomRightCornerBorderPolygon = Array.Empty<Vector2>();
            _bottomLeftCornerBorderPolygon = Array.Empty<Vector2>();
            _topLeftCornerBorderPolygonPointCount = 0;
            _topRightCornerBorderPolygonPointCount = 0;
            _bottomRightCornerBorderPolygonPointCount = 0;
            _bottomLeftCornerBorderPolygonPointCount = 0;
            return;
        }

        var innerRadii = NormalizeRadii(
            new RoundedRectRadii(
                MathF.Max(0f, outerRadii.TopLeftX - borderThickness.Left),
                MathF.Max(0f, outerRadii.TopLeftY - borderThickness.Top),
                MathF.Max(0f, outerRadii.TopRightX - borderThickness.Right),
                MathF.Max(0f, outerRadii.TopRightY - borderThickness.Top),
                MathF.Max(0f, outerRadii.BottomRightX - borderThickness.Right),
                MathF.Max(0f, outerRadii.BottomRightY - borderThickness.Bottom),
                MathF.Max(0f, outerRadii.BottomLeftX - borderThickness.Left),
                MathF.Max(0f, outerRadii.BottomLeftY - borderThickness.Bottom)),
            innerRect.Width,
            innerRect.Height);

        CacheCornerBorderPolygon(BorderCorner.TopLeft, outerRect, innerRect, outerRadii, innerRadii, ref _topLeftCornerBorderPolygon, ref _topLeftCornerBorderPolygonPointCount);
        CacheCornerBorderPolygon(BorderCorner.TopRight, outerRect, innerRect, outerRadii, innerRadii, ref _topRightCornerBorderPolygon, ref _topRightCornerBorderPolygonPointCount);
        CacheCornerBorderPolygon(BorderCorner.BottomRight, outerRect, innerRect, outerRadii, innerRadii, ref _bottomRightCornerBorderPolygon, ref _bottomRightCornerBorderPolygonPointCount);
        CacheCornerBorderPolygon(BorderCorner.BottomLeft, outerRect, innerRect, outerRadii, innerRadii, ref _bottomLeftCornerBorderPolygon, ref _bottomLeftCornerBorderPolygonPointCount);
    }

    private static void CacheCornerBorderPolygon(
        BorderCorner corner,
        LayoutRect outerRect,
        LayoutRect innerRect,
        RoundedRectRadii outerRadii,
        RoundedRectRadii innerRadii,
        ref Vector2[] polygon,
        ref int pointCount)
    {
        Span<Vector2> buffer = stackalloc Vector2[64];
        pointCount = BuildCornerBorderPolygon(corner, outerRect, innerRect, outerRadii, innerRadii, buffer);
        polygon = CopyPolygon(buffer, pointCount);
    }

    private static Vector2[] CopyPolygon(Span<Vector2> source, int pointCount)
    {
        if (pointCount <= 0)
        {
            return Array.Empty<Vector2>();
        }

        var copy = new Vector2[pointCount];
        source[..pointCount].CopyTo(copy);
        return copy;
    }

    private static bool RoundedGeometryCacheKeyClose(RoundedGeometryCacheKey left, RoundedGeometryCacheKey right)
    {
        return AreClose(left.Rect.X, right.Rect.X) &&
               AreClose(left.Rect.Y, right.Rect.Y) &&
               AreClose(left.Rect.Width, right.Rect.Width) &&
               AreClose(left.Rect.Height, right.Rect.Height) &&
               ThicknessClose(left.BorderThickness, right.BorderThickness) &&
               CornerRadiusClose(left.CornerRadius, right.CornerRadius);
    }

    private static bool ThicknessClose(Thickness left, Thickness right)
    {
        return AreClose(left.Left, right.Left) &&
               AreClose(left.Top, right.Top) &&
               AreClose(left.Right, right.Right) &&
               AreClose(left.Bottom, right.Bottom);
    }

    private static bool CornerRadiusClose(CornerRadius left, CornerRadius right)
    {
        return AreClose(left.TopLeft, right.TopLeft) &&
               AreClose(left.TopRight, right.TopRight) &&
               AreClose(left.BottomRight, right.BottomRight) &&
               AreClose(left.BottomLeft, right.BottomLeft);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) < 0.01f;
    }

    private static bool HasAnyCornerRadius(CornerRadius cornerRadius)
    {
        return cornerRadius.TopLeft > 0f ||
               cornerRadius.TopRight > 0f ||
               cornerRadius.BottomRight > 0f ||
               cornerRadius.BottomLeft > 0f;
    }

    private static int GetAxisAlignedPixelThickness(float start, float end, float scale, float offset)
    {
        var transformedStart = (start * scale) + offset;
        var transformedEnd = (end * scale) + offset;
        return Math.Abs((int)MathF.Round(transformedEnd) - (int)MathF.Round(transformedStart));
    }

    private static bool HasVisibleBorder(Thickness thickness, Color borderColor)
    {
        return borderColor.A > 0 &&
               (thickness.Left > 0f || thickness.Top > 0f || thickness.Right > 0f || thickness.Bottom > 0f);
    }

    private static RoundedRectRadii CreateOuterRadii(CornerRadius cornerRadius, float width, float height)
    {
        var clamped = cornerRadius.ClampNonNegative();
        return NormalizeRadii(
            new RoundedRectRadii(
                clamped.TopLeft,
                clamped.TopLeft,
                clamped.TopRight,
                clamped.TopRight,
                clamped.BottomRight,
                clamped.BottomRight,
                clamped.BottomLeft,
                clamped.BottomLeft),
            width,
            height);
    }

    private static RoundedRectRadii NormalizeRadii(RoundedRectRadii radii, float width, float height)
    {
        if (width <= 0f || height <= 0f)
        {
            return RoundedRectRadii.Empty;
        }

        var maxHorizontal = MathF.Max(radii.TopLeftX + radii.TopRightX, radii.BottomLeftX + radii.BottomRightX);
        var maxVertical = MathF.Max(radii.TopLeftY + radii.BottomLeftY, radii.TopRightY + radii.BottomRightY);
        var scaleX = maxHorizontal > width && maxHorizontal > 0f ? width / maxHorizontal : 1f;
        var scaleY = maxVertical > height && maxVertical > 0f ? height / maxVertical : 1f;
        if (scaleX >= 1f && scaleY >= 1f)
        {
            return radii;
        }

        return new RoundedRectRadii(
            radii.TopLeftX * scaleX,
            radii.TopLeftY * scaleY,
            radii.TopRightX * scaleX,
            radii.TopRightY * scaleY,
            radii.BottomRightX * scaleX,
            radii.BottomRightY * scaleY,
            radii.BottomLeftX * scaleX,
            radii.BottomLeftY * scaleY);
    }

    private static int BuildRoundedRectPolygon(LayoutRect rect, RoundedRectRadii radii, Span<Vector2> buffer)
    {
        if (!radii.HasAnyRadius)
        {
            buffer[0] = new Vector2(rect.X, rect.Y);
            buffer[1] = new Vector2(rect.X + rect.Width, rect.Y);
            buffer[2] = new Vector2(rect.X + rect.Width, rect.Y + rect.Height);
            buffer[3] = new Vector2(rect.X, rect.Y + rect.Height);
            return 4;
        }

        var count = 0;
        count = AddPoint(buffer, count, new Vector2(rect.X + radii.TopLeftX, rect.Y));
        count = AddPoint(buffer, count, new Vector2(rect.X + rect.Width - radii.TopRightX, rect.Y));
        count = AppendArc(buffer, count, new Vector2(rect.X + rect.Width - radii.TopRightX, rect.Y + radii.TopRightY), radii.TopRightX, radii.TopRightY, -MathF.PI / 2f, 0f, includeStart: false);
        count = AddPoint(buffer, count, new Vector2(rect.X + rect.Width, rect.Y + rect.Height - radii.BottomRightY));
        count = AppendArc(buffer, count, new Vector2(rect.X + rect.Width - radii.BottomRightX, rect.Y + rect.Height - radii.BottomRightY), radii.BottomRightX, radii.BottomRightY, 0f, MathF.PI / 2f, includeStart: false);
        count = AddPoint(buffer, count, new Vector2(rect.X + radii.BottomLeftX, rect.Y + rect.Height));
        count = AppendArc(buffer, count, new Vector2(rect.X + radii.BottomLeftX, rect.Y + rect.Height - radii.BottomLeftY), radii.BottomLeftX, radii.BottomLeftY, MathF.PI / 2f, MathF.PI, includeStart: false);
        count = AddPoint(buffer, count, new Vector2(rect.X, rect.Y + radii.TopLeftY));
        count = AppendArc(buffer, count, new Vector2(rect.X + radii.TopLeftX, rect.Y + radii.TopLeftY), radii.TopLeftX, radii.TopLeftY, MathF.PI, MathF.PI * 1.5f, includeStart: false);
        return count;
    }

    private static int BuildCornerBorderPolygon(
        BorderCorner corner,
        LayoutRect outerRect,
        LayoutRect innerRect,
        RoundedRectRadii outerRadii,
        RoundedRectRadii innerRadii,
        Span<Vector2> buffer)
    {
        var count = 0;

        switch (corner)
        {
            case BorderCorner.TopLeft:
                if (outerRadii.TopLeftX <= 0f && outerRadii.TopLeftY <= 0f)
                {
                    return 0;
                }

                count = AppendArc(buffer, count, new Vector2(outerRect.X + outerRadii.TopLeftX, outerRect.Y + outerRadii.TopLeftY), outerRadii.TopLeftX, outerRadii.TopLeftY, MathF.PI * 1.5f, MathF.PI, includeStart: true);
                count = AppendInnerBoundary(buffer, count, new Vector2(innerRect.X + innerRadii.TopLeftX, innerRect.Y + innerRadii.TopLeftY), innerRadii.TopLeftX, innerRadii.TopLeftY, MathF.PI, MathF.PI * 1.5f, new Vector2(innerRect.X, innerRect.Y));
                return count;

            case BorderCorner.TopRight:
                if (outerRadii.TopRightX <= 0f && outerRadii.TopRightY <= 0f)
                {
                    return 0;
                }

                count = AppendArc(buffer, count, new Vector2(outerRect.X + outerRect.Width - outerRadii.TopRightX, outerRect.Y + outerRadii.TopRightY), outerRadii.TopRightX, outerRadii.TopRightY, -MathF.PI / 2f, 0f, includeStart: true);
                count = AppendInnerBoundary(buffer, count, new Vector2(innerRect.X + innerRect.Width - innerRadii.TopRightX, innerRect.Y + innerRadii.TopRightY), innerRadii.TopRightX, innerRadii.TopRightY, 0f, -MathF.PI / 2f, new Vector2(innerRect.X + innerRect.Width, innerRect.Y));
                return count;

            case BorderCorner.BottomRight:
                if (outerRadii.BottomRightX <= 0f && outerRadii.BottomRightY <= 0f)
                {
                    return 0;
                }

                count = AppendArc(buffer, count, new Vector2(outerRect.X + outerRect.Width - outerRadii.BottomRightX, outerRect.Y + outerRect.Height - outerRadii.BottomRightY), outerRadii.BottomRightX, outerRadii.BottomRightY, 0f, MathF.PI / 2f, includeStart: true);
                count = AppendInnerBoundary(buffer, count, new Vector2(innerRect.X + innerRect.Width - innerRadii.BottomRightX, innerRect.Y + innerRect.Height - innerRadii.BottomRightY), innerRadii.BottomRightX, innerRadii.BottomRightY, MathF.PI / 2f, 0f, new Vector2(innerRect.X + innerRect.Width, innerRect.Y + innerRect.Height));
                return count;

            case BorderCorner.BottomLeft:
                if (outerRadii.BottomLeftX <= 0f && outerRadii.BottomLeftY <= 0f)
                {
                    return 0;
                }

                count = AppendArc(buffer, count, new Vector2(outerRect.X + outerRadii.BottomLeftX, outerRect.Y + outerRect.Height - outerRadii.BottomLeftY), outerRadii.BottomLeftX, outerRadii.BottomLeftY, MathF.PI / 2f, MathF.PI, includeStart: true);
                count = AppendInnerBoundary(buffer, count, new Vector2(innerRect.X + innerRadii.BottomLeftX, innerRect.Y + innerRect.Height - innerRadii.BottomLeftY), innerRadii.BottomLeftX, innerRadii.BottomLeftY, MathF.PI, MathF.PI / 2f, new Vector2(innerRect.X, innerRect.Y + innerRect.Height));
                return count;

            default:
                return 0;
        }
    }

    private static int AppendInnerBoundary(
        Span<Vector2> buffer,
        int count,
        Vector2 center,
        float radiusX,
        float radiusY,
        float startAngle,
        float endAngle,
        Vector2 fallbackPoint)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return AddPoint(buffer, count, fallbackPoint);
        }

        return AppendArc(buffer, count, center, radiusX, radiusY, startAngle, endAngle, includeStart: true);
    }

    private static int AppendArc(
        Span<Vector2> buffer,
        int count,
        Vector2 center,
        float radiusX,
        float radiusY,
        float startAngle,
        float endAngle,
        bool includeStart)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return AddPoint(buffer, count, new Vector2(
                center.X + (MathF.Cos(endAngle) * radiusX),
                center.Y + (MathF.Sin(endAngle) * radiusY)));
        }

        var segmentCount = GetArcSegmentCount(MathF.Max(radiusX, radiusY));
        var startIndex = includeStart ? 0 : 1;
        for (var index = startIndex; index <= segmentCount; index++)
        {
            var progress = (float)index / segmentCount;
            var angle = startAngle + ((endAngle - startAngle) * progress);
            count = AddPoint(buffer, count, new Vector2(
                center.X + (MathF.Cos(angle) * radiusX),
                center.Y + (MathF.Sin(angle) * radiusY)));
        }

        return count;
    }

    private static int AddPoint(Span<Vector2> buffer, int count, Vector2 point)
    {
        if (count > 0)
        {
            var previous = buffer[count - 1];
            if (MathF.Abs(previous.X - point.X) < 0.01f && MathF.Abs(previous.Y - point.Y) < 0.01f)
            {
                return count;
            }
        }

        buffer[count] = point;
        return count + 1;
    }

    private static int GetArcSegmentCount(float radius)
    {
        return Math.Clamp((int)MathF.Ceiling(radius / 4f), 2, 12);
    }

    private static bool ContainsRoundedRectPoint(
        LayoutRect rect,
        RoundedRectRadii radii,
        float x,
        float y)
    {
        if (x < rect.X || x >= rect.X + rect.Width || y < rect.Y || y >= rect.Y + rect.Height)
        {
            return false;
        }

        if (!radii.HasAnyRadius)
        {
            return true;
        }

        if (x < rect.X + radii.TopLeftX && y < rect.Y + radii.TopLeftY)
        {
            return IsInsideEllipse(rect.X + radii.TopLeftX, rect.Y + radii.TopLeftY, radii.TopLeftX, radii.TopLeftY, x, y);
        }

        if (x >= rect.X + rect.Width - radii.TopRightX && y < rect.Y + radii.TopRightY)
        {
            return IsInsideEllipse(rect.X + rect.Width - radii.TopRightX, rect.Y + radii.TopRightY, radii.TopRightX, radii.TopRightY, x, y);
        }

        if (x >= rect.X + rect.Width - radii.BottomRightX && y >= rect.Y + rect.Height - radii.BottomRightY)
        {
            return IsInsideEllipse(rect.X + rect.Width - radii.BottomRightX, rect.Y + rect.Height - radii.BottomRightY, radii.BottomRightX, radii.BottomRightY, x, y);
        }

        if (x < rect.X + radii.BottomLeftX && y >= rect.Y + rect.Height - radii.BottomLeftY)
        {
            return IsInsideEllipse(rect.X + radii.BottomLeftX, rect.Y + rect.Height - radii.BottomLeftY, radii.BottomLeftX, radii.BottomLeftY, x, y);
        }

        return true;
    }

    private static bool IsInsideEllipse(float centerX, float centerY, float radiusX, float radiusY, float x, float y)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return true;
        }

        var normalizedX = (x - centerX) / radiusX;
        var normalizedY = (y - centerY) / radiusY;
        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1f;
    }

    private enum BorderCorner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    private readonly record struct RoundedGeometryCacheKey(
        LayoutRect Rect,
        Thickness BorderThickness,
        CornerRadius CornerRadius);

    private readonly record struct RoundedTextureCacheKey(
        int Width,
        int Height,
        RoundedRectRadii Radii,
        Thickness BorderThickness,
        Color BackgroundColor,
        Color BorderColor,
        bool HasVisibleBackground,
        bool HasVisibleBorder);

    private readonly struct RoundedRectRadii
    {
        public RoundedRectRadii(
            float topLeftX,
            float topLeftY,
            float topRightX,
            float topRightY,
            float bottomRightX,
            float bottomRightY,
            float bottomLeftX,
            float bottomLeftY)
        {
            TopLeftX = topLeftX;
            TopLeftY = topLeftY;
            TopRightX = topRightX;
            TopRightY = topRightY;
            BottomRightX = bottomRightX;
            BottomRightY = bottomRightY;
            BottomLeftX = bottomLeftX;
            BottomLeftY = bottomLeftY;
        }

        public float TopLeftX { get; }

        public float TopLeftY { get; }

        public float TopRightX { get; }

        public float TopRightY { get; }

        public float BottomRightX { get; }

        public float BottomRightY { get; }

        public float BottomLeftX { get; }

        public float BottomLeftY { get; }

        public bool HasAnyRadius =>
            TopLeftX > 0f ||
            TopLeftY > 0f ||
            TopRightX > 0f ||
            TopRightY > 0f ||
            BottomRightX > 0f ||
            BottomRightY > 0f ||
            BottomLeftX > 0f ||
            BottomLeftY > 0f;

        public static RoundedRectRadii Empty => new(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
    }

    private readonly record struct BorderRenderState(
        Color BackgroundColor,
        Color BorderColor,
        Thickness BorderThickness,
        bool HasVisibleBackground,
        bool HasVisibleBorder);
}
