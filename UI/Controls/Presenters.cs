using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ContentPresenter : FrameworkElement
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(
            nameof(ContentTemplate),
            typeof(DataTemplate),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ContentTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentSourceProperty =
        DependencyProperty.Register(
            nameof(ContentSource),
            typeof(string),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(
                "Content",
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _presentedElement;
    private DependencyObject? _sourceOwner;

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

    public string ContentSource
    {
        get => GetValue<string>(ContentSourceProperty) ?? "Content";
        set => SetValue(ContentSourceProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_presentedElement != null)
        {
            yield return _presentedElement;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_presentedElement != null)
        {
            yield return _presentedElement;
        }
    }

    internal void NotifyOwnerContentChanged()
    {
        RefreshPresentedElement();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ContentProperty ||
            args.Property == ContentTemplateProperty ||
            args.Property == ContentTemplateSelectorProperty ||
            args.Property == ContentSourceProperty)
        {
            RefreshSourceBinding();
            RefreshPresentedElement();
            InvalidateMeasure();
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        RefreshPresentedElement();
        InvalidateMeasure();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        RefreshPresentedElement();
        InvalidateMeasure();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureSourceBinding();
        if (_presentedElement is FrameworkElement element)
        {
            element.Measure(availableSize);
            return element.DesiredSize;
        }

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        EnsureSourceBinding();
        if (_presentedElement is FrameworkElement element)
        {
            element.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    private void RefreshSourceBinding()
    {
        EnsureSourceBinding();
    }

    private void EnsureSourceBinding()
    {
        var foundOwner = FindSourceOwner();
        if (ReferenceEquals(foundOwner, _sourceOwner))
        {
            return;
        }

        if (_sourceOwner != null)
        {
            _sourceOwner.DependencyPropertyChanged -= OnSourceOwnerPropertyChanged;
            if (_sourceOwner is ContentControl oldContentControl)
            {
                oldContentControl.DetachContentPresenter(this);
            }
        }

        _sourceOwner = foundOwner;
        if (_sourceOwner != null)
        {
            _sourceOwner.DependencyPropertyChanged += OnSourceOwnerPropertyChanged;
            if (_sourceOwner is ContentControl contentControl && string.Equals(ContentSource, "Content", StringComparison.Ordinal))
            {
                contentControl.AttachContentPresenter(this);
            }
        }

        RefreshPresentedElement();
    }

    private void OnSourceOwnerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        RefreshPresentedElement();
        InvalidateMeasure();
    }

    private void RefreshPresentedElement()
    {
        var content = ResolveEffectiveContent();
        var template = ResolveEffectiveTemplate();
        var selector = ResolveEffectiveTemplateSelector();
        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            content,
            template,
            selector,
            this);
        var built = BuildContentElement(content, selectedTemplate);

        if (_presentedElement != null)
        {
            _presentedElement.SetVisualParent(null);
            _presentedElement.SetLogicalParent(null);
        }

        _presentedElement = built;
        if (_presentedElement != null)
        {
            _presentedElement.SetVisualParent(this);
            _presentedElement.SetLogicalParent(this);
        }
    }

    private object? ResolveEffectiveContent()
    {
        if (HasLocalValue(ContentProperty))
        {
            return Content;
        }

        if (_sourceOwner == null)
        {
            return Content;
        }

        var property = _sourceOwner.GetType().GetProperty(ContentSource, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(_sourceOwner);
    }

    private DataTemplate? ResolveEffectiveTemplate()
    {
        if (HasLocalValue(ContentTemplateProperty))
        {
            return ContentTemplate;
        }

        if (_sourceOwner == null)
        {
            return ContentTemplate;
        }

        var templatePropertyName = ContentSource + "Template";
        var property = _sourceOwner.GetType().GetProperty(templatePropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType == typeof(DataTemplate))
        {
            return property.GetValue(_sourceOwner) as DataTemplate;
        }

        return ContentTemplate;
    }

    private DataTemplateSelector? ResolveEffectiveTemplateSelector()
    {
        if (HasLocalValue(ContentTemplateSelectorProperty))
        {
            return ContentTemplateSelector;
        }

        if (_sourceOwner == null)
        {
            return ContentTemplateSelector;
        }

        var selectorPropertyName = ContentSource + "TemplateSelector";
        var property = _sourceOwner.GetType().GetProperty(selectorPropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType == typeof(DataTemplateSelector))
        {
            return property.GetValue(_sourceOwner) as DataTemplateSelector;
        }

        return ContentTemplateSelector;
    }

    private UIElement? BuildContentElement(object? content, DataTemplate? template)
    {
        if (content is UIElement uiElement)
        {
            return uiElement;
        }

        if (template != null)
        {
            return template.Build(content, this);
        }

        if (content != null)
        {
            return new Label { Text = content.ToString() ?? string.Empty };
        }

        return null;
    }

    private DependencyObject? FindSourceOwner()
    {
        for (var current = LogicalParent ?? VisualParent; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (current is not DependencyObject dependencyObject)
            {
                continue;
            }

            var property = current.GetType().GetProperty(ContentSource, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return dependencyObject;
            }
        }

        return null;
    }
}

public class ItemsPresenter : FrameworkElement
{
    private static readonly bool EnableItemsPresenterTrace = false;
    private ItemsControl? _itemsOwner;
    private ItemsControl? _explicitItemsOwner;

    internal void SetExplicitItemsOwner(ItemsControl? owner)
    {
        if (ReferenceEquals(_explicitItemsOwner, owner))
        {
            return;
        }

        _explicitItemsOwner = owner;
        RefreshOwner();
    }

    internal bool TryGetItemContainersForHitTest(out IReadOnlyList<UIElement> containers)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            containers = Array.Empty<UIElement>();
            return false;
        }

        containers = _itemsOwner.GetItemContainersForPresenter();
        return true;
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_itemsOwner == null)
        {
            yield break;
        }

        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            yield return child;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_itemsOwner == null)
        {
            yield break;
        }

        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            yield return child;
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshOwner();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshOwner();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            return Vector2.Zero;
        }

        Trace($"Measure start available={availableSize} owner={_itemsOwner.GetType().Name} items={_itemsOwner.GetItemContainersForPresenter().Count}");

        var desired = Vector2.Zero;
        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(availableSize);
            desired.X = MathF.Max(desired.X, element.DesiredSize.X);
            desired.Y += element.DesiredSize.Y;
        }

        Trace($"Measure end desired={desired}");
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            return finalSize;
        }

        Trace($"Arrange start final={finalSize} owner={_itemsOwner.GetType().Name} items={_itemsOwner.GetItemContainersForPresenter().Count}");

        var y = LayoutSlot.Y;
        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(LayoutSlot.X, y, finalSize.X, height));
            y += height;
        }

        Trace($"Arrange end final={finalSize}");
        return finalSize;
    }

    private void RefreshOwner()
    {
        EnsureOwner(force: true);
    }

    private void EnsureOwner(bool force = false)
    {
        var foundOwner = _explicitItemsOwner ?? FindItemsOwner();
        if (!force && ReferenceEquals(foundOwner, _itemsOwner))
        {
            return;
        }

        if (_itemsOwner != null)
        {
            _itemsOwner.DetachItemsHost(this);
            _itemsOwner = null;
        }

        _itemsOwner = foundOwner;
        _itemsOwner?.AttachItemsHost(this);
        InvalidateMeasure();
    }

    private ItemsControl? FindItemsOwner()
    {
        for (var current = LogicalParent ?? VisualParent; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (current is ItemsControl itemsControl)
            {
                return itemsControl;
            }
        }

        return null;
    }

    private void Trace(string message)
    {
        if (!EnableItemsPresenterTrace)
        {
            return;
        }

        Console.WriteLine($"[ItemsPresenter#{GetHashCode():X8}] t={Environment.TickCount64} {message}");
    }
}

