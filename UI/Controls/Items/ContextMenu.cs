using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ContextMenu : ListBox
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ContextMenu), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(ContextMenu), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(nameof(PlacementMode), typeof(PopupPlacementMode), typeof(ContextMenu), new FrameworkPropertyMetadata(PopupPlacementMode.Absolute, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(nameof(PlacementTarget), typeof(UIElement), typeof(ContextMenu), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(nameof(Left), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register(nameof(Top), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContextMenuProperty =
        DependencyProperty.RegisterAttached("ContextMenu", typeof(ContextMenu), typeof(ContextMenu), new FrameworkPropertyMetadata(null));

    private Panel? _host;

    public ContextMenu()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool StaysOpen
    {
        get => GetValue<bool>(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public PopupPlacementMode PlacementMode
    {
        get => GetValue<PopupPlacementMode>(PlacementModeProperty);
        set => SetValue(PlacementModeProperty, value);
    }

    public UIElement? PlacementTarget
    {
        get => GetValue<UIElement>(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    public float Left
    {
        get => GetValue<float>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public float Top
    {
        get => GetValue<float>(TopProperty);
        set => SetValue(TopProperty, value);
    }

    public void Open(Panel host)
    {
        _host = host;
        if (VisualParent == null)
        {
            host.AddChild(this);
        }

        var autoWidth = !float.IsFinite(Width) || Width <= 0f;
        var autoHeight = !float.IsFinite(Height) || Height <= 0f;
        if (autoWidth || autoHeight)
        {
            if (autoWidth)
            {
                Width = float.NaN;
            }

            if (autoHeight)
            {
                Height = float.NaN;
            }

            var contentSize = MeasureContentSizeFromItems();
            if (autoWidth)
            {
                var maxWidth = MathF.Max(1f, host.LayoutSlot.Width);
                Width = MathF.Min(maxWidth, MathF.Max(120f, contentSize.X));
            }

            if (autoHeight)
            {
                var maxHeight = MathF.Max(1f, host.LayoutSlot.Height);
                Height = MathF.Min(maxHeight, MathF.Max(24f, contentSize.Y));
            }
        }

        ApplyPlacement();

        IsOpen = true;
    }

    public void OpenAt(Panel host, float left, float top, UIElement? placementTarget = null)
    {
        PlacementMode = PopupPlacementMode.Absolute;
        PlacementTarget = placementTarget;
        Left = left;
        Top = top;
        Open(host);
    }

    public void Close()
    {
        IsOpen = false;
        if (_host != null && VisualParent != null)
        {
            _host.RemoveChild(this);
        }

        _host = null;
    }

    public static void SetContextMenu(UIElement element, ContextMenu? value)
    {
        element.SetValue(ContextMenuProperty, value);
    }

    public static ContextMenu? GetContextMenu(UIElement element)
    {
        return element.GetValue<ContextMenu>(ContextMenuProperty);
    }

    private void ApplyPlacement()
    {
        if (_host == null)
        {
            return;
        }

        if (_host is Canvas)
        {
            Canvas.SetLeft(this, Left);
            Canvas.SetTop(this, Top);
            return;
        }

        Margin = new Thickness(Left, Top, 0f, 0f);
    }

    private Vector2 MeasureContentSizeFromItems()
    {
        var maxWidth = 0f;
        var totalHeight = 0f;

        foreach (var container in ItemContainers)
        {
            if (container is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            maxWidth = MathF.Max(maxWidth, element.DesiredSize.X);
            totalHeight += element.DesiredSize.Y;
        }

        var border = BorderThickness * 2f;
        return new Vector2(
            maxWidth + border,
            totalHeight + border);
    }
}
