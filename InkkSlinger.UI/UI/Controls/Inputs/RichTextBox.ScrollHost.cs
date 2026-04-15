using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private static readonly Lazy<Style> DefaultRichTextBoxStyle = new(BuildDefaultRichTextBoxStyle);

    private readonly RichTextBoxScrollContentPresenter _scrollContentPresenter;
    private ScrollViewer? _contentHost;
    private bool _hasPendingContentHostScrollOffsets;

    protected override Style? GetFallbackStyle()
    {
        return DefaultRichTextBoxStyle.Value;
    }

    public override void OnApplyTemplate()
    {
        DetachContentHost();
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_ContentHost") is not ScrollViewer contentHost)
        {
            return;
        }

        _contentHost = contentHost;
        _contentHost.ViewportChanged += OnContentHostViewportChanged;
        _contentHost.Content = _scrollContentPresenter;
        SyncContentHostProperties();
        ApplyPendingScrollOffsetsToContentHost();
        EnsureHostedDocumentChildLayout();
        NotifyViewportChangedIfNeeded();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, HorizontalScrollBarVisibilityProperty) ||
            ReferenceEquals(args.Property, VerticalScrollBarVisibilityProperty))
        {
            SyncContentHostProperties();
            _contentHost?.InvalidateScrollInfo();
            NotifyViewportChangedIfNeeded();
        }
    }

    private void DetachContentHost()
    {
        if (_contentHost == null)
        {
            return;
        }

        _contentHost.ViewportChanged -= OnContentHostViewportChanged;
        if (ReferenceEquals(_contentHost.Content, _scrollContentPresenter))
        {
            _contentHost.Content = null;
        }

        _contentHost = null;
    }

    private void OnContentHostViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyPendingScrollOffsetsToContentHost();
        EnsureHostedDocumentChildLayout();
        NotifyViewportChangedIfNeeded();
    }

    private void SyncContentHostProperties()
    {
        if (_contentHost == null)
        {
            return;
        }

        _contentHost.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        _contentHost.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
    }

    private void ApplyPendingScrollOffsetsToContentHost()
    {
        if (_contentHost == null || !HasUsableContentHostMetrics() || !_hasPendingContentHostScrollOffsets)
        {
            return;
        }

        _contentHost.ScrollToHorizontalOffset(_horizontalOffset);
        _contentHost.ScrollToVerticalOffset(_verticalOffset);
        _horizontalOffset = _contentHost.HorizontalOffset;
        _verticalOffset = _contentHost.VerticalOffset;
        _hasPendingContentHostScrollOffsets = false;
    }

    private float GetEffectiveHorizontalOffset()
    {
        return HasUsableContentHostMetrics() ? _contentHost!.HorizontalOffset : _horizontalOffset;
    }

    private float GetEffectiveVerticalOffset()
    {
        return HasUsableContentHostMetrics() ? _contentHost!.VerticalOffset : _verticalOffset;
    }

    private bool HasUsableContentHostMetrics()
    {
         return _contentHost != null &&
             (_contentHost.ViewportWidth > 0f || _contentHost.ViewportHeight > 0f) &&
             (_contentHost.ExtentWidth > 0f || _contentHost.ExtentHeight > 0f);
    }

    private float ResolveHostedContentLayoutWidth(float fallbackWidth)
    {
        if (TextWrapping == TextWrapping.NoWrap)
        {
            return float.PositiveInfinity;
        }

        if (float.IsFinite(fallbackWidth) && fallbackWidth > 0f)
        {
            return fallbackWidth;
        }

        if (_contentHost != null && _contentHost.ViewportWidth > 0f)
        {
            return _contentHost.ViewportWidth;
        }

        return Math.Max(0f, fallbackWidth);
    }

    private Vector2 MeasureHostedScrollContent(Vector2 availableSize)
    {
        var layoutWidth = ResolveHostedContentLayoutWidth(availableSize.X);
        var layout = BuildOrGetLayout(layoutWidth);
        _lastMeasuredLayout = layout;
        return new Vector2(Math.Max(0f, layout.ContentWidth), Math.Max(0f, layout.ContentHeight));
    }

    private bool CanReuseHostedContentMeasure(float previousFallbackWidth, float nextFallbackWidth)
    {
        var previousLayoutWidth = ResolveHostedContentLayoutWidth(previousFallbackWidth);
        var nextLayoutWidth = ResolveHostedContentLayoutWidth(nextFallbackWidth);
        if (AreEquivalentDocumentLayoutWidths(previousLayoutWidth, nextLayoutWidth))
        {
            return true;
        }

        return CanReuseDocumentLayoutForWidthChange(_lastMeasuredLayout ?? _lastRenderedLayout, previousLayoutWidth, nextLayoutWidth);
    }

    private static bool CanReuseDocumentLayoutForWidthChange(DocumentLayoutResult? layout, float previousWidth, float nextWidth)
    {
        if (layout == null)
        {
            return false;
        }

        if (AreEquivalentDocumentLayoutWidths(previousWidth, nextWidth))
        {
            return true;
        }

        if (!float.IsFinite(previousWidth) || !float.IsFinite(nextWidth))
        {
            return false;
        }

        if (nextWidth > previousWidth + 0.01f)
        {
            return false;
        }

        return layout.ContentWidth <= nextWidth + 0.01f;
    }

    private static bool AreEquivalentDocumentLayoutWidths(float left, float right)
    {
        if (float.IsPositiveInfinity(left) || float.IsPositiveInfinity(right))
        {
            return float.IsPositiveInfinity(left) == float.IsPositiveInfinity(right);
        }

        if (float.IsNaN(left) || float.IsNaN(right))
        {
            return false;
        }

        return MathF.Abs(left - right) <= 0.01f;
    }

    private void RenderHostedScrollContent(SpriteBatch spriteBatch, LayoutRect slot)
    {
        var layout = BuildOrGetLayout(ResolveHostedContentLayoutWidth(slot.Width));
        RenderDocumentSurface(spriteBatch, slot, layout, 0f, 0f, includeHostedChildren: false);
    }

    private static Style BuildDefaultRichTextBoxStyle()
    {
        var style = new Style(typeof(RichTextBox));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultRichTextBoxTemplate()));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(202, 202, 202)));

        var focusedTrigger = new Trigger(IsFocusedProperty, true);
        focusedTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(94, 168, 255)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(168, 168, 168)));
        disabledTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(102, 102, 102)));

        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(focusedTrigger);
        style.Triggers.Add(disabledTrigger);
        return style;
    }

    private static ControlTemplate BuildDefaultRichTextBoxTemplate()
    {
        var template = new ControlTemplate(static _ =>
        {
            var border = new Border
            {
                Name = "PART_Border"
            };

            border.Child = new ScrollViewer
            {
                Name = "PART_ContentHost",
                Focusable = false
            };

            return border;
        })
        {
            TargetType = typeof(RichTextBox)
        };

        template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_ContentHost", FrameworkElement.MarginProperty, PaddingProperty);
        template.BindTemplate("PART_ContentHost", ScrollViewer.HorizontalScrollBarVisibilityProperty, HorizontalScrollBarVisibilityProperty);
        template.BindTemplate("PART_ContentHost", ScrollViewer.VerticalScrollBarVisibilityProperty, VerticalScrollBarVisibilityProperty);

        return template;
    }

    private sealed class RichTextBoxScrollContentPresenter : FrameworkElement, IHyperlinkHoverHost
    {
        private readonly RichTextBox _owner;

        public RichTextBoxScrollContentPresenter(RichTextBox owner)
        {
            _owner = owner;
            Focusable = false;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _owner.MeasureHostedScrollContent(availableSize);
        }

        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return _owner.CanReuseHostedContentMeasure(previousAvailableSize.X, nextAvailableSize.X);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }

        protected override void OnRender(SpriteBatch spriteBatch)
        {
            _owner.RenderHostedScrollContent(spriteBatch, LayoutSlot);
        }

        public void UpdateHoveredHyperlinkFromPointer(Vector2 pointerPosition)
        {
            _owner.UpdateHoveredHyperlinkFromPointer(pointerPosition);
        }
    }
}