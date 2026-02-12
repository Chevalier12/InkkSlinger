using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ItemsControl : Control
{
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private readonly ObservableCollection<object> _items = new();
    private readonly List<GeneratedItemContainer> _generatedChildren = new();
    private ItemsPresenter? _activeItemsPresenter;
    private bool _suspendRegeneration;

    public ItemsControl()
    {
        _items.CollectionChanged += (_, _) => RegenerateChildren();
    }

    public ObservableCollection<object> Items => _items;

    public void AddItems(IEnumerable<object> items)
    {
        _suspendRegeneration = true;
        try
        {
            foreach (var item in items)
            {
                _items.Add(item);
            }
        }
        finally
        {
            _suspendRegeneration = false;
        }

        RegenerateChildren();
    }

    public DataTemplate? ItemTemplate
    {
        get => GetValue<DataTemplate>(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public DataTemplateSelector? ItemTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    protected virtual bool IncludeGeneratedChildrenInVisualTree => _activeItemsPresenter == null;

    protected IReadOnlyList<UIElement> ItemContainers
    {
        get
        {
            var containers = new List<UIElement>(_generatedChildren.Count);
            foreach (var entry in _generatedChildren)
            {
                containers.Add(entry.Container);
            }

            return containers;
        }
    }

    internal IReadOnlyList<UIElement> GetItemContainersForPresenter()
    {
        return ItemContainers;
    }

    internal void AttachItemsPresenter(ItemsPresenter presenter)
    {
        if (ReferenceEquals(_activeItemsPresenter, presenter))
        {
            return;
        }

        _activeItemsPresenter = presenter;
        InvalidateMeasure();
    }

    internal void DetachItemsPresenter(ItemsPresenter presenter)
    {
        if (!ReferenceEquals(_activeItemsPresenter, presenter))
        {
            return;
        }

        _activeItemsPresenter = null;
        InvalidateMeasure();
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            yield break;
        }

        foreach (var generated in _generatedChildren)
        {
            yield return generated.Container;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            yield break;
        }

        foreach (var generated in _generatedChildren)
        {
            yield return generated.Container;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            return desired;
        }

        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(availableSize);
            desired.X = MathF.Max(desired.X, element.DesiredSize.X);
            desired.Y += element.DesiredSize.Y;
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            return finalSize;
        }

        var currentY = LayoutSlot.Y;
        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
            currentY += height;
        }

        return finalSize;
    }

    protected virtual bool IsItemItsOwnContainerOverride(object item)
    {
        return item is UIElement;
    }

    protected virtual UIElement CreateContainerForItemOverride(object item)
    {
        return new Label
        {
            Text = item?.ToString() ?? string.Empty
        };
    }

    protected virtual void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
    }

    protected virtual void ClearContainerForItemOverride(UIElement element, object item)
    {
    }

    protected virtual void OnItemsChanged()
    {
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ItemTemplateProperty || args.Property == ItemTemplateSelectorProperty)
        {
            RegenerateChildren();
        }
    }

    protected int IndexFromContainer(UIElement element)
    {
        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            if (ReferenceEquals(_generatedChildren[i].Container, element))
            {
                return i;
            }
        }

        return -1;
    }

    protected object? ItemFromContainer(UIElement element)
    {
        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            if (ReferenceEquals(_generatedChildren[i].Container, element))
            {
                return _generatedChildren[i].Item;
            }
        }

        return null;
    }

    private void RegenerateChildren()
    {
        if (_suspendRegeneration)
        {
            return;
        }

        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            ClearContainerForItemOverride(child, entry.Item);
            child.SetVisualParent(null);
            child.SetLogicalParent(null);
        }

        _generatedChildren.Clear();

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            UIElement? element;
            var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
                this,
                item,
                ItemTemplate,
                ItemTemplateSelector,
                this);

            if (selectedTemplate != null)
            {
                element = selectedTemplate.Build(item, this);
            }
            else if (IsItemItsOwnContainerOverride(item) && item is UIElement uiElement)
            {
                element = uiElement;
            }
            else
            {
                element = CreateContainerForItemOverride(item);
            }

            if (element == null)
            {
                continue;
            }

            PrepareContainerForItemOverride(element, item, i);
            element.SetVisualParent(this);
            element.SetLogicalParent(this);
            _generatedChildren.Add(new GeneratedItemContainer(item, element));
        }

        OnItemsChanged();
        _activeItemsPresenter?.InvalidateMeasure();
        InvalidateMeasure();
    }

    private readonly struct GeneratedItemContainer
    {
        public GeneratedItemContainer(object item, UIElement container)
        {
            Item = item;
            Container = container;
        }

        public object Item { get; }

        public UIElement Container { get; }
    }
}
