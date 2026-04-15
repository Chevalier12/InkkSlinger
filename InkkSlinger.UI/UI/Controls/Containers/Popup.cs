using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Popup : ContentControl
{
    private static readonly List<Popup> OpenPopups = [];

    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(
            nameof(PlacementMode),
            typeof(PopupPlacementMode),
            typeof(Popup),
            new FrameworkPropertyMetadata(PopupPlacementMode.Absolute));

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(
            nameof(PlacementTarget),
            typeof(UIElement),
            typeof(Popup),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(Popup),
            new FrameworkPropertyMetadata("Popup", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(
            nameof(Left),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(100f));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register(
            nameof(Top),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(100f));

    public static readonly DependencyProperty TitleBarHeightProperty =
        DependencyProperty.Register(
            nameof(TitleBarHeight),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(34f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is float v && v >= 0f);

    public new static readonly DependencyProperty BackgroundProperty =
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

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Popup),
            new FrameworkPropertyMetadata(new Color(107, 182, 232), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Popup),
            new FrameworkPropertyMetadata(2f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is float v && v >= 0f);

    public new static readonly DependencyProperty PaddingProperty =
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
    private UIElement? _focusBeforeOpen;
    private UIElement? _pendingFocusRestore;
    private bool _isOpen;
    private bool _isCloseHovered;
    private bool _isClosePressed;
    private bool _isDragging;
    private bool _isReconcilingDescendantMeasureInvalidation;
    private Vector2 _dragPointerOffset;
    private bool _coercingPlacement;
    private float _resolvedLeft;
    private float _resolvedTop;
    private bool _hasResolvedPlacement;

    public string Title
    {
        get => GetValue<string>(TitleProperty) ?? string.Empty;
        set => SetValue(TitleProperty, value);
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

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color TitleBarBackground
    {
        get => GetValue<Color>(TitleBarBackgroundProperty);
        set => SetValue(TitleBarBackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
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

    internal static Panel? ResolveOverlayHost(UIElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        Panel? fallbackHost = null;
        for (var current = owner;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                fallbackHost = panel;
            }
        }

        return fallbackHost;
    }

    internal static void CloseAnchoredPopupsWithin(UIElement ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);

        if (OpenPopups.Count == 0)
        {
            return;
        }

        Popup[] snapshot;
        lock (OpenPopups)
        {
            snapshot = OpenPopups.ToArray();
        }

        for (var i = 0; i < snapshot.Length; i++)
        {
            var popup = snapshot[i];
            if (!popup._isOpen || popup.PlacementMode == PopupPlacementMode.Absolute)
            {
                continue;
            }

            var placementTarget = popup.PlacementTarget;
            if (placementTarget == null || !IsElementDescendantOf(placementTarget, ancestor))
            {
                continue;
            }

            popup.Close();
        }
    }

    public Popup()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    public void Open(Panel host)
    {
        Dispatcher.VerifyAccess();
        if (_isOpen)
        {
            Activate();
            return;
        }

        _focusBeforeOpen = FocusManager.GetFocusedElement();
        _host = host;
        _host.LayoutUpdated += OnHostLayoutUpdated;
        host.AddChild(this);
        _isOpen = true;
        lock (OpenPopups)
        {
            OpenPopups.Add(this);
        }

        Activate();
        UpdatePlacement();
        UiRoot.Current?.NotifyOverlayOpened(this);
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Show(Panel host)
    {
        Open(host);
    }

    public void ShowCentered(Panel host, float width, float height)
    {
        Width = width;
        Height = height;
        Left = MathF.Max(0f, (host.LayoutSlot.Width - width) / 2f);
        Top = MathF.Max(0f, (host.LayoutSlot.Height - height) / 2f);
        Open(host);
    }

    public bool TrySetRootSpacePosition(float left, float top)
    {
        Dispatcher.VerifyAccess();
        if (_host == null)
        {
            return false;
        }

        var hostOrigin = ResolveRenderedOrigin(_host);
        var hostRelativeLeft = left - hostOrigin.X;
        var hostRelativeTop = top - hostOrigin.Y;
        if (MathF.Abs(hostRelativeLeft - Left) < 0.001f &&
            MathF.Abs(hostRelativeTop - Top) < 0.001f)
        {
            return true;
        }

        Left = hostRelativeLeft;
        Top = hostRelativeTop;
        return true;
    }

    public void Close()
    {
        Dispatcher.VerifyAccess();
        if (!_isOpen || _host == null)
        {
            return;
        }

        _isCloseHovered = false;
        _isClosePressed = false;
        _isDragging = false;
        _host.RemoveChild(this);
        _host.LayoutUpdated -= OnHostLayoutUpdated;
        _host = null;
        _isOpen = false;
        lock (OpenPopups)
        {
            OpenPopups.Remove(this);
        }

        _pendingFocusRestore = _focusBeforeOpen;
        _focusBeforeOpen = null;
        UiRoot.Current?.NotifyOverlayClosed(this);

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

    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        if (base.TryHandleMeasureInvalidation(origin, source, reason))
        {
            return true;
        }

        if (Template != null || HasTemplateRoot ||
            ReferenceEquals(origin, this) ||
            _isReconcilingDescendantMeasureInvalidation ||
            NeedsMeasure ||
            ContentElement is not FrameworkElement content ||
            !IsDescendantOfContentSubtree(origin))
        {
            return false;
        }

        var availableSize = PreviousAvailableSizeForTests;
        if (float.IsNaN(availableSize.X) || float.IsNaN(availableSize.Y))
        {
            return false;
        }

        _isReconcilingDescendantMeasureInvalidation = true;
        try
        {
            var availableContentSize = GetAvailableContentSizeForMeasure(availableSize, content);
            content.Measure(availableContentSize);
            if (content.NeedsMeasure || !content.IsMeasureValidForTests)
            {
                return false;
            }

            var chromeHorizontal = GetChromeHorizontal();
            var chromeVertical = GetChromeVertical();
            var minTitleWidth = !string.IsNullOrWhiteSpace(Title)
                ? TextLayout.Layout(Title, UiTextRenderer.ResolveTypography(this, FontSize), FontSize, float.PositiveInfinity, TextWrapping.NoWrap).Size.X + 96f
                : 120f;
            var desiredSize = ResolveDesiredSizeFromMeasuredSizeForLocalReconciliation(new Vector2(
                MathF.Max(minTitleWidth, content.DesiredSize.X + chromeHorizontal),
                content.DesiredSize.Y + chromeVertical));
            if (!AreLocalLayoutSizesClose(desiredSize, DesiredSize))
            {
                return false;
            }

            MarkMeasureValidAfterLocalReconciliation();
            InvalidateArrangeForDirectLayoutOnly();
            return true;
        }
        finally
        {
            _isReconcilingDescendantMeasureInvalidation = false;
        }
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        if (base.ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(descendant))
        {
            return true;
        }

        return (_isReconcilingDescendantMeasureInvalidation || IsMeasuring || IsArrangingOverride) &&
               IsDescendantOfContentSubtree(descendant);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (Template != null || HasTemplateRoot)
        {
            var desired = base.MeasureOverride(availableSize);
            if (HasTemplateRoot)
            {
                return desired;
            }
        }

        var measured = Vector2.Zero;
        if (ContentElement is FrameworkElement content)
        {
            var availableContentSize = GetAvailableContentSizeForMeasure(availableSize, content);
            content.Measure(availableContentSize);
            measured = content.DesiredSize;
        }

        var chromeHorizontal = GetChromeHorizontal();
        var chromeVertical = GetChromeVertical();

        var minTitleWidth = !string.IsNullOrWhiteSpace(Title)
            ? TextLayout.Layout(Title, UiTextRenderer.ResolveTypography(this, FontSize), FontSize, float.PositiveInfinity, TextWrapping.NoWrap).Size.X + 96f
            : 120f;

        var desiredWidth = MathF.Max(minTitleWidth, measured.X + chromeHorizontal);
        var desiredHeight = measured.Y + chromeVertical;
        return new Vector2(desiredWidth, desiredHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (Template != null || HasTemplateRoot)
        {
            base.ArrangeOverride(finalSize);
            if (HasTemplateRoot)
            {
                return finalSize;
            }
        }

        if (ContentElement is FrameworkElement content)
        {
            var contentRect = GetContentRect(finalSize);
            if (content.TryTranslateArrangedSubtree(contentRect))
            {
                content.InvalidateVisual();
            }
            else if (RequiresContentArrange(content, contentRect))
            {
                content.Arrange(contentRect);
            }
        }

        return finalSize;
    }

    protected override LayoutRect ResolveArrangeRect(LayoutRect finalRect, LayoutRect arrangedRect)
    {
        if (_host == null)
        {
            return base.ResolveArrangeRect(finalRect, arrangedRect);
        }

        return new LayoutRect(
            finalRect.X + _resolvedLeft,
            finalRect.Y + _resolvedTop,
            arrangedRect.Width,
            arrangedRect.Height);
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        if (Template != null || HasTemplateRoot)
        {
            return base.CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
        }

        if (AreClose(previousAvailableSize.X, nextAvailableSize.X) &&
            AreClose(previousAvailableSize.Y, nextAvailableSize.Y))
        {
            return true;
        }

        if (ContentElement is not FrameworkElement content)
        {
            return true;
        }

        var previousContentAvailableSize = GetAvailableContentSizeForMeasure(previousAvailableSize, content);
        var nextContentAvailableSize = GetAvailableContentSizeForMeasure(nextAvailableSize, content);
        if (AreClose(previousContentAvailableSize.X, nextContentAvailableSize.X) &&
            AreClose(previousContentAvailableSize.Y, nextContentAvailableSize.Y))
        {
            return true;
        }

        return content.CanReuseMeasureForAvailableSizeChangeForParentLayout(
            previousContentAvailableSize,
            nextContentAvailableSize);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        if (HasTemplateRoot)
        {
            return;
        }

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
            var closeColor = _isClosePressed
                ? Blend(CloseButtonBackground, Color.Black, 0.18f)
                : _isCloseHovered
                ? Blend(CloseButtonBackground, Color.White, 0.2f)
                : CloseButtonBackground;
            UiDrawing.DrawFilledRect(spriteBatch, closeRect, closeColor, Opacity);

            var text = "X";
            var closeLayout = TextLayout.Layout(text, UiTextRenderer.ResolveTypography(this, FontSize), FontSize, float.PositiveInfinity, TextWrapping.NoWrap);
            var textX = closeRect.X + ((closeRect.Width - closeLayout.Size.X) / 2f);
            var textY = closeRect.Y + ((closeRect.Height - closeLayout.Size.Y) / 2f);
            UiTextRenderer.DrawString(spriteBatch, this, text, new Vector2(textX, textY), CloseButtonForeground * Opacity, FontSize);
        }

        if (!string.IsNullOrWhiteSpace(Title))
        {
            var titleLayout = TextLayout.Layout(Title, UiTextRenderer.ResolveTypography(this, FontSize), FontSize, MathF.Max(0f, slot.Width - 72f), TextWrapping.WrapWithOverflow);
            var textPosition = new Vector2(slot.X + 10f, slot.Y + MathF.Max(4f, (TitleBarHeight - titleLayout.Size.Y) / 2f));
            for (var i = 0; i < titleLayout.Lines.Count; i++)
            {
                var line = titleLayout.Lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                var linePosition = new Vector2(textPosition.X, textPosition.Y + (i * UiTextRenderer.GetLineHeight(this, FontSize)));
                UiTextRenderer.DrawString(spriteBatch, this, line, linePosition, TitleForeground * Opacity, FontSize);
            }
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

    private float GetChromeHorizontal()
    {
        var border = BorderThickness;
        var padding = Padding;
        return (border * 2f) + padding.Horizontal;
    }

    private float GetChromeVertical()
    {
        var border = BorderThickness;
        var padding = Padding;
        return (border * 2f) + TitleBarHeight + padding.Vertical;
    }

    private Vector2 GetAvailableContentSize(Vector2 availableSize)
    {
        return new Vector2(
            MathF.Max(0f, availableSize.X - GetChromeHorizontal()),
            MathF.Max(0f, availableSize.Y - GetChromeVertical()));
    }

    private Vector2 GetAvailableContentSizeForMeasure(Vector2 availableSize, FrameworkElement content)
    {
        var constrainedAvailableSize = availableSize;

        if (!float.IsNaN(Width) && Width > 0f)
        {
            constrainedAvailableSize.X = MathF.Min(constrainedAvailableSize.X, Width);
        }

        if (!float.IsNaN(Height) && Height > 0f)
        {
            constrainedAvailableSize.Y = MathF.Min(constrainedAvailableSize.Y, Height);
        }

        if (ShouldConstrainContentMeasureToCurrentViewport(content))
        {
            var currentWidth = ResolveCurrentDimension(Width, ActualWidth, DesiredSize.X);
            var currentHeight = ResolveCurrentDimension(Height, ActualHeight, DesiredSize.Y);
            if (currentWidth > 0f)
            {
                constrainedAvailableSize.X = MathF.Min(constrainedAvailableSize.X, currentWidth);
            }

            if (currentHeight > 0f)
            {
                constrainedAvailableSize.Y = MathF.Min(constrainedAvailableSize.Y, currentHeight);
            }
        }

        return GetAvailableContentSize(constrainedAvailableSize);
    }

    private LayoutRect GetContentRect(Vector2 finalSize)
    {
        var border = BorderThickness;
        var padding = Padding;
        return new LayoutRect(
            LayoutSlot.X + border + padding.Left,
            LayoutSlot.Y + border + TitleBarHeight + padding.Top,
            MathF.Max(0f, finalSize.X - GetChromeHorizontal()),
            MathF.Max(0f, finalSize.Y - GetChromeVertical()));
    }

    private static bool RequiresContentArrange(FrameworkElement content, LayoutRect targetRect)
    {
        return content.NeedsMeasure ||
               content.NeedsArrange ||
               !AreLayoutRectsClose(content.LayoutSlot, targetRect);
    }

    private bool ShouldConstrainContentMeasureToCurrentViewport(FrameworkElement content)
    {
        if (!_isOpen)
        {
            return false;
        }

        if (content.NeedsMeasure ||
            content.HasPendingMeasureInvalidationInVisualSubtreeForLayout())
        {
            return false;
        }

        return ActualWidth > 0f ||
               ActualHeight > 0f ||
               (!float.IsNaN(Width) && Width > 0f) ||
               (!float.IsNaN(Height) && Height > 0f);
    }

    private static bool AreLayoutRectsClose(LayoutRect left, LayoutRect right)
    {
        return AreClose(left.X, right.X) &&
               AreClose(left.Y, right.Y) &&
               AreClose(left.Width, right.Width) &&
               AreClose(left.Height, right.Height);
    }

    private bool IsDescendantOfContentSubtree(UIElement descendant)
    {
        if (ContentElement == null)
        {
            return false;
        }

        for (UIElement? current = descendant; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, this) || ReferenceEquals(current, ContentElement))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
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

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (!_isOpen || !HitTest(pointerPosition))
        {
            return false;
        }

        if (CanClose && TitleBarHeight > 0f)
        {
            var onClose = Contains(GetCloseButtonRect(), pointerPosition.X, pointerPosition.Y);
            _isCloseHovered = onClose;
            _isClosePressed = onClose;
            if (onClose)
            {
                InvalidateVisual();
                return true;
            }
        }

        if (CanDragMove && IsInTitleBar(pointerPosition))
        {
            if (PlacementMode != PopupPlacementMode.Absolute)
            {
                // Dragging should behave like moving a window surface, so detach from
                // anchored placement and continue in absolute coordinates.
                var hostX = _host?.LayoutSlot.X ?? 0f;
                var hostY = _host?.LayoutSlot.Y ?? 0f;
                Left = LayoutSlot.X - hostX;
                Top = LayoutSlot.Y - hostY;
                PlacementMode = PopupPlacementMode.Absolute;
                PlacementTarget = null;
                HorizontalOffset = 0f;
                VerticalOffset = 0f;
            }

            _isDragging = true;
            var hostXNow = _host?.LayoutSlot.X ?? 0f;
            var hostYNow = _host?.LayoutSlot.Y ?? 0f;
            _dragPointerOffset = new Vector2(
                pointerPosition.X - (hostXNow + Left),
                pointerPosition.Y - (hostYNow + Top));
            return true;
        }

        return false;
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!_isOpen)
        {
            return;
        }

        if (_isDragging && _host != null)
        {
            Left = pointerPosition.X - _host.LayoutSlot.X - _dragPointerOffset.X;
            Top = pointerPosition.Y - _host.LayoutSlot.Y - _dragPointerOffset.Y;
        }

        if (CanClose && TitleBarHeight > 0f)
        {
            var isHovered = Contains(GetCloseButtonRect(), pointerPosition.X, pointerPosition.Y);
            if (_isCloseHovered != isHovered)
            {
                _isCloseHovered = isHovered;
                InvalidateVisual();
            }
        }
    }

    internal void HandlePointerUpFromInput(Vector2 pointerPosition)
    {
        if (!_isOpen)
        {
            return;
        }

        var shouldClose = _isClosePressed &&
                          CanClose &&
                          TitleBarHeight > 0f &&
                          Contains(GetCloseButtonRect(), pointerPosition.X, pointerPosition.Y);
        _isClosePressed = false;
        _isDragging = false;
        _isCloseHovered = CanClose && TitleBarHeight > 0f && Contains(GetCloseButtonRect(), pointerPosition.X, pointerPosition.Y);
        InvalidateVisual();

        if (shouldClose)
        {
            Close();
        }
    }

    internal bool TryConsumePendingFocusRestore(out UIElement? element)
    {
        element = _pendingFocusRestore;
        _pendingFocusRestore = null;
        return element != null;
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
        var hasPlacementTargetBounds = TryGetPlacementTargetBoundsRelativeToHost(out var targetBounds);
        switch (PlacementMode)
        {
            case PopupPlacementMode.Center:
                computedLeft = ((hostWidth - currentWidth) / 2f) + HorizontalOffset;
                computedTop = ((hostHeight - currentHeight) / 2f) + VerticalOffset;
                break;
            case PopupPlacementMode.Top:
                if (hasPlacementTargetBounds)
                {
                    computedLeft = targetBounds.X + HorizontalOffset;
                    computedTop = targetBounds.Y - currentHeight + VerticalOffset;
                }
                else
                {
                    // Without a placement target, keep explicit coordinates stable.
                    computedLeft = Left + HorizontalOffset;
                    computedTop = Top + VerticalOffset;
                }

                break;
            case PopupPlacementMode.Bottom:
                if (hasPlacementTargetBounds)
                {
                    computedLeft = targetBounds.X + HorizontalOffset;
                    computedTop = targetBounds.Y + targetBounds.Height + VerticalOffset;
                }
                else
                {
                    computedLeft = Left + HorizontalOffset;
                    computedTop = Top + VerticalOffset;
                }

                break;
            case PopupPlacementMode.Left:
                if (hasPlacementTargetBounds)
                {
                    computedLeft = targetBounds.X - currentWidth + HorizontalOffset;
                    computedTop = targetBounds.Y + VerticalOffset;
                }
                else
                {
                    // Without a placement target, keep explicit coordinates stable.
                    computedLeft = Left + HorizontalOffset;
                    computedTop = Top + VerticalOffset;
                }

                break;
            case PopupPlacementMode.Right:
                if (hasPlacementTargetBounds)
                {
                    computedLeft = targetBounds.X + targetBounds.Width + HorizontalOffset;
                    computedTop = targetBounds.Y + VerticalOffset;
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
        if (_hasResolvedPlacement &&
            MathF.Abs(clampedLeft - _resolvedLeft) < 0.001f &&
            MathF.Abs(clampedTop - _resolvedTop) < 0.001f &&
            MathF.Abs(Left - clampedLeft) < 0.001f &&
            MathF.Abs(Top - clampedTop) < 0.001f)
        {
            return;
        }

        _coercingPlacement = true;
        try
        {
            _resolvedLeft = clampedLeft;
            _resolvedTop = clampedTop;
            _hasResolvedPlacement = true;

            if (MathF.Abs(Left - clampedLeft) >= 0.001f)
            {
                Left = clampedLeft;
            }

            if (MathF.Abs(Top - clampedTop) >= 0.001f)
            {
                Top = clampedTop;
            }

            InvalidateArrangeForDirectLayoutOnly();
        }
        finally
        {
            _coercingPlacement = false;
        }
    }

    private bool TryGetPlacementTargetBoundsRelativeToHost(out LayoutRect bounds)
    {
        bounds = default;
        if (_host == null || PlacementTarget == null)
        {
            return false;
        }

        var hostOrigin = ResolveRenderedOrigin(_host);
        var targetBounds = ResolveRenderedBounds(PlacementTarget);
        bounds = new LayoutRect(
            targetBounds.X - hostOrigin.X,
            targetBounds.Y - hostOrigin.Y,
            targetBounds.Width,
            targetBounds.Height);
        return true;
    }

    private static LayoutRect ResolveRenderedBounds(UIElement element)
    {
        var bounds = element.LayoutSlot;
        if (!TryGetTransformFromThisToRoot(element, out var transform))
        {
            return bounds;
        }

        return TransformRect(bounds, transform);
    }

    private static Vector2 ResolveRenderedOrigin(UIElement element)
    {
        var origin = new Vector2(element.LayoutSlot.X, element.LayoutSlot.Y);
        if (!TryGetTransformFromThisToRoot(element, out var transform))
        {
            return origin;
        }

        return Vector2.Transform(origin, transform);
    }

    private static bool TryGetTransformFromThisToRoot(UIElement? element, out Matrix transform)
    {
        transform = Matrix.Identity;
        var hasTransform = false;
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        return hasTransform;
    }

    private static LayoutRect TransformRect(LayoutRect rect, Matrix transform)
    {
        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
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


