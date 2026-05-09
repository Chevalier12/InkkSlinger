using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public enum PanningMode
{
    None,
    HorizontalOnly,
    VerticalOnly,
    Both,
    HorizontalFirst,
    VerticalFirst
}

[TemplatePart("PART_ScrollContentPresenter", typeof(ScrollContentPresenter))]
[TemplatePart("PART_HorizontalScrollBar", typeof(ScrollBar))]
[TemplatePart("PART_VerticalScrollBar", typeof(ScrollBar))]
public partial class ScrollViewer : ContentControl
{
    private enum ScrollOffsetUpdateSource
    {
        External,
        HorizontalScrollBar,
        VerticalScrollBar,
    }

    private const float ContentScrollBarGap = 1f;
    public static readonly DependencyProperty UseTransformContentScrollingProperty =
        DependencyProperty.RegisterAttached(
            "UseTransformContentScrolling",
            typeof(bool),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    element.InvalidateArrange();
                    element.InvalidateVisual();
                    if (element.VisualParent is ScrollViewer visualViewer)
                    {
                        visualViewer.InvalidateArrange();
                        visualViewer.InvalidateVisual();
                    }
                    else if (element.LogicalParent is ScrollViewer logicalViewer)
                    {
                        logicalViewer.InvalidateArrange();
                        logicalViewer.InvalidateVisual();
                    }
                }));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Visible, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                0f,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not ScrollViewer viewer ||
                        viewer._suppressOffsetPropertyChange ||
                        args.OldValue is not float oldValue ||
                        args.NewValue is not float newValue ||
                        AreClose(oldValue, newValue))
                    {
                        return;
                    }

                    viewer.SetOffsets(
                        newValue,
                        viewer.VerticalOffset,
                        ScrollOffsetUpdateSource.External,
                        previousHorizontalOverride: oldValue,
                        previousVerticalOverride: viewer.VerticalOffset);
                },
                coerceValueCallback: static (dependencyObject, value) => CoerceOffsetValue(dependencyObject, value, horizontalAxis: true)));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                0f,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not ScrollViewer viewer ||
                        viewer._suppressOffsetPropertyChange ||
                        args.OldValue is not float oldValue ||
                        args.NewValue is not float newValue ||
                        AreClose(oldValue, newValue))
                    {
                        return;
                    }

                    viewer.SetOffsets(
                        viewer.HorizontalOffset,
                        newValue,
                        ScrollOffsetUpdateSource.External,
                        previousHorizontalOverride: viewer.HorizontalOffset,
                        previousVerticalOverride: oldValue);
                },
                coerceValueCallback: static (dependencyObject, value) => CoerceOffsetValue(dependencyObject, value, horizontalAxis: false)));

    public static readonly DependencyProperty ExtentWidthProperty =
        DependencyProperty.Register(
            nameof(ExtentWidth),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ExtentHeightProperty =
        DependencyProperty.Register(
            nameof(ExtentHeight),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportWidthProperty =
        DependencyProperty.Register(
            nameof(ViewportWidth),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportHeightProperty =
        DependencyProperty.Register(
            nameof(ViewportHeight),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScrollableWidthProperty =
        DependencyProperty.Register(
            nameof(ScrollableWidth),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty ScrollableHeightProperty =
        DependencyProperty.Register(
            nameof(ScrollableHeight),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty ComputedHorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(ComputedHorizontalScrollBarVisibility),
            typeof(Visibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty ComputedVerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(ComputedVerticalScrollBarVisibility),
            typeof(Visibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty ContentHorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(ContentHorizontalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty ContentVerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(ContentVerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty IsDeferredScrollingEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsDeferredScrollingEnabled",
            typeof(bool),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CanContentScrollProperty =
        DependencyProperty.RegisterAttached(
            "CanContentScroll",
            typeof(bool),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollViewer viewer)
                    {
                        viewer._contentPresenter.HookupScrollInfo();
                    }
                }));

    public static readonly DependencyProperty IsTransformContentLayerStableProperty =
        DependencyProperty.RegisterAttached(
            "IsTransformContentLayerStable",
            typeof(bool),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    element.InvalidateVisual();
                    if (element.VisualParent is ScrollViewer visualViewer)
                    {
                        visualViewer.InvalidateVisual();
                    }
                    else if (element.LogicalParent is ScrollViewer logicalViewer)
                    {
                        logicalViewer.InvalidateVisual();
                    }
                }));

    public static readonly DependencyProperty ScrollBarThicknessProperty =
        DependencyProperty.Register(
            nameof(ScrollBarThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(12f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PanningModeProperty =
        DependencyProperty.Register(
            nameof(PanningMode),
            typeof(PanningMode),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(PanningMode.None));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public event EventHandler? ViewportChanged;

    public static readonly RoutedEvent ScrollChangedEvent = new(nameof(ScrollChanged), RoutingStrategy.Bubble);

    public event EventHandler<ScrollChangedEventArgs> ScrollChanged
    {
        add => AddHandler(ScrollChangedEvent, value);
        remove => RemoveHandler(ScrollChangedEvent, value);
    }

    static ScrollViewer()
    {
        EventManager.RegisterClassHandler<ScrollViewer, MouseWheelRoutedEventArgs>(
            UIElement.MouseWheelEvent,
            static (viewer, args) =>
            {
                if (!args.Handled && viewer.HandleMouseWheelFromInput(args.Delta))
                {
                    args.Handled = true;
                }
            });

        EventManager.RegisterClassHandler<ScrollViewer, KeyRoutedEventArgs>(
            UIElement.KeyDownEvent,
            static (viewer, args) => viewer.HandleKeyDownFromInput(args),
            handledEventsToo: false);

        EventManager.RegisterClassHandler<ScrollViewer, RequestBringIntoViewEventArgs>(
            UIElement.RequestBringIntoViewEvent,
            static (viewer, args) =>
            {
                if (!args.Handled && viewer.MakeVisible(args.TargetObject, args.TargetRect))
                {
                    args.Handled = true;
                }
            });
    }

    public ScrollViewer()
    {
        _contentPresenter = new ScrollViewerContentPresenter(this);
        _horizontalBar = new ScrollBar { Orientation = Orientation.Horizontal };
        _verticalBar = new ScrollBar { Orientation = Orientation.Vertical };
        _horizontalBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        _verticalBar.ValueChanged += OnVerticalScrollBarValueChanged;
        _horizontalBar.ThumbDragCompleted += OnScrollBarThumbDragCompleted;
        _verticalBar.ThumbDragCompleted += OnScrollBarThumbDragCompleted;
        Loaded += (_, _) => EnsureInternalScrollBarsLoaded();
        Unloaded += (_, _) => UnloadInternalScrollBars();
        SyncInternalScrollBarParents();
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    internal void SetInternalScrollBarLineButtonVisibility(bool showHorizontalLineButtons, bool showVerticalLineButtons)
    {
        var horizontalChanged = _horizontalBar.ShowLineButtons != showHorizontalLineButtons;
        var verticalChanged = _verticalBar.ShowLineButtons != showVerticalLineButtons;
        if (!horizontalChanged && !verticalChanged)
        {
            return;
        }

        _horizontalBar.ShowLineButtons = showHorizontalLineButtons;
        _verticalBar.ShowLineButtons = showVerticalLineButtons;
        InvalidateMeasure();
        InvalidateArrange();
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        private set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        private set => SetValue(VerticalOffsetProperty, value);
    }

    public float ExtentWidth
    {
        get => GetValue<float>(ExtentWidthProperty);
        private set => SetValue(ExtentWidthProperty, value);
    }

    public float ExtentHeight
    {
        get => GetValue<float>(ExtentHeightProperty);
        private set => SetValue(ExtentHeightProperty, value);
    }

    public float ViewportWidth
    {
        get => GetValue<float>(ViewportWidthProperty);
        private set => SetValue(ViewportWidthProperty, value);
    }

    public float ViewportHeight
    {
        get => GetValue<float>(ViewportHeightProperty);
        private set => SetValue(ViewportHeightProperty, value);
    }

    public float ScrollableWidth
    {
        get => GetValue<float>(ScrollableWidthProperty);
        private set => SetValue(ScrollableWidthProperty, value);
    }

    public float ScrollableHeight
    {
        get => GetValue<float>(ScrollableHeightProperty);
        private set => SetValue(ScrollableHeightProperty, value);
    }

    public Visibility ComputedHorizontalScrollBarVisibility
    {
        get => GetValue<Visibility>(ComputedHorizontalScrollBarVisibilityProperty);
        private set => SetValue(ComputedHorizontalScrollBarVisibilityProperty, value);
    }

    public Visibility ComputedVerticalScrollBarVisibility
    {
        get => GetValue<Visibility>(ComputedVerticalScrollBarVisibilityProperty);
        private set => SetValue(ComputedVerticalScrollBarVisibilityProperty, value);
    }

    public float ContentHorizontalOffset
    {
        get => GetValue<float>(ContentHorizontalOffsetProperty);
        private set => SetValue(ContentHorizontalOffsetProperty, value);
    }

    public float ContentVerticalOffset
    {
        get => GetValue<float>(ContentVerticalOffsetProperty);
        private set => SetValue(ContentVerticalOffsetProperty, value);
    }

    public bool IsDeferredScrollingEnabled
    {
        get => GetValue<bool>(IsDeferredScrollingEnabledProperty);
        set => SetValue(IsDeferredScrollingEnabledProperty, value);
    }

    public bool CanContentScroll
    {
        get => GetValue<bool>(CanContentScrollProperty);
        set => SetValue(CanContentScrollProperty, value);
    }

    public float ScrollBarThickness
    {
        get => GetValue<float>(ScrollBarThicknessProperty);
        set => SetValue(ScrollBarThicknessProperty, value);
    }

    public PanningMode PanningMode
    {
        get => GetValue<PanningMode>(PanningModeProperty);
        set => SetValue(PanningModeProperty, value);
    }

    public override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
    }

    public static bool GetIsDeferredScrollingEnabled(DependencyObject element)
    {
        return element.GetValue<bool>(IsDeferredScrollingEnabledProperty);
    }

    public static void SetIsDeferredScrollingEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsDeferredScrollingEnabledProperty, value);
    }

    public static bool GetCanContentScroll(DependencyObject element)
    {
        return element.GetValue<bool>(CanContentScrollProperty);
    }

    public static void SetCanContentScroll(DependencyObject element, bool value)
    {
        element.SetValue(CanContentScrollProperty, value);
    }

}
