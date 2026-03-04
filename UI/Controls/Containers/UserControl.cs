using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class UserControl : ContentControl
{
    private UIElement? _cachedTemplateRoot;

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // WPF-like mental model: UserControl hosts a single visual root.
    public new UIElement? Content
    {
        get => base.Content as UIElement;
        set => base.Content = value;
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.Property == ContentProperty &&
            args.NewValue != null &&
            args.NewValue is not UIElement)
        {
            throw new InvalidOperationException(
                "UserControl.Content must be a UIElement. Wrap non-visual data in a visual element.");
        }

        if (args.Property == TemplateProperty)
        {
            DetachTemplateContentPresenters();
            // Clear early so any re-entrant layout during template clear/rebuild cannot observe a stale root.
            _cachedTemplateRoot = null;
        }

        base.OnDependencyPropertyChanged(args);

        if (args.Property == TemplateProperty && HasTemplateAssigned())
        {
            RefreshCachedTemplateRoot();
        }
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (!HasTemplateAssigned())
        {
            foreach (var child in base.GetVisualChildren())
            {
                yield return child;
            }

            yield break;
        }

        foreach (var child in base.GetVisualChildren())
        {
            if (ReferenceEquals(child, ContentElement))
            {
                continue;
            }

            yield return child;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (!HasTemplateAssigned())
        {
            foreach (var child in base.GetLogicalChildren())
            {
                yield return child;
            }

            yield break;
        }

        foreach (var child in base.GetLogicalChildren())
        {
            if (ReferenceEquals(child, ContentElement))
            {
                continue;
            }

            yield return child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (HasTemplateAssigned())
        {
            EnsureTemplateAppliedIfNeeded();

            if (_cachedTemplateRoot is FrameworkElement templateRoot)
            {
                templateRoot.Measure(availableSize);
                return templateRoot.DesiredSize;
            }

            return Vector2.Zero;
        }

        var measured = base.MeasureOverride(availableSize);
        var chrome = GetChromeThickness();
        return new Vector2(measured.X + chrome.Horizontal, measured.Y + chrome.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (HasTemplateAssigned())
        {
            EnsureTemplateAppliedIfNeeded();

            if (_cachedTemplateRoot is FrameworkElement templateRoot)
            {
                templateRoot.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
            }

            return finalSize;
        }

        // Intentional compatibility path: base arranges content first; we then re-arrange with UserControl chrome offsets.
        base.ArrangeOverride(finalSize);

        if (ContentElement is FrameworkElement content)
        {
            var chrome = GetChromeThickness();
            content.Arrange(new LayoutRect(
                LayoutSlot.X + chrome.Left,
                LayoutSlot.Y + chrome.Top,
                MathF.Max(0f, finalSize.X - chrome.Horizontal),
                MathF.Max(0f, finalSize.Y - chrome.Vertical)));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (HasTemplateAssigned())
        {
            return;
        }

        var slot = LayoutSlot;
        var border = BorderThickness;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, border.Left, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, slot.Width, border.Top),
                BorderBrush,
                Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom),
                BorderBrush,
                Opacity);
        }
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

    private bool HasTemplateAssigned()
    {
        return Template != null;
    }

    private void EnsureTemplateAppliedIfNeeded()
    {
        if (Template != null && !HasTemplateRoot)
        {
            _cachedTemplateRoot = null;
            ApplyTemplate();
            RefreshCachedTemplateRoot();
            return;
        }

        if (HasTemplateAssigned() && _cachedTemplateRoot == null && HasTemplateRoot)
        {
            RefreshCachedTemplateRoot();
        }
    }

    private void RefreshCachedTemplateRoot()
    {
        _cachedTemplateRoot = null;
        foreach (var child in base.GetVisualChildren())
        {
            if (!ReferenceEquals(child, ContentElement))
            {
                _cachedTemplateRoot = child;
                return;
            }
        }
    }

    private void DetachTemplateContentPresenters()
    {
        var templateRoot = _cachedTemplateRoot;
        if (templateRoot == null)
        {
            foreach (var child in base.GetVisualChildren())
            {
                if (!ReferenceEquals(child, ContentElement))
                {
                    templateRoot = child;
                    break;
                }
            }
        }

        if (templateRoot == null)
        {
            return;
        }

        var pending = new Stack<UIElement>();
        var visited = new HashSet<UIElement>();
        pending.Push(templateRoot);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is ContentPresenter presenter)
            {
                DetachContentPresenter(presenter);
            }

            foreach (var child in current.GetVisualChildren().Concat(current.GetLogicalChildren()))
            {
                pending.Push(child);
            }
        }
    }
}
