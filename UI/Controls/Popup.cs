using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Popup : ContentControl
{
    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(
            nameof(PlacementMode),
            typeof(PopupPlacementMode),
            typeof(Popup),
            new FrameworkPropertyMetadata(PopupPlacementMode.Absolute, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(
            nameof(PlacementTarget),
            typeof(UIElement),
            typeof(Popup),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(Popup),
            new FrameworkPropertyMetadata("Popup", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(Popup),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(
            nameof(Left),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(100f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register(
            nameof(Top),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(100f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TitleBarHeightProperty =
        DependencyProperty.Register(
            nameof(TitleBarHeight),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(34f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is float v && v >= 0f);

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(18, 23, 32), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TitleBarBackgroundProperty =
        DependencyProperty.Register(
            nameof(TitleBarBackground),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(39, 54, 73), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(107, 182, 232), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(2f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is float v && v >= 0f);

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Thickness(8f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TitleForegroundProperty =
        DependencyProperty.Register(
            nameof(TitleForeground),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(236, 244, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CloseButtonBackgroundProperty =
        DependencyProperty.Register(
            nameof(CloseButtonBackground),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(165, 70, 70), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CloseButtonForegroundProperty =
        DependencyProperty.Register(
            nameof(CloseButtonForeground),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CanDragMoveProperty =
        DependencyProperty.Register(
            nameof(CanDragMove),
            typeof(bool),
            typeof(Popup),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty CanCloseProperty =
        DependencyProperty.Register(
            nameof(CanClose),
            typeof(bool),
            typeof(Popup),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DismissOnOutsideClickProperty =
        DependencyProperty.Register(
            nameof(DismissOnOutsideClick),
            typeof(bool),
            typeof(Popup),
            new FrameworkPropertyMetadata(false));

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    private Panel? _host;
    private bool _isOpen;
    private bool _isDragging;
    private bool _isCloseHovered;
    private bool _closePressed;
    private bool _updatingPlacement;
    private bool _coercingPlacement;
    private Vector2 _dragPointerOffset;
    private UIElement? _focusRestoreTarget;

    public string Title
    {
        get => GetValue<string>(TitleProperty) ?? string.Empty;
        set => SetValue(TitleProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
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

    public float TitleBarHeight
    {
        get => GetValue<float>(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color TitleBarBackground
    {
        get => GetValue<Color>(TitleBarBackgroundProperty);
        set => SetValue(TitleBarBackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Color TitleForeground
    {
        get => GetValue<Color>(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }

    public Color CloseButtonBackground
    {
        get => GetValue<Color>(CloseButtonBackgroundProperty);
        set => SetValue(CloseButtonBackgroundProperty, value);
    }

    public Color CloseButtonForeground
    {
        get => GetValue<Color>(CloseButtonForegroundProperty);
        set => SetValue(CloseButtonForegroundProperty, value);
    }

    public bool CanDragMove
    {
        get => GetValue<bool>(CanDragMoveProperty);
        set => SetValue(CanDragMoveProperty, value);
    }

    public bool CanClose
    {
        get => GetValue<bool>(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
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

    public bool DismissOnOutsideClick
    {
        get => GetValue<bool>(DismissOnOutsideClickProperty);
        set => SetValue(DismissOnOutsideClickProperty, value);
    }

    public bool IsOpen => _isOpen;

    public Popup()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Focusable = true;
        SyncMarginFromPlacement();
    }

    public void Show(Panel host)
    {
        Dispatcher.VerifyAccess();
        if (_isOpen)
        {
            Activate();
            Focus();
            return;
        }

        _focusRestoreTarget = FocusManager.FocusedElement;
        _host = host;
        _host.LayoutUpdated += OnHostLayoutUpdated;
        _host.PreviewMouseDown += OnHostPreviewMouseDown;
        _host.PreviewKeyDown += OnHostPreviewKeyDown;
        host.AddChild(this);
        _isOpen = true;
        Activate();
        UpdatePlacement();
        Focus();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void ShowCentered(Panel host, float width, float height)
    {
        Width = width;
        Height = height;
        Left = MathF.Max(0f, (host.LayoutSlot.Width - width) / 2f);
        Top = MathF.Max(0f, (host.LayoutSlot.Height - height) / 2f);
        Show(host);
    }

    public void Close()
    {
        Dispatcher.VerifyAccess();
        if (!_isOpen || _host == null)
        {
            return;
        }

        ReleaseMouseCapture();
        _isDragging = false;
        _closePressed = false;
        _isCloseHovered = false;

        _host.RemoveChild(this);
        _host.LayoutUpdated -= OnHostLayoutUpdated;
        _host.PreviewMouseDown -= OnHostPreviewMouseDown;
        _host.PreviewKeyDown -= OnHostPreviewKeyDown;
        _host = null;
        _isOpen = false;

        var shouldRestoreFocus = FocusManager.FocusedElement != null && IsSelfOrDescendant(FocusManager.FocusedElement);
        if (shouldRestoreFocus && _focusRestoreTarget != null)
        {
            if (!FocusManager.SetFocusedElement(_focusRestoreTarget))
            {
                FocusManager.SetFocusedElement(null);
            }
        }
        else if (shouldRestoreFocus)
        {
            FocusManager.SetFocusedElement(null);
        }

        _focusRestoreTarget = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Activate()
    {
        Dispatcher.VerifyAccess();
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

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.Property == ContentProperty &&
            args.NewValue != null &&
            args.NewValue is not UIElement)
        {
            throw new InvalidOperationException("Popup.Content must be a UIElement.");
        }

        if (!_updatingPlacement &&
            (args.Property == LeftProperty || args.Property == TopProperty) &&
            PlacementMode == PopupPlacementMode.Absolute)
        {
            SyncMarginFromPlacement();
        }

        base.OnDependencyPropertyChanged(args);

        if (_host == null)
        {
            return;
        }

        if (args.Property == LeftProperty ||
            args.Property == TopProperty ||
            args.Property == PlacementModeProperty ||
            args.Property == PlacementTargetProperty ||
            args.Property == HorizontalOffsetProperty ||
            args.Property == VerticalOffsetProperty ||
            args.Property == WidthProperty ||
            args.Property == HeightProperty)
        {
            UpdatePlacement();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var border = BorderThickness;
        var padding = Padding;
        var chromeHorizontal = (border * 2f) + padding.Horizontal;
        var chromeVertical = (border * 2f) + TitleBarHeight + padding.Vertical;

        var measured = Vector2.Zero;
        if (ContentElement is FrameworkElement content)
        {
            content.Measure(new Vector2(
                MathF.Max(0f, availableSize.X - chromeHorizontal),
                MathF.Max(0f, availableSize.Y - chromeVertical)));
            measured = content.DesiredSize;
        }

        var minTitleWidth = Font != null && !string.IsNullOrWhiteSpace(Title)
            ? TextLayout.Layout(Title, Font, float.PositiveInfinity, TextWrapping.NoWrap).Size.X + 96f
            : 120f;

        var desiredWidth = MathF.Max(minTitleWidth, measured.X + chromeHorizontal);
        var desiredHeight = measured.Y + chromeVertical;
        return new Vector2(desiredWidth, desiredHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var padding = Padding;

        if (ContentElement is FrameworkElement content)
        {
            var contentX = LayoutSlot.X + border + padding.Left;
            var contentY = LayoutSlot.Y + border + TitleBarHeight + padding.Top;
            var contentWidth = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);
            var contentHeight = MathF.Max(0f, finalSize.Y - (border * 2f) - TitleBarHeight - padding.Vertical);
            content.Arrange(new LayoutRect(contentX, contentY, contentWidth, contentHeight));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var border = BorderThickness;

        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(slot.X, slot.Y, slot.Width, TitleBarHeight),
            TitleBarBackground,
            Opacity);

        if (border > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, slot.Width, border), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - border, slot.Width, border),
                BorderBrush,
                Opacity);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, border, slot.Height), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - border, slot.Y, border, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (CanClose && TitleBarHeight > 0f)
        {
            var closeRect = GetCloseButtonRect();
            var closeColor = _isCloseHovered
                ? Blend(CloseButtonBackground, Color.White, 0.2f)
                : CloseButtonBackground;
            UiDrawing.DrawFilledRect(spriteBatch, closeRect, closeColor, Opacity);

            if (Font != null)
            {
                var text = "X";
                var closeLayout = TextLayout.Layout(text, Font, float.PositiveInfinity, TextWrapping.NoWrap);
                var textX = closeRect.X + ((closeRect.Width - closeLayout.Size.X) / 2f);
                var textY = closeRect.Y + ((closeRect.Height - closeLayout.Size.Y) / 2f);
                FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(textX, textY), CloseButtonForeground * Opacity);
            }
        }

        if (Font != null && !string.IsNullOrWhiteSpace(Title))
        {
            var titleLayout = TextLayout.Layout(Title, Font, MathF.Max(0f, slot.Width - 72f), TextWrapping.WrapWithOverflow);
            var textPosition = new Vector2(slot.X + 10f, slot.Y + MathF.Max(4f, (TitleBarHeight - titleLayout.Size.Y) / 2f));
            for (var i = 0; i < titleLayout.Lines.Count; i++)
            {
                var line = titleLayout.Lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                var linePosition = new Vector2(textPosition.X, textPosition.Y + (i * FontStashTextRenderer.GetLineHeight(Font)));
                FontStashTextRenderer.DrawString(spriteBatch, Font, line, linePosition, TitleForeground * Opacity);
            }
        }
    }

    protected override void OnMouseMove(RoutedMouseEventArgs args)
    {
        base.OnMouseMove(args);

        var pointer = args.Position;
        _isCloseHovered = CanClose && TitleBarHeight > 0f && Contains(GetCloseButtonRect(), pointer.X, pointer.Y);

        if (_isDragging && CanDragMove)
        {
            Left = pointer.X - _dragPointerOffset.X;
            Top = pointer.Y - _dragPointerOffset.Y;
            args.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);
        Activate();

        if (!IsEnabled)
        {
            return;
        }

        if (CanClose && TitleBarHeight > 0f && Contains(GetCloseButtonRect(), args.Position.X, args.Position.Y))
        {
            _closePressed = true;
            CaptureMouse();
            args.Handled = true;
            return;
        }

        if (CanDragMove && IsInTitleBar(args.Position))
        {
            _isDragging = true;
            _dragPointerOffset = new Vector2(args.Position.X - Left, args.Position.Y - Top);
            CaptureMouse();
            args.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);

        var closeHit = CanClose && TitleBarHeight > 0f && Contains(GetCloseButtonRect(), args.Position.X, args.Position.Y);
        var shouldClose = _closePressed && closeHit;
        _closePressed = false;

        _isDragging = false;
        if (InputManager.MouseCapturedElement == this)
        {
            ReleaseMouseCapture();
        }

        if (shouldClose)
        {
            Close();
            args.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        base.OnLostMouseCapture(args);
        _isDragging = false;
        _closePressed = false;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if ((CanClose || DismissOnOutsideClick) && args.Key == Keys.Escape)
        {
            Close();
            args.Handled = true;
        }
    }

    private bool IsInTitleBar(Vector2 point)
    {
        if (!Contains(LayoutSlot, point.X, point.Y))
        {
            return false;
        }

        if (TitleBarHeight <= 0f || point.Y > LayoutSlot.Y + TitleBarHeight)
        {
            return false;
        }

        if (CanClose && TitleBarHeight > 0f && Contains(GetCloseButtonRect(), point.X, point.Y))
        {
            return false;
        }

        return true;
    }

    private LayoutRect GetCloseButtonRect()
    {
        if (TitleBarHeight <= 0f)
        {
            return new LayoutRect(LayoutSlot.X, LayoutSlot.Y, 0f, 0f);
        }

        var size = MathF.Max(16f, TitleBarHeight - 8f);
        return new LayoutRect(
            LayoutSlot.X + LayoutSlot.Width - size - 6f,
            LayoutSlot.Y + ((TitleBarHeight - size) / 2f),
            size,
            size);
    }

    private static bool Contains(LayoutRect rect, float x, float y)
    {
        return x >= rect.X &&
               x <= rect.X + rect.Width &&
               y >= rect.Y &&
               y <= rect.Y + rect.Height;
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

    private void SyncMarginFromPlacement()
    {
        if (_host is Canvas)
        {
            var current = Margin;
            if (current.Left == 0f && current.Top == 0f && current.Right == 0f && current.Bottom == 0f)
            {
                return;
            }

            _updatingPlacement = true;
            try
            {
                Margin = new Thickness(0f);
            }
            finally
            {
                _updatingPlacement = false;
            }

            return;
        }

        _updatingPlacement = true;
        try
        {
            var margin = Margin;
            Margin = new Thickness(Left, Top, margin.Right, margin.Bottom);
        }
        finally
        {
            _updatingPlacement = false;
        }
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

    private void OnHostLayoutUpdated(object? sender, EventArgs e)
    {
        UpdatePlacement();
    }

    private void OnHostPreviewMouseDown(object? sender, RoutedMouseButtonEventArgs args)
    {
        if (!_isOpen || !DismissOnOutsideClick)
        {
            return;
        }

        var source = args.OriginalSource;
        if (source != null &&
            (IsSelfOrDescendant(source) ||
             (PlacementTarget != null && IsElementDescendantOf(source, PlacementTarget))))
        {
            return;
        }

        Close();
    }

    private void OnHostPreviewKeyDown(object? sender, RoutedKeyEventArgs args)
    {
        if (!_isOpen || args.Key != Keys.Escape || (!CanClose && !DismissOnOutsideClick))
        {
            return;
        }

        Close();
        args.Handled = true;
    }

    private void UpdatePlacement()
    {
        if (_host == null || _coercingPlacement)
        {
            return;
        }

        var hostWidth = MathF.Max(0f, _host.LayoutSlot.Width);
        var hostHeight = MathF.Max(0f, _host.LayoutSlot.Height);
        if (hostWidth <= 0f || hostHeight <= 0f)
        {
            return;
        }

        var currentWidth = ResolveCurrentDimension(Width, ActualWidth, DesiredSize.X);
        var currentHeight = ResolveCurrentDimension(Height, ActualHeight, DesiredSize.Y);

        var computedLeft = Left;
        var computedTop = Top;
        switch (PlacementMode)
        {
            case PopupPlacementMode.Center:
                computedLeft = ((hostWidth - currentWidth) / 2f) + HorizontalOffset;
                computedTop = ((hostHeight - currentHeight) / 2f) + VerticalOffset;
                break;
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
            case PopupPlacementMode.Absolute:
            default:
                computedLeft = Left + HorizontalOffset;
                computedTop = Top + VerticalOffset;
                break;
        }

        var maxLeft = MathF.Max(0f, hostWidth - currentWidth);
        var maxTop = MathF.Max(0f, hostHeight - currentHeight);

        var clampedLeft = Math.Clamp(computedLeft, 0f, maxLeft);
        var clampedTop = Math.Clamp(computedTop, 0f, maxTop);
        var requiresCanvasPositionSync = _host is Canvas;
        if (!requiresCanvasPositionSync &&
            MathF.Abs(clampedLeft - Left) < 0.001f &&
            MathF.Abs(clampedTop - Top) < 0.001f)
        {
            return;
        }

        _coercingPlacement = true;
        try
        {
            if (MathF.Abs(clampedLeft - Left) >= 0.001f)
            {
                Left = clampedLeft;
            }

            if (MathF.Abs(clampedTop - Top) >= 0.001f)
            {
                Top = clampedTop;
            }

            // Canvas arranges children via attached Canvas.Left/Top, not Margin.
            if (_host is Canvas)
            {
                SyncMarginFromPlacement();
                Canvas.SetLeft(this, clampedLeft);
                Canvas.SetTop(this, clampedTop);
            }
            else
            {
                // Non-canvas hosts position via Margin in the base panel layout.
                SyncMarginFromPlacement();
            }
        }
        finally
        {
            _coercingPlacement = false;
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

    private static bool IsElementDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
