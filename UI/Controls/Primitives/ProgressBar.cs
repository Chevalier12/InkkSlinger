using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ProgressBar : RangeBase, IUiRootUpdateParticipant
{
    private const float LayoutComparisonEpsilon = 0.0001f;

    public new static readonly DependencyProperty MinimumProperty = RangeBase.MinimumProperty;

    public new static readonly DependencyProperty MaximumProperty = RangeBase.MaximumProperty;

    public new static readonly DependencyProperty ValueProperty = RangeBase.ValueProperty;

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(24, 24, 24), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(72, 146, 210), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(98, 98, 98), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    private float _indeterminatePhase;
    private FrameworkElement? _track;
    private FrameworkElement? _indicator;
    private FrameworkElement? _glowRect;
    private TranslateTransform? _indicatorTranslateTransform;
    private TranslateTransform? _glowTranslateTransform;

    static ProgressBar()
    {
        MinimumProperty.OverrideMetadata(
            typeof(ProgressBar),
            CreateDerivedMetadata(MinimumProperty, 0f, FrameworkPropertyMetadataOptions.AffectsRender));
        MaximumProperty.OverrideMetadata(
            typeof(ProgressBar),
            CreateDerivedMetadata(MaximumProperty, 100f, FrameworkPropertyMetadataOptions.AffectsRender));
        ValueProperty.OverrideMetadata(
            typeof(ProgressBar),
            CreateDerivedMetadata(ValueProperty, 0f, FrameworkPropertyMetadataOptions.AffectsRender));
    }

    public ProgressBar()
    {
        IsHitTestVisible = false;
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public bool IsIndeterminate
    {
        get => GetValue<bool>(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return desired;
        }

        if (Orientation == Orientation.Horizontal)
        {
            desired.X = MathF.Max(desired.X, 120f);
            desired.Y = MathF.Max(desired.Y, 18f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, 18f);
            desired.Y = MathF.Max(desired.Y, 120f);
        }

        return desired;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateIndeterminateState(gameTime);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _track = GetTemplateChild("PART_Track") as FrameworkElement;
        _indicator = GetTemplateChild("PART_Indicator") as FrameworkElement;
        _glowRect = GetTemplateChild("PART_GlowRect") as FrameworkElement;
        _indicatorTranslateTransform = null;
        _glowTranslateTransform = null;

        PrepareTemplatePartForDirectLayout(_indicator);
        PrepareTemplatePartForDirectLayout(_glowRect);
        UpdateTrackClipState();

        UpdateVisualStates();
        SyncTemplateParts();
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => IsIndeterminate;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        UpdateIndeterminateState(gameTime);
    }

    private void UpdateIndeterminateState(GameTime gameTime)
    {
        if (!IsIndeterminate)
        {
            if (_indeterminatePhase != 0f)
            {
                _indeterminatePhase = 0f;
                SyncTemplateParts();
            }

            return;
        }

        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (deltaSeconds <= 0f)
        {
            return;
        }

        _indeterminatePhase += deltaSeconds * 0.9f;
        if (_indeterminatePhase > 1f)
        {
            _indeterminatePhase -= MathF.Floor(_indeterminatePhase);
        }

        SyncTemplateParts();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        if (CanUseTemplateParts())
        {
            return;
        }

        var slot = LayoutSlot;
        var border = BorderThickness;
        var inner = new LayoutRect(
            slot.X + border,
            slot.Y + border,
            MathF.Max(0f, slot.Width - (border * 2f)),
            MathF.Max(0f, slot.Height - (border * 2f)));

        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (border > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, border, BorderBrush, Opacity);
        }

        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        if (IsIndeterminate)
        {
            DrawIndeterminateFill(spriteBatch, inner);
            return;
        }

        var normalized = GetNormalizedValue();
        if (normalized <= 0f)
        {
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            var width = inner.Width * normalized;
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(inner.X, inner.Y, width, inner.Height),
                Foreground,
                Opacity);
            return;
        }

        var height = inner.Height * normalized;
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(inner.X, inner.Y + (inner.Height - height), inner.Width, height),
            Foreground,
            Opacity);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        SyncTemplateParts();
        return arranged;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == IsIndeterminateProperty ||
            args.Property == OrientationProperty ||
            args.Property == Validation.HasErrorProperty ||
            args.Property == Control.IsFocusedProperty)
        {
            UpdateVisualStates();
        }

        if (args.Property == IsIndeterminateProperty ||
            args.Property == OrientationProperty ||
            args.Property == ValueProperty ||
            args.Property == MinimumProperty ||
            args.Property == MaximumProperty)
        {
            if (args.Property == IsIndeterminateProperty)
            {
                UpdateTrackClipState();
            }

            SyncTemplateParts();
        }
    }

    protected override void OnMinimumChanged(float oldMinimum, float newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        SyncTemplateParts();
    }

    protected override void OnMaximumChanged(float oldMaximum, float newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        SyncTemplateParts();
    }

    protected override void OnValueChanged(float oldValue, float newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        SyncTemplateParts();
    }

    private void DrawIndeterminateFill(SpriteBatch spriteBatch, LayoutRect inner)
    {
        const float chunkRatio = 0.32f;

        if (Orientation == Orientation.Horizontal)
        {
            var segment = MathF.Max(6f, inner.Width * chunkRatio);
            var travel = inner.Width + segment;
            var start = inner.X + (_indeterminatePhase * travel) - segment;
            var end = start + segment;
            var visibleStart = MathF.Max(inner.X, start);
            var visibleEnd = MathF.Min(inner.X + inner.Width, end);
            var visibleWidth = MathF.Max(0f, visibleEnd - visibleStart);
            if (visibleWidth > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(visibleStart, inner.Y, visibleWidth, inner.Height),
                    Foreground,
                    Opacity);
            }

            return;
        }

        var segmentHeight = MathF.Max(6f, inner.Height * chunkRatio);
        var travelHeight = inner.Height + segmentHeight;
        var endY = inner.Y + inner.Height - (_indeterminatePhase * travelHeight) + segmentHeight;
        var startY = endY - segmentHeight;
        var visibleStartY = MathF.Max(inner.Y, startY);
        var visibleEndY = MathF.Min(inner.Y + inner.Height, endY);
        var visibleHeight = MathF.Max(0f, visibleEndY - visibleStartY);
        if (visibleHeight > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(inner.X, visibleStartY, inner.Width, visibleHeight),
                Foreground,
                Opacity);
        }
    }

    private float GetNormalizedValue()
    {
        var range = Maximum - Minimum;
        if (range <= 0f)
        {
            return 0f;
        }

        var normalized = (Value - Minimum) / range;
        return MathF.Max(0f, MathF.Min(1f, normalized));
    }

    private bool CanUseTemplateParts()
    {
        return HasTemplateRoot && _track != null && _indicator != null;
    }

    private void UpdateVisualStates()
    {
        _ = VisualStateManager.GoToState(this, IsIndeterminate ? "Indeterminate" : "Determinate");

        var validationState = Validation.GetHasError(this)
            ? (IsFocused ? "InvalidFocused" : "InvalidUnfocused")
            : "Valid";
        _ = VisualStateManager.GoToState(this, validationState);
    }

    private void SyncTemplateParts()
    {
        if (!CanUseTemplateParts() || _track == null || _indicator == null)
        {
            return;
        }

        var trackWidth = _track.ActualWidth;
        var trackHeight = _track.ActualHeight;
        if (trackWidth <= 0f || trackHeight <= 0f)
        {
            return;
        }

        if (IsIndeterminate)
        {
            SyncIndeterminateTemplateParts(trackWidth, trackHeight);
            return;
        }

        SyncDeterminateTemplateParts(trackWidth, trackHeight);
    }

    private void SyncDeterminateTemplateParts(float trackWidth, float trackHeight)
    {
        var normalized = GetNormalizedValue();
        if (Orientation == Orientation.Horizontal)
        {
            SetElementRect(_track!, _indicator!, 0f, 0f, trackWidth * normalized, trackHeight);
            SetElementRect(_track, _glowRect, 0f, 0f, 0f, trackHeight);
            return;
        }

        var fillHeight = trackHeight * normalized;
        SetElementRect(_track!, _indicator!, 0f, trackHeight - fillHeight, trackWidth, fillHeight);
        SetElementRect(_track, _glowRect, 0f, 0f, trackWidth, 0f);
    }

    private void SyncIndeterminateTemplateParts(float trackWidth, float trackHeight)
    {
        const float chunkRatio = 0.32f;
        const float glowRatio = 0.42f;

        if (Orientation == Orientation.Horizontal)
        {
            var segmentWidth = MathF.Max(6f, trackWidth * chunkRatio);
            var travel = trackWidth + segmentWidth;
            var startX = (_indeterminatePhase * travel) - segmentWidth;
            if (TrySyncIndeterminateTemplatePartsWithTransforms(
                    _indicator,
                    ref _indicatorTranslateTransform,
                    _glowRect,
                    ref _glowTranslateTransform,
                    segmentWidth,
                    trackHeight,
                    startX,
                    0f,
                    MathF.Max(4f, segmentWidth * glowRatio),
                    trackHeight,
                    startX + ((segmentWidth - MathF.Max(4f, segmentWidth * glowRatio)) / 2f),
                    0f))
            {
                return;
            }

            var endX = startX + segmentWidth;
            var visibleStartX = MathF.Max(0f, startX);
            var visibleEndX = MathF.Min(trackWidth, endX);
            SetElementRect(_track!, _indicator!, visibleStartX, 0f, MathF.Max(0f, visibleEndX - visibleStartX), trackHeight);

            var glowWidth = MathF.Max(4f, segmentWidth * glowRatio);
            var glowX = startX + ((segmentWidth - glowWidth) / 2f);
            var glowEndX = glowX + glowWidth;
            var visibleGlowStartX = MathF.Max(0f, glowX);
            var visibleGlowEndX = MathF.Min(trackWidth, glowEndX);
            SetElementRect(_track, _glowRect, visibleGlowStartX, 0f, MathF.Max(0f, visibleGlowEndX - visibleGlowStartX), trackHeight);

            return;
        }

        var segmentHeight = MathF.Max(6f, trackHeight * chunkRatio);
        var travelHeight = trackHeight + segmentHeight;
        var startY = trackHeight - (_indeterminatePhase * travelHeight);
        if (TrySyncIndeterminateTemplatePartsWithTransforms(
                _indicator,
                ref _indicatorTranslateTransform,
                _glowRect,
                ref _glowTranslateTransform,
                trackWidth,
                segmentHeight,
                0f,
                startY,
                trackWidth,
                MathF.Max(4f, segmentHeight * glowRatio),
                0f,
                startY + ((segmentHeight - MathF.Max(4f, segmentHeight * glowRatio)) / 2f)))
        {
            return;
        }

        var endY = startY + segmentHeight;
        var visibleStartY = MathF.Max(0f, startY);
        var visibleEndY = MathF.Min(trackHeight, endY);
        SetElementRect(_track!, _indicator!, 0f, visibleStartY, trackWidth, MathF.Max(0f, visibleEndY - visibleStartY));

        var glowHeight = MathF.Max(4f, segmentHeight * glowRatio);
        var glowY = startY + ((segmentHeight - glowHeight) / 2f);
        var glowEndY = glowY + glowHeight;
        var visibleGlowStartY = MathF.Max(0f, glowY);
        var visibleGlowEndY = MathF.Min(trackHeight, glowEndY);
        SetElementRect(_track, _glowRect, 0f, visibleGlowStartY, trackWidth, MathF.Max(0f, visibleGlowEndY - visibleGlowStartY));
    }

    private static void SetElementRect(FrameworkElement? track, FrameworkElement? element, float x, float y, float width, float height)
    {
        if (track == null || element == null)
        {
            return;
        }

        var coercedWidth = MathF.Max(0f, width);
        var coercedHeight = MathF.Max(0f, height);
        var targetRect = new LayoutRect(
            track.LayoutSlot.X + x,
            track.LayoutSlot.Y + y,
            coercedWidth,
            coercedHeight);

        if (LayoutRectsClose(element.LayoutSlot, targetRect))
        {
            return;
        }

        element.Arrange(targetRect);
        element.InvalidateVisual();
    }

    private static void ArrangeElementRect(FrameworkElement? track, FrameworkElement? element, float x, float y, float width, float height)
    {
        if (track == null || element == null)
        {
            return;
        }

        var coercedWidth = MathF.Max(0f, width);
        var coercedHeight = MathF.Max(0f, height);
        var targetRect = new LayoutRect(
            track.LayoutSlot.X + x,
            track.LayoutSlot.Y + y,
            coercedWidth,
            coercedHeight);

        if (LayoutRectsClose(element.LayoutSlot, targetRect))
        {
            return;
        }

        element.Arrange(targetRect);
    }

    private void UpdateTrackClipState()
    {
        if (_track == null)
        {
            return;
        }

        if (_track.ClipToBounds == IsIndeterminate)
        {
            return;
        }

        _track.ClipToBounds = IsIndeterminate;
    }

    private static bool TrySyncIndeterminateTemplatePartsWithTransforms(
        FrameworkElement? indicator,
        ref TranslateTransform? indicatorTransform,
        FrameworkElement? glow,
        ref TranslateTransform? glowTransform,
        float indicatorWidth,
        float indicatorHeight,
        float indicatorTranslateX,
        float indicatorTranslateY,
        float glowWidth,
        float glowHeight,
        float glowTranslateX,
        float glowTranslateY)
    {
        if (!TryGetOrCreateTranslateTransform(indicator, ref indicatorTransform) ||
            !TryGetOrCreateTranslateTransform(glow, ref glowTransform))
        {
            return false;
        }

        if (indicator?.VisualParent is not FrameworkElement track)
        {
            return false;
        }

        ArrangeElementRect(track, indicator, 0f, 0f, indicatorWidth, indicatorHeight);
        ArrangeElementRect(track, glow, 0f, 0f, glowWidth, glowHeight);

        UIElement.BeginFreezableInvalidationBatch();
        try
        {
            indicatorTransform!.X = indicatorTranslateX;
            indicatorTransform.Y = indicatorTranslateY;
            glowTransform!.X = glowTranslateX;
            glowTransform.Y = glowTranslateY;
        }
        finally
        {
            UIElement.EndFreezableInvalidationBatch();
        }

        return true;
    }

    private static bool TryGetOrCreateTranslateTransform(FrameworkElement? element, ref TranslateTransform? transform)
    {
        if (element == null)
        {
            return false;
        }

        if (transform != null)
        {
            return true;
        }

        if (element.RenderTransform is TranslateTransform existingTranslate)
        {
            transform = existingTranslate;
            return true;
        }

        if (element.RenderTransform != null)
        {
            return false;
        }

        transform = new TranslateTransform();
        element.RenderTransform = transform;
        return true;
    }

    private static void PrepareTemplatePartForDirectLayout(FrameworkElement? element)
    {
        if (element == null)
        {
            return;
        }

        if (element.HorizontalAlignment != HorizontalAlignment.Stretch)
        {
            element.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        if (element.VerticalAlignment != VerticalAlignment.Stretch)
        {
            element.VerticalAlignment = VerticalAlignment.Stretch;
        }

        if (!ThicknessesClose(element.Margin, default))
        {
            element.Margin = default;
        }

        if (!float.IsNaN(element.Width))
        {
            element.Width = float.NaN;
        }

        if (!float.IsNaN(element.Height))
        {
            element.Height = float.NaN;
        }
    }

    private static bool LayoutRectsClose(LayoutRect left, LayoutRect right)
    {
        return AreCloseForLayout(left.X, right.X) &&
               AreCloseForLayout(left.Y, right.Y) &&
               AreCloseForLayout(left.Width, right.Width) &&
               AreCloseForLayout(left.Height, right.Height);
    }

    private static bool ThicknessesClose(Thickness left, Thickness right)
    {
        return AreCloseForLayout(left.Left, right.Left) &&
               AreCloseForLayout(left.Top, right.Top) &&
               AreCloseForLayout(left.Right, right.Right) &&
               AreCloseForLayout(left.Bottom, right.Bottom);
    }

    private static bool AreCloseForLayout(float left, float right)
    {
        if (float.IsNaN(left) && float.IsNaN(right))
        {
            return true;
        }

        if (float.IsNaN(left) || float.IsNaN(right))
        {
            return false;
        }

        return MathF.Abs(left - right) <= LayoutComparisonEpsilon;
    }

}
