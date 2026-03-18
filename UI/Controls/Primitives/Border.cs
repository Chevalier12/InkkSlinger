using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Border : Decorator
{
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

        var backgroundColor = Background?.ToColor() ?? Color.Transparent;
        var borderColor = BorderBrush?.ToColor() ?? Color.Transparent;
        var borderThickness = GetRenderBorderThickness();
        var outerRadii = CreateOuterRadii(CornerRadius, slot.Width, slot.Height);
        if (!outerRadii.HasAnyRadius)
        {
            DrawRectangularBorder(spriteBatch, slot, borderThickness, backgroundColor, borderColor);
            return;
        }

        if (backgroundColor.A > 0)
        {
            DrawRoundedRectFill(spriteBatch, slot, outerRadii, backgroundColor);
        }

        if (!HasVisibleBorder(borderThickness, borderColor))
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
        InvalidateVisual();
    }

    private void OnBorderBrushMutated()
    {
        InvalidateVisual();
    }

    private void DrawRectangularBorder(
        SpriteBatch spriteBatch,
        LayoutRect slot,
        Thickness borderThickness,
        Color backgroundColor,
        Color borderColor)
    {
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

    private void DrawRoundedRectFill(
        SpriteBatch spriteBatch,
        LayoutRect rect,
        RoundedRectRadii radii,
        Color color)
    {
        Span<Vector2> polygon = stackalloc Vector2[96];
        var pointCount = BuildRoundedRectPolygon(rect, radii, polygon);
        if (pointCount >= 3)
        {
            UiDrawing.DrawFilledPolygon(spriteBatch, polygon[..pointCount], color, Opacity);
        }
    }

    private void DrawRoundedBorder(
        SpriteBatch spriteBatch,
        LayoutRect outerRect,
        Thickness borderThickness,
        RoundedRectRadii outerRadii,
        Color borderColor)
    {
        var innerRect = new LayoutRect(
            outerRect.X + borderThickness.Left,
            outerRect.Y + borderThickness.Top,
            MathF.Max(0f, outerRect.Width - borderThickness.Left - borderThickness.Right),
            MathF.Max(0f, outerRect.Height - borderThickness.Top - borderThickness.Bottom));

        if (innerRect.Width <= 0f || innerRect.Height <= 0f)
        {
            DrawRoundedRectFill(spriteBatch, outerRect, outerRadii, borderColor);
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

        DrawCornerBorderSegment(spriteBatch, BorderCorner.TopLeft, outerRect, innerRect, outerRadii, innerRadii, borderColor);
        DrawCornerBorderSegment(spriteBatch, BorderCorner.TopRight, outerRect, innerRect, outerRadii, innerRadii, borderColor);
        DrawCornerBorderSegment(spriteBatch, BorderCorner.BottomRight, outerRect, innerRect, outerRadii, innerRadii, borderColor);
        DrawCornerBorderSegment(spriteBatch, BorderCorner.BottomLeft, outerRect, innerRect, outerRadii, innerRadii, borderColor);
    }

    private void DrawBorderBand(SpriteBatch spriteBatch, float x, float y, float width, float height, Color color)
    {
        if (width <= 0f || height <= 0f || color.A == 0)
        {
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, width, height), color, Opacity);
    }

    private void DrawCornerBorderSegment(
        SpriteBatch spriteBatch,
        BorderCorner corner,
        LayoutRect outerRect,
        LayoutRect innerRect,
        RoundedRectRadii outerRadii,
        RoundedRectRadii innerRadii,
        Color color)
    {
        Span<Vector2> polygon = stackalloc Vector2[64];
        var pointCount = BuildCornerBorderPolygon(corner, outerRect, innerRect, outerRadii, innerRadii, polygon);
        if (pointCount >= 3)
        {
            UiDrawing.DrawFilledPolygon(spriteBatch, polygon[..pointCount], color, Opacity);
        }
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

    private enum BorderCorner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

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
}
