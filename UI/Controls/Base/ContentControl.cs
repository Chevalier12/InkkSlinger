using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ContentControl : Control
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(
            nameof(ContentTemplate),
            typeof(DataTemplate),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ContentTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _contentElement;
    private ContentPresenter? _activeContentPresenter;
    protected UIElement? ContentElement => _contentElement;

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate? ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public DataTemplateSelector? ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        if (_contentElement != null)
        {
            yield return _contentElement;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (_contentElement != null)
        {
            yield return _contentElement;
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty)
        {
            UpdateContentElement(args.NewValue);
        }

        if (args.Property == ContentTemplateProperty || args.Property == ContentTemplateSelectorProperty)
        {
            UpdateContentElement(Content);
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        UpdateContentElement(Content);
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        UpdateContentElement(Content);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var templateSize = base.MeasureOverride(availableSize);

        if (_activeContentPresenter == null && _contentElement is FrameworkElement content)
        {
            content.Measure(availableSize);
            templateSize.X = MathF.Max(templateSize.X, content.DesiredSize.X);
            templateSize.Y = MathF.Max(templateSize.Y, content.DesiredSize.Y);
        }

        return templateSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);

        if (_activeContentPresenter == null && _contentElement is FrameworkElement content)
        {
            content.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    internal void AttachContentPresenter(ContentPresenter presenter)
    {
        if (ReferenceEquals(_activeContentPresenter, presenter))
        {
            return;
        }

        _activeContentPresenter = presenter;
        UpdateContentElement(Content);
        InvalidateMeasure();
    }

    internal void DetachContentPresenter(ContentPresenter presenter)
    {
        if (!ReferenceEquals(_activeContentPresenter, presenter))
        {
            return;
        }

        _activeContentPresenter = null;
        UpdateContentElement(Content);
        InvalidateMeasure();
    }

    private void UpdateContentElement(object? content)
    {
        if (_contentElement != null)
        {
            _contentElement.SetVisualParent(null);
            _contentElement.SetLogicalParent(null);
            _contentElement = null;
        }

        if (_activeContentPresenter != null)
        {
            _activeContentPresenter.NotifyOwnerContentChanged();
            return;
        }

        if (content is UIElement element)
        {
            _contentElement = element;
            _contentElement.SetVisualParent(this);
            _contentElement.SetLogicalParent(this);
            return;
        }

        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            content,
            ContentTemplate,
            ContentTemplateSelector,
            this);
        if (selectedTemplate != null)
        {
            _contentElement = selectedTemplate.Build(content, this);
            if (_contentElement != null)
            {
                _contentElement.SetVisualParent(this);
                _contentElement.SetLogicalParent(this);
            }
            return;
        }

        if (content != null)
        {
            _contentElement = new Label
            {
                Text = content.ToString() ?? string.Empty
            };
            _contentElement.SetVisualParent(this);
            _contentElement.SetLogicalParent(this);
        }
    }
}
