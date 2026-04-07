using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment),
            typeof(HorizontalAlignment),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalContentAlignment),
            typeof(VerticalAlignment),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _presentedElement;
    private DependencyObject? _sourceOwner;
    private object? _lastEffectiveContent;
    private DataTemplate? _lastEffectiveTemplate;
    private DataTemplateSelector? _lastEffectiveTemplateSelector;
    private bool _hasEffectivePresentationState;

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

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_presentedElement != null)
        {
            yield return _presentedElement;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _presentedElement != null ? 1 : 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index == 0 && _presentedElement != null)
        {
            return _presentedElement;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
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
        if (RefreshPresentedElement())
        {
            InvalidateMeasure();
        }
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
            if (RefreshPresentedElement())
            {
                InvalidateMeasure();
            }
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        if (RefreshPresentedElement())
        {
            InvalidateMeasure();
        }
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        if (RefreshPresentedElement())
        {
            InvalidateMeasure();
        }
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

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        EnsureSourceBinding();
        if (_presentedElement is not FrameworkElement element)
        {
            return true;
        }

        return element.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        EnsureSourceBinding();
        if (_presentedElement is FrameworkElement element)
        {
            var horizontalAlignment = ResolveEffectiveHorizontalContentAlignment();
            var verticalAlignment = ResolveEffectiveVerticalContentAlignment();
            var childWidth = ResolveAlignedSize(finalSize.X, element.DesiredSize.X, horizontalAlignment);
            var childHeight = ResolveAlignedSize(finalSize.Y, element.DesiredSize.Y, verticalAlignment);
            var childX = ResolveAlignedPosition(LayoutSlot.X, finalSize.X, childWidth, horizontalAlignment);
            var childY = ResolveAlignedPosition(LayoutSlot.Y, finalSize.Y, childHeight, verticalAlignment);
            element.Arrange(new LayoutRect(childX, childY, childWidth, childHeight));
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
        if (!IsSourceOwnerPropertyRelevant(args.Property))
        {
            return;
        }

        var rebuiltPresentedElement = RefreshPresentedElement();
        if (rebuiltPresentedElement)
        {
            InvalidateMeasure();
            return;
        }

        var refreshedFallbackText = TryRefreshFallbackTextStyling(args.Property);
        if (refreshedFallbackText)
        {
            if (!IsForegroundProperty(args.Property))
            {
                InvalidateMeasure();
                InvalidateVisual();
            }

            return;
        }

        if (IsContentAlignmentProperty(args.Property))
        {
            InvalidateArrange();
        }
    }

    private bool RefreshPresentedElement()
    {
        var content = ResolveEffectiveContent();
        var template = ResolveEffectiveTemplate();
        var selector = ResolveEffectiveTemplateSelector();
        if (_hasEffectivePresentationState &&
            Equals(_lastEffectiveContent, content) &&
            ReferenceEquals(_lastEffectiveTemplate, template) &&
            ReferenceEquals(_lastEffectiveTemplateSelector, selector))
        {
            return false;
        }

        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            content,
            template,
            selector,
            this);
        var built = BuildContentElement(content, selectedTemplate);
        if (ReferenceEquals(_presentedElement, built))
        {
            CacheEffectivePresentationState(content, template, selector);
            return false;
        }

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

        CacheEffectivePresentationState(content, template, selector);
        return true;
    }

    private void CacheEffectivePresentationState(object? content, DataTemplate? template, DataTemplateSelector? selector)
    {
        _lastEffectiveContent = content;
        _lastEffectiveTemplate = template;
        _lastEffectiveTemplateSelector = selector;
        _hasEffectivePresentationState = true;
    }

    private bool IsSourceOwnerPropertyRelevant(DependencyProperty property)
    {
        var contentSource = ContentSource;
        if (string.IsNullOrEmpty(contentSource))
        {
            contentSource = "Content";
        }

        if (!HasLocalValue(ContentProperty) &&
            string.Equals(property.Name, contentSource, StringComparison.Ordinal))
        {
            return true;
        }

        if (!HasLocalValue(ContentTemplateProperty) &&
            string.Equals(property.Name, contentSource + "Template", StringComparison.Ordinal))
        {
            return true;
        }

        if (!HasLocalValue(ContentTemplateSelectorProperty) &&
            string.Equals(property.Name, contentSource + "TemplateSelector", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(property.Name, nameof(FrameworkElement.FontFamily), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontSize), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontWeight), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontStyle), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.Foreground), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.HorizontalContentAlignment), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.VerticalContentAlignment), StringComparison.Ordinal))
        {
            return true;
        }

        return false;
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

        var property = FindReadableProperty(_sourceOwner.GetType(), ContentSource);
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
        var property = FindReadableProperty(_sourceOwner.GetType(), templatePropertyName);
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
        var property = FindReadableProperty(_sourceOwner.GetType(), selectorPropertyName);
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
            if (WouldCreatePresentationCycle(uiElement))
            {
                return BuildCycleGuardLabel(content);
            }

            return uiElement;
        }

        if (template != null)
        {
            var built = template.Build(content, this);
            if (built != null && WouldCreatePresentationCycle(built))
            {
                return BuildCycleGuardLabel(content);
            }

            return built;
        }

        if (content != null)
        {
            if (RecognizesAccessKey)
            {
                var accessText = new AccessText
                {
                    Text = content.ToString() ?? string.Empty
                };

                ApplyFallbackTextBlockStyling(accessText, changedProperty: null);
                return accessText;
            }

            var label = new Label
            {
                Content = content.ToString() ?? string.Empty
            };
            ApplyFallbackLabelStyling(label, changedProperty: null);
            return label;
        }

        return null;
    }

    internal UIElement? ResolveAccessKeyTarget()
    {
        if (_sourceOwner is Label label)
        {
            return label.ResolveAccessKeyTarget();
        }

        if (_sourceOwner is FrameworkElement frameworkElement &&
            frameworkElement.RecognizesAccessKey)
        {
            return frameworkElement;
        }

        return null;
    }

    private bool TryRefreshFallbackTextStyling(DependencyProperty? changedProperty = null)
    {
        var content = ResolveEffectiveContent();
        if (content == null || content is UIElement)
        {
            return false;
        }

        if (ResolveEffectiveTemplate() != null)
        {
            return false;
        }

        if (_presentedElement is Label label && !RecognizesAccessKey)
        {
            ApplyFallbackLabelStyling(label, changedProperty);
            return true;
        }

        if (_presentedElement is TextBlock textBlock && RecognizesAccessKey)
        {
            ApplyFallbackTextBlockStyling(textBlock, changedProperty);
            return true;
        }

        return false;
    }

    private void ApplyFallbackLabelStyling(Label label, DependencyProperty? changedProperty)
    {
        if (IsForegroundProperty(changedProperty))
        {
            if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foregroundOnly))
            {
                ApplyFallbackLabelAssignment(
                    label,
                    "Foreground",
                    static currentLabel => currentLabel.Foreground,
                    static (currentLabel, value) => currentLabel.Foreground = value,
                    foregroundOnly);
            }

            return;
        }

        if (_sourceOwner is FrameworkElement frameworkElement)
        {
            ApplyFallbackLabelAssignment(
                label,
                "FontFamily",
                static currentLabel => currentLabel.FontFamily,
                static (currentLabel, value) => currentLabel.FontFamily = value,
                frameworkElement.FontFamily);
            ApplyFallbackLabelAssignment(
                label,
                "FontSize",
                static currentLabel => currentLabel.FontSize,
                static (currentLabel, value) => currentLabel.FontSize = value,
                frameworkElement.FontSize);
            ApplyFallbackLabelAssignment(
                label,
                "FontWeight",
                static currentLabel => currentLabel.FontWeight,
                static (currentLabel, value) => currentLabel.FontWeight = value,
                frameworkElement.FontWeight);
            ApplyFallbackLabelAssignment(
                label,
                "FontStyle",
                static currentLabel => currentLabel.FontStyle,
                static (currentLabel, value) => currentLabel.FontStyle = value,
                frameworkElement.FontStyle);
        }

        if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foreground))
        {
            ApplyFallbackLabelAssignment(
                label,
                "Foreground",
                static currentLabel => currentLabel.Foreground,
                static (currentLabel, value) => currentLabel.Foreground = value,
                foreground);
        }
    }

    private void ApplyFallbackTextBlockStyling(TextBlock textBlock, DependencyProperty? changedProperty)
    {
        if (IsForegroundProperty(changedProperty))
        {
            if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foregroundOnly))
            {
                ApplyFallbackTextBlockAssignment(
                    textBlock,
                    static currentTextBlock => currentTextBlock.Foreground,
                    static (currentTextBlock, value) => currentTextBlock.Foreground = value,
                    foregroundOnly);
            }

            return;
        }

        if (_sourceOwner is FrameworkElement frameworkElement)
        {
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontFamily,
                static (currentTextBlock, value) => currentTextBlock.FontFamily = value,
                frameworkElement.FontFamily);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontSize,
                static (currentTextBlock, value) => currentTextBlock.FontSize = value,
                frameworkElement.FontSize);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontWeight,
                static (currentTextBlock, value) => currentTextBlock.FontWeight = value,
                frameworkElement.FontWeight);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontStyle,
                static (currentTextBlock, value) => currentTextBlock.FontStyle = value,
                frameworkElement.FontStyle);
        }

        if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foreground))
        {
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.Foreground,
                static (currentTextBlock, value) => currentTextBlock.Foreground = value,
                foreground);
        }
    }

    private static bool IsForegroundProperty(DependencyProperty? property)
    {
        return property != null &&
               string.Equals(property.Name, nameof(Control.Foreground), StringComparison.Ordinal);
    }

    private void ApplyFallbackLabelAssignment<TValue>(
        Label label,
        string propertyName,
        Func<Label, TValue> getter,
        Action<Label, TValue> setter,
        TValue value)
    {
        setter(label, value);
    }

    private void ApplyFallbackTextBlockAssignment<TValue>(
        TextBlock textBlock,
        Func<TextBlock, TValue> getter,
        Action<TextBlock, TValue> setter,
        TValue value)
    {
        setter(textBlock, value);
    }

    private static bool TryGetOwnerPropertyValue<TValue>(DependencyObject owner, string propertyName, out TValue value)
    {
        var property = FindReadableProperty(owner.GetType(), propertyName);
        if (property?.PropertyType == typeof(TValue))
        {
            var resolved = property.GetValue(owner);
            if (resolved is TValue typed)
            {
                value = typed;
                return true;
            }
        }

        value = default!;
        return false;
    }

    private bool WouldCreatePresentationCycle(UIElement candidate)
    {
        // Reusing the currently presented element is valid; this happens during
        // refresh passes where content did not materially change.
        if (ReferenceEquals(candidate, _presentedElement))
        {
            return false;
        }

        if (ReferenceEquals(candidate, this))
        {
            return true;
        }

        for (UIElement? current = this; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        for (UIElement? current = candidate; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private static Label BuildCycleGuardLabel(object? content)
    {
        var contentType = content?.GetType().Name ?? "null";
        return new Label { Content = $"ContentPresenter cycle guard ({contentType})" };
    }

    private DependencyObject? FindSourceOwner()
    {
        for (var current = LogicalParent ?? VisualParent; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (current is not DependencyObject dependencyObject)
            {
                continue;
            }

            var property = FindReadableProperty(current.GetType(), ContentSource);
            if (property != null)
            {
                try
                {
                    var value = property.GetValue(current);
                    if (ReferenceEquals(value, this))
                    {
                        // Avoid self-owner cycles (for example host.Content == this presenter).
                        continue;
                    }
                }
                catch
                {
                    // Ignore reflective getter failures and continue owner probing.
                }

                return dependencyObject;
            }
        }

        return null;
    }

    private static PropertyInfo? FindReadableProperty(Type type, string propertyName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var direct = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            for (var i = 0; i < direct.Length; i++)
            {
                var property = direct[i];
                if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (property.GetMethod == null)
                {
                    continue;
                }

                return property;
            }
        }

        return null;
    }

    private HorizontalAlignment ResolveEffectiveHorizontalContentAlignment()
    {
        if (HasLocalValue(HorizontalContentAlignmentProperty))
        {
            return HorizontalContentAlignment;
        }

        if (_sourceOwner is Control control)
        {
            return control.HorizontalContentAlignment;
        }

        return HorizontalContentAlignment;
    }

    private VerticalAlignment ResolveEffectiveVerticalContentAlignment()
    {
        if (HasLocalValue(VerticalContentAlignmentProperty))
        {
            return VerticalContentAlignment;
        }

        if (_sourceOwner is Control control)
        {
            return control.VerticalContentAlignment;
        }

        return VerticalContentAlignment;
    }

    private static float ResolveAlignedSize(float available, float desired, HorizontalAlignment alignment)
    {
        if (alignment == HorizontalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, desired);
    }

    private static float ResolveAlignedSize(float available, float desired, VerticalAlignment alignment)
    {
        if (alignment == VerticalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, desired);
    }

    private static float ResolveAlignedPosition(float start, float available, float size, HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => start + ((available - size) / 2f),
            HorizontalAlignment.Right => start + (available - size),
            _ => start
        };
    }

    private static float ResolveAlignedPosition(float start, float available, float size, VerticalAlignment alignment)
    {
        return alignment switch
        {
            VerticalAlignment.Center => start + ((available - size) / 2f),
            VerticalAlignment.Bottom => start + (available - size),
            _ => start
        };
    }

    private static bool IsContentAlignmentProperty(DependencyProperty property)
    {
        return string.Equals(property.Name, nameof(Control.HorizontalContentAlignment), StringComparison.Ordinal) ||
               string.Equals(property.Name, nameof(Control.VerticalContentAlignment), StringComparison.Ordinal);
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

    internal override int GetVisualChildCountForTraversal()
    {
        EnsureOwner();
        return _itemsOwner?.GetItemContainersForPresenter().Count ?? 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        EnsureOwner();
        var items = _itemsOwner?.GetItemContainersForPresenter();
        if (items != null && (uint)index < (uint)items.Count)
        {
            return items[index];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
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

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() + (_headerElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        if (index == baseCount && _headerElement != null)
        {
            return _headerElement;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
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
                _headerElement = new Label { Content = Header.ToString() ?? string.Empty };
            }
        }

        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
        }
    }
}
