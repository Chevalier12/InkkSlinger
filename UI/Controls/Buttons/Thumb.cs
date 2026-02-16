using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Thumb : Control
{
    public static readonly RoutedEvent DragStartedEvent =
        new(nameof(DragStarted), RoutingStrategy.Bubble);

    public static readonly RoutedEvent DragDeltaEvent =
        new(nameof(DragDelta), RoutingStrategy.Bubble);

    public static readonly RoutedEvent DragCompletedEvent =
        new(nameof(DragCompleted), RoutingStrategy.Bubble);

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(
            nameof(IsDragging),
            typeof(bool),
            typeof(Thumb),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Thumb),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Thumb),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Thumb),
            new FrameworkPropertyMetadata(new Color(164, 164, 164), FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isDragging;

    public Thumb()
    {
    }

    public event System.EventHandler<DragStartedEventArgs> DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    public event System.EventHandler<DragDeltaEventArgs> DragDelta
    {
        add => AddHandler(DragDeltaEvent, value);
        remove => RemoveHandler(DragDeltaEvent, value);
    }

    public event System.EventHandler<DragCompletedEventArgs> DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public void CancelDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        EndDrag(canceled: true, releaseCapture: true);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        desired.X = MathF.Max(desired.X, 10f);
        desired.Y = MathF.Max(desired.Y, 10f);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var fill = IsDragging
            ? new Color(132, 188, 240)
            : IsMouseOver ? new Color(116, 116, 116) : Background;

        UiDrawing.DrawFilledRect(spriteBatch, slot, fill, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
    }







    private void EndDrag(bool canceled, bool releaseCapture)
    {
        const float totalX = 0f;
        const float totalY = 0f;

        _isDragging = false;
        IsDragging = false;

        _ = releaseCapture;

        RaiseRoutedEvent(
            DragCompletedEvent,
            new DragCompletedEventArgs(DragCompletedEvent, totalX, totalY, canceled));
    }
}
