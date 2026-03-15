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

    protected virtual bool ShouldCreateImplicitContentElement(object? content, DataTemplate? selectedTemplate)
    {
        _ = content;
        _ = selectedTemplate;
        return true;
    }

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

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() + (_contentElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        if (index == baseCount && _contentElement != null)
        {
            return _contentElement;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
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
        if (_activeContentPresenter == null &&
            content is UIElement existingElement &&
            ReferenceEquals(_contentElement, existingElement) &&
            ReferenceEquals(existingElement.VisualParent, this) &&
            ReferenceEquals(existingElement.LogicalParent, this))
        {
            return;
        }

        if (_activeContentPresenter == null && content == null && _contentElement == null)
        {
            return;
        }

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

        if (this is Label)
        {
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

        if (!ShouldCreateImplicitContentElement(content, selectedTemplate))
        {
            return;
        }

        if (content != null)
        {
            _contentElement = new Label
            {
                Content = content.ToString() ?? string.Empty
            };
            _contentElement.SetVisualParent(this);
            _contentElement.SetLogicalParent(this);
        }
    }
}
