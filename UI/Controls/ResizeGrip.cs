using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ResizeGrip : Control
{
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(
            nameof(Target),
            typeof(FrameworkElement),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ResizeIncrementProperty =
        DependencyProperty.Register(
            nameof(ResizeIncrement),
            typeof(float),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(
                1f,
                coerceValueCallback: static (_, value) => value is float step && step > 0f ? step : 1f));

    public static readonly DependencyProperty GripSizeProperty =
        DependencyProperty.Register(
            nameof(GripSize),
            typeof(float),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(
                16f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float size && size >= 8f ? size : 8f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(new Color(0, 0, 0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(new Color(196, 196, 196), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(
            nameof(IsDragging),
            typeof(bool),
            typeof(ResizeGrip),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isDragging;
    private FrameworkElement? _activeTarget;
    private Vector2 _dragStartPointer;
    private float _startWidth;
    private float _startHeight;

    public ResizeGrip()
    {
        Focusable = true;
        Cursor = UiCursor.SizeNWSE;
    }

    public FrameworkElement? Target
    {
        get => GetValue<FrameworkElement>(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public float ResizeIncrement
    {
        get => GetValue<float>(ResizeIncrementProperty);
        set => SetValue(ResizeIncrementProperty, value);
    }

    public float GripSize
    {
        get => GetValue<float>(GripSizeProperty);
        set => SetValue(GripSizeProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var size = GripSize;
        desired.X = MathF.Max(desired.X, size);
        desired.Y = MathF.Max(desired.Y, size);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var background = IsDragging
            ? Blend(Background, Color.White, 0.16f)
            : IsMouseOver ? Blend(Background, Color.White, 0.08f) : Background;
        UiDrawing.DrawFilledRect(spriteBatch, slot, background, Opacity);

        var dotColor = IsDragging
            ? Blend(Foreground, Color.White, 0.2f)
            : IsMouseOver ? Blend(Foreground, Color.White, 0.1f) : Foreground;

        var inset = 3f;
        var step = 4f;
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col <= row; col++)
            {
                var x = slot.X + slot.Width - inset - (col * step) - 2f;
                var y = slot.Y + slot.Height - inset - ((2 - row) * step) - 2f;
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, 2f, 2f), dotColor, Opacity);
            }
        }
    }

    protected override void OnMouseEnter(RoutedMouseEventArgs args)
    {
        base.OnMouseEnter(args);
        IsMouseOver = true;
    }

    protected override void OnMouseLeave(RoutedMouseEventArgs args)
    {
        base.OnMouseLeave(args);
        IsMouseOver = false;
    }

    protected override void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);
        if (!IsEnabled)
        {
            return;
        }

        var target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        _activeTarget = target;
        _isDragging = true;
        IsDragging = true;
        _dragStartPointer = args.Position;
        _startWidth = ResolveCurrentDimension(target.Width, target.ActualWidth, target.DesiredSize.X);
        _startHeight = ResolveCurrentDimension(target.Height, target.ActualHeight, target.DesiredSize.Y);

        Focus();
        CaptureMouse();
        args.Handled = true;
    }

    protected override void OnMouseMove(RoutedMouseEventArgs args)
    {
        base.OnMouseMove(args);
        if (!_isDragging || _activeTarget == null)
        {
            return;
        }

        var deltaX = Snap(args.Position.X - _dragStartPointer.X, ResizeIncrement);
        var deltaY = Snap(args.Position.Y - _dragStartPointer.Y, ResizeIncrement);
        ApplyResize(_activeTarget, _startWidth + deltaX, _startHeight + deltaY);
        args.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);
        EndDrag(releaseCapture: true);
        args.Handled = true;
    }

    protected override void OnLostMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        base.OnLostMouseCapture(args);
        EndDrag(releaseCapture: false);
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (!IsEnabled)
        {
            return;
        }

        var target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        var delta = 10f;
        var width = ResolveCurrentDimension(target.Width, target.ActualWidth, target.DesiredSize.X);
        var height = ResolveCurrentDimension(target.Height, target.ActualHeight, target.DesiredSize.Y);
        var handled = true;

        switch (args.Key)
        {
            case Keys.Right:
                width += delta;
                break;
            case Keys.Left:
                width -= delta;
                break;
            case Keys.Down:
                height += delta;
                break;
            case Keys.Up:
                height -= delta;
                break;
            default:
                handled = false;
                break;
        }

        if (!handled)
        {
            return;
        }

        ApplyResize(target, width, height);
        args.Handled = true;
    }

    private FrameworkElement? ResolveTarget()
    {
        if (Target != null)
        {
            return Target;
        }

        for (var current = VisualParent ?? LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Popup popup)
            {
                return popup;
            }
        }

        return null;
    }

    private static void ApplyResize(FrameworkElement target, float width, float height)
    {
        var clampedWidth = Clamp(width, target.MinWidth, target.MaxWidth);
        var clampedHeight = Clamp(height, target.MinHeight, target.MaxHeight);
        target.Width = MathF.Max(0f, clampedWidth);
        target.Height = MathF.Max(0f, clampedHeight);
    }

    private void EndDrag(bool releaseCapture)
    {
        _isDragging = false;
        IsDragging = false;
        _activeTarget = null;

        if (releaseCapture && ReferenceEquals(InputManager.MouseCapturedElement, this))
        {
            ReleaseMouseCapture();
        }
    }

    private static float ResolveCurrentDimension(float explicitSize, float actual, float desired)
    {
        if (!float.IsNaN(explicitSize) && explicitSize > 0f)
        {
            return explicitSize;
        }

        if (actual > 0f)
        {
            return actual;
        }

        return MathF.Max(0f, desired);
    }

    private static float Snap(float value, float increment)
    {
        if (increment <= 0f)
        {
            return value;
        }

        return MathF.Round(value / increment) * increment;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        var clamped = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)(from.R + ((to.R - from.R) * clamped)),
            (byte)(from.G + ((to.G - from.G) * clamped)),
            (byte)(from.B + ((to.B - from.B) * clamped)),
            (byte)(from.A + ((to.A - from.A) * clamped)));
    }
}