public class HeaderedContentControl : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
    }
}

public class HeaderedItemsControl : ItemsControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _headerElement;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_headerElement != null)
        {
            yield return _headerElement;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_headerElement != null)
        {
            yield return _headerElement;
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == HeaderProperty ||
            args.Property == HeaderTemplateProperty ||
            args.Property == HeaderTemplateSelectorProperty)
        {
            UpdateHeaderElement();
            InvalidateMeasure();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (_headerElement is FrameworkElement header)
        {
            header.Measure(availableSize);
            desired.X = MathF.Max(desired.X, header.DesiredSize.X);
            desired.Y += header.DesiredSize.Y;
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (_headerElement is FrameworkElement header)
        {
            var headerHeight = header.DesiredSize.Y;
            header.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, headerHeight));

            var offset = headerHeight;
            foreach (var child in GetItemContainersForPresenter())
            {
                if (child is FrameworkElement element)
                {
                    var rect = element.LayoutSlot;
                    element.Arrange(new LayoutRect(rect.X, rect.Y + offset, rect.Width, rect.Height));
                }
            }
        }

        return arranged;
    }

    private void UpdateHeaderElement()
    {
        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(null);
            _headerElement.SetLogicalParent(null);
            _headerElement = null;
        }

        if (Header is UIElement headerElement)
        {
            _headerElement = headerElement;
        }
        else
        {
            var template = DataTemplateResolver.ResolveTemplateForContent(
                this,
                Header,
                HeaderTemplate,
                HeaderTemplateSelector,
                this);
            if (template != null)
            {
                _headerElement = template.Build(Header, this);
            }
            else if (Header != null)
            {
                _headerElement = new Label { Text = Header.ToString() ?? string.Empty };
            }
        }

        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
        }
    }
}
