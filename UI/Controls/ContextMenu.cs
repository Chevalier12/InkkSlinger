using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ContextMenu : ListBox
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(
                false,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ContextMenu menu && args.NewValue is bool isOpen)
                    {
                        menu.OnIsOpenChanged(isOpen);
                    }
                }));

    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(
            nameof(StaysOpen),
            typeof(bool),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(
            nameof(PlacementMode),
            typeof(PopupPlacementMode),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(PopupPlacementMode.Absolute, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(
            nameof(PlacementTarget),
            typeof(UIElement),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(
            nameof(Left),
            typeof(float),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register(
            nameof(Top),
            typeof(float),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContextMenuProperty =
        DependencyProperty.RegisterAttached(
            "ContextMenu",
            typeof(ContextMenu),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is UIElement element)
                    {
                        OnAttachedContextMenuChanged(element, args.OldValue as ContextMenu, args.NewValue as ContextMenu);
                    }
                }));

    private static readonly Dictionary<UIElement, EventHandler<RoutedMouseButtonEventArgs>> AttachedOpenHandlers = new();

    private Panel? _host;
    private bool _isUpdatingIsOpen;
    private UIElement? _focusRestoreTarget;

    public event EventHandler? Opened;
    public event EventHandler? Closed;

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

    public ContextMenu()
    {
        Focusable = true;
        SelectionMode = SelectionMode.Single;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        BorderThickness = 1f;
        Background = new Color(23, 31, 44);
        BorderBrush = new Color(94, 139, 181);

        // Menu items (e.g., Button) can mark MouseUp handled. We still need to close.
        AddHandler<RoutedMouseButtonEventArgs>(MouseUpEvent, OnAnyMouseUp, handledEventsToo: true);
    }

    public void Open(Panel host)
    {
        if (_host != null && !ReferenceEquals(_host, host))
        {
            CloseCore();
        }

        _host = host;
        if (IsOpen)
        {
            OpenCore();
            return;
        }

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
    }

    public static void SetContextMenu(UIElement element, ContextMenu? value)
    {
        element.SetValue(ContextMenuProperty, value);
    }

    public static ContextMenu? GetContextMenu(UIElement element)
    {
        return element.GetValue<ContextMenu>(ContextMenuProperty);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (!IsOpen || _host == null)
        {
            return;
        }

        if (args.Property == PlacementModeProperty ||
            args.Property == PlacementTargetProperty ||
            args.Property == HorizontalOffsetProperty ||
            args.Property == VerticalOffsetProperty ||
            args.Property == LeftProperty ||
            args.Property == TopProperty ||
            args.Property == WidthProperty ||
            args.Property == HeightProperty)
        {
            UpdatePlacement();
        }
    }

    protected override void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (args.Key == Keys.Escape && IsOpen)
        {
            Close();
            args.Handled = true;
        }
    }

    private void OnIsOpenChanged(bool isOpen)
    {
        if (_isUpdatingIsOpen)
        {
            return;
        }

        if (isOpen)
        {
            if (_host == null && PlacementTarget != null)
            {
                _host = FindHostPanel(PlacementTarget);
            }

            if (_host == null)
            {
                SetIsOpenCore(false);
                return;
            }

            OpenCore();
            return;
        }

        CloseCore();
    }

    private void OpenCore()
    {
        if (_host == null)
        {
            return;
        }

        if (HostContainsSelf())
        {
            UpdatePlacement();
            Activate();
            Focus();
            return;
        }

        _focusRestoreTarget = FocusManager.FocusedElement;
        _host.LayoutUpdated += OnHostLayoutUpdated;
        _host.PreviewMouseDown += OnHostPreviewMouseDown;
        _host.PreviewKeyDown += OnHostPreviewKeyDown;
        _host.AddChild(this);

        UpdatePlacement();
        Activate();
        Focus();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseCore()
    {
        if (_host == null)
        {
            return;
        }

        if (HostContainsSelf())
        {
            _host.RemoveChild(this);
        }

        _host.LayoutUpdated -= OnHostLayoutUpdated;
        _host.PreviewMouseDown -= OnHostPreviewMouseDown;
        _host.PreviewKeyDown -= OnHostPreviewKeyDown;

        if (_focusRestoreTarget != null)
        {
            FocusManager.SetFocusedElement(_focusRestoreTarget);
        }

        _focusRestoreTarget = null;
        _host = null;
        SelectedIndex = -1;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnHostLayoutUpdated(object? sender, EventArgs e)
    {
        UpdatePlacement();
    }

    private void OnHostPreviewMouseDown(object? sender, RoutedMouseButtonEventArgs args)
    {
        if (!IsOpen)
        {
            return;
        }

        var source = args.OriginalSource;
        if (source != null && IsSelfOrDescendant(source))
        {
            return;
        }

        Close();
    }

    private void OnHostPreviewKeyDown(object? sender, RoutedKeyEventArgs args)
    {
        if (!IsOpen || args.Key != Keys.Escape)
        {
            return;
        }

        Close();
        args.Handled = true;
    }

    private void Activate()
    {
        if (_host == null)
        {
            return;
        }

        var topZ = 0;
        foreach (var child in _host.Children)
        {
            topZ = Math.Max(topZ, Panel.GetZIndex(child));
        }

        Panel.SetZIndex(this, topZ + 1);
    }

    private void UpdatePlacement()
    {
        if (_host == null)
        {
            return;
        }

        var hostWidth = MathF.Max(0f, _host.LayoutSlot.Width);
        var hostHeight = MathF.Max(0f, _host.LayoutSlot.Height);
        if (hostWidth <= 0f || hostHeight <= 0f)
        {
            return;
        }

        var desiredWidth = ResolveCurrentDimension(Width, ActualWidth, DesiredSize.X);
        var desiredHeight = ResolveCurrentDimension(Height, ActualHeight, DesiredSize.Y);

        var computedLeft = Left;
        var computedTop = Top;
        switch (PlacementMode)
        {
            case PopupPlacementMode.Bottom:
                if (PlacementTarget != null)
                {
                    computedLeft = (PlacementTarget.LayoutSlot.X - _host.LayoutSlot.X) + HorizontalOffset;
                    computedTop = (PlacementTarget.LayoutSlot.Y - _host.LayoutSlot.Y) + PlacementTarget.LayoutSlot.Height + VerticalOffset;
                }
                else
                {
                    computedLeft = Left + HorizontalOffset;
                    computedTop = Top + VerticalOffset;
                }

                break;
            case PopupPlacementMode.Right:
                if (PlacementTarget != null)
                {
                    computedLeft = (PlacementTarget.LayoutSlot.X - _host.LayoutSlot.X) + PlacementTarget.LayoutSlot.Width + HorizontalOffset;
                    computedTop = (PlacementTarget.LayoutSlot.Y - _host.LayoutSlot.Y) + VerticalOffset;
                }
                else
                {
                    computedLeft = Left + HorizontalOffset;
                    computedTop = Top + VerticalOffset;
                }

                break;
            case PopupPlacementMode.Center:
                computedLeft = ((hostWidth - desiredWidth) / 2f) + HorizontalOffset;
                computedTop = ((hostHeight - desiredHeight) / 2f) + VerticalOffset;
                break;
            case PopupPlacementMode.Absolute:
            default:
                computedLeft = Left + HorizontalOffset;
                computedTop = Top + VerticalOffset;
                break;
        }

        var maxLeft = MathF.Max(0f, hostWidth - desiredWidth);
        var maxTop = MathF.Max(0f, hostHeight - desiredHeight);
        var clampedLeft = Math.Clamp(computedLeft, 0f, maxLeft);
        var clampedTop = Math.Clamp(computedTop, 0f, maxTop);

        if (_host is Canvas)
        {
            Canvas.SetLeft(this, clampedLeft);
            Canvas.SetTop(this, clampedTop);
            Margin = new Thickness(0f);
        }
        else
        {
            Margin = new Thickness(clampedLeft, clampedTop, 0f, 0f);
        }
    }

    private static float ResolveCurrentDimension(float explicitDimension, float actualDimension, float desiredDimension)
    {
        if (!float.IsNaN(explicitDimension) && explicitDimension > 0f)
        {
            return explicitDimension;
        }

        if (actualDimension > 0f)
        {
            return actualDimension;
        }

        return MathF.Max(0f, desiredDimension);
    }

    private static void OnAttachedContextMenuChanged(UIElement element, ContextMenu? oldMenu, ContextMenu? newMenu)
    {
        if (AttachedOpenHandlers.TryGetValue(element, out var existingHandler))
        {
            element.MouseRightButtonDown -= existingHandler;
            AttachedOpenHandlers.Remove(element);
        }

        if (newMenu == null)
        {
            return;
        }

        EventHandler<RoutedMouseButtonEventArgs> handler = (_, args) =>
        {
            if (!element.IsEnabled || !element.IsVisible)
            {
                return;
            }

            var host = FindHostPanel(element);
            if (host == null)
            {
                return;
            }

            var left = args.Position.X - host.LayoutSlot.X;
            var top = args.Position.Y - host.LayoutSlot.Y;
            newMenu.OpenAt(host, left, top, element);
            args.Handled = true;
        };

        element.MouseRightButtonDown += handler;
        AttachedOpenHandlers[element] = handler;
    }

    private static Panel? FindHostPanel(UIElement element)
    {
        for (var current = element.VisualParent ?? element.LogicalParent;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                return panel;
            }
        }

        return null;
    }

    private bool IsSelfOrDescendant(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private bool HostContainsSelf()
    {
        if (_host == null)
        {
            return false;
        }

        foreach (var child in _host.Children)
        {
            if (ReferenceEquals(child, this))
            {
                return true;
            }
        }

        return false;
    }

    private void SetIsOpenCore(bool value)
    {
        _isUpdatingIsOpen = true;
        try
        {
            IsOpen = value;
        }
        finally
        {
            _isUpdatingIsOpen = false;
        }
    }

    private void OnAnyMouseUp(object? sender, RoutedMouseButtonEventArgs args)
    {
        if (!IsOpen || StaysOpen || args.Button != MouseButton.Left)
        {
            return;
        }

        var source = args.OriginalSource;
        if (source == null || !IsSelfOrDescendant(source))
        {
            return;
        }

        // WPF-like behavior: click invocation in the menu closes it.
        Close();
    }
}
