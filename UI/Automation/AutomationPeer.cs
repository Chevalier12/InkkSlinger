using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace InkkSlinger;

public abstract class AutomationPeer
{
    private readonly List<AutomationPeer> _children = new();

    protected AutomationPeer(AutomationManager manager, UIElement owner)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    protected AutomationManager Manager { get; }

    public UIElement Owner { get; }

    public int RuntimeId { get; internal set; }

    public AutomationPeer? Parent { get; internal set; }

    public IReadOnlyList<AutomationPeer> Children => _children;

    internal void SetChildren(IReadOnlyList<AutomationPeer> children)
    {
        _children.Clear();
        for (var i = 0; i < children.Count; i++)
        {
            _children.Add(children[i]);
        }
    }

    public virtual string GetName()
    {
        var explicitName = AutomationProperties.GetName(Owner);
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        if (Owner is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name))
        {
            return frameworkElement.Name;
        }

        if (Owner is MenuItem menuItem)
        {
            return MenuAccessText.StripAccessMarkers(menuItem.Header);
        }

        var headerValue = TryReadProperty(Owner, "Header");
        var headerText = ConvertToNameText(headerValue);
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            return headerText;
        }

        var textValue = TryReadProperty(Owner, "Text") ?? TryReadProperty(Owner, "Title");
        var text = ConvertToNameText(textValue);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var contentValue = TryReadProperty(Owner, "Content");
        var contentText = ConvertToNameText(contentValue);
        if (!string.IsNullOrWhiteSpace(contentText))
        {
            return contentText;
        }

        return string.Empty;
    }

    public virtual string GetAutomationId()
    {
        var explicitId = AutomationProperties.GetAutomationId(Owner);
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return explicitId;
        }

        if (Owner is FrameworkElement frameworkElement)
        {
            return frameworkElement.Name ?? string.Empty;
        }

        return string.Empty;
    }

    public virtual string GetHelpText()
    {
        return AutomationProperties.GetHelpText(Owner);
    }

    public virtual string GetItemType()
    {
        return AutomationProperties.GetItemType(Owner);
    }

    public virtual string GetItemStatus()
    {
        return AutomationProperties.GetItemStatus(Owner);
    }

    public virtual bool IsRequiredForForm()
    {
        return AutomationProperties.GetIsRequiredForForm(Owner);
    }

    public virtual AutomationControlType GetControlType()
    {
        return AutomationControlType.Custom;
    }

    public virtual LayoutRect GetBoundingRectRootSpace()
    {
        if (Owner.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            return bounds;
        }

        return default;
    }

    public virtual bool IsEnabled()
    {
        return Owner.IsEnabled;
    }

    public virtual bool IsOffscreen()
    {
        if (!Owner.IsVisible)
        {
            return true;
        }

        var bounds = GetBoundingRectRootSpace();
        return bounds.Width <= 0f || bounds.Height <= 0f;
    }

    public virtual bool IsKeyboardFocusable()
    {
        return Owner is FrameworkElement frameworkElement && frameworkElement.Focusable && IsEnabled() && Owner.IsVisible;
    }

    public virtual bool HasKeyboardFocus()
    {
        return ReferenceEquals(FocusManager.GetFocusedElement(), Owner);
    }

    public virtual bool TryGetPattern(AutomationPatternType patternType, out object? provider)
    {
        provider = null;
        return false;
    }

    protected static object? TryReadProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.CanRead == true ? property.GetValue(target) : null;
    }

    private static string ConvertToNameText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            Label label => label.Text,
            TextBlock textBlock => textBlock.Text,
            _ => value.ToString() ?? string.Empty
        };
    }
}

public class ElementAutomationPeer : AutomationPeer
{
    public ElementAutomationPeer(AutomationManager manager, UIElement owner)
        : base(manager, owner)
    {
    }

    public override AutomationControlType GetControlType()
    {
        return AutomationControlType.Custom;
    }
}

public class FrameworkElementAutomationPeer : ElementAutomationPeer
{
    public FrameworkElementAutomationPeer(AutomationManager manager, FrameworkElement owner)
        : base(manager, owner)
    {
    }

    public override AutomationControlType GetControlType()
    {
        return AutomationControlType.Pane;
    }
}

public class ControlAutomationPeer : FrameworkElementAutomationPeer
{
    public ControlAutomationPeer(AutomationManager manager, Control owner)
        : base(manager, owner)
    {
    }

    public override AutomationControlType GetControlType()
    {
        return AutomationControlType.Custom;
    }
}

internal sealed class GenericAutomationPeer : ControlAutomationPeer,
    IInvokeProvider,
    IValueProvider,
    IRangeValueProvider,
    ISelectionProvider,
    ISelectionItemProvider,
    IExpandCollapseProvider,
    IScrollProvider
{
    private readonly AutomationControlType _controlType;

    public GenericAutomationPeer(AutomationManager manager, Control owner, AutomationControlType controlType)
        : base(manager, owner)
    {
        _controlType = controlType;
    }

    public override AutomationControlType GetControlType()
    {
        return _controlType;
    }

    public override bool TryGetPattern(AutomationPatternType patternType, out object? provider)
    {
        switch (patternType)
        {
            case AutomationPatternType.Invoke when SupportsInvoke():
                provider = this;
                return true;
            case AutomationPatternType.Value when SupportsValue():
                provider = this;
                return true;
            case AutomationPatternType.RangeValue when SupportsRangeValue():
                provider = this;
                return true;
            case AutomationPatternType.Selection when SupportsSelection():
                provider = this;
                return true;
            case AutomationPatternType.SelectionItem when SupportsSelectionItem():
                provider = this;
                return true;
            case AutomationPatternType.ExpandCollapse when SupportsExpandCollapse():
                provider = this;
                return true;
            case AutomationPatternType.Scroll when SupportsScroll():
                provider = this;
                return true;
            default:
                provider = null;
                return false;
        }
    }

    public void Invoke()
    {
        switch (Owner)
        {
            case Button button:
                button.InvokeFromInput();
                break;
            case MenuItem menuItem:
                _ = menuItem.InvokeLeaf();
                break;
            default:
                throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support invoke.");
        }
    }

    public bool IsReadOnly => Owner switch
    {
        TextBox textBox => textBox.IsReadOnly,
        PasswordBox => true,
        ProgressBar => true,
        _ => false
    };

    public string Value
    {
        get
        {
            return Owner switch
            {
                TextBox textBox => textBox.Text,
                PasswordBox => string.Empty,
                _ => Convert.ToString(TryReadProperty(Owner, nameof(Value)), CultureInfo.InvariantCulture) ?? string.Empty
            };
        }
    }

    public void SetValue(string value)
    {
        switch (Owner)
        {
            case TextBox textBox when !textBox.IsReadOnly:
                textBox.Text = value;
                return;
            default:
                throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support setting string value.");
        }
    }

    bool IRangeValueProvider.IsReadOnly => Owner is ProgressBar;

    public float Minimum => Owner switch
    {
        Slider slider => slider.Minimum,
        ScrollBar scrollBar => scrollBar.Minimum,
        ProgressBar progressBar => progressBar.Minimum,
        _ => 0f
    };

    public float Maximum => Owner switch
    {
        Slider slider => slider.Maximum,
        ScrollBar scrollBar => scrollBar.Maximum,
        ProgressBar progressBar => progressBar.Maximum,
        _ => 0f
    };

    float IRangeValueProvider.Value => Owner switch
    {
        Slider slider => slider.Value,
        ScrollBar scrollBar => scrollBar.Value,
        ProgressBar progressBar => progressBar.Value,
        _ => 0f
    };

    public void SetValue(float value)
    {
        switch (Owner)
        {
            case Slider slider:
                slider.Value = value;
                return;
            case ScrollBar scrollBar:
                scrollBar.Value = value;
                return;
            default:
                throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support setting numeric value.");
        }
    }

    public bool CanSelectMultiple => Owner is Selector selector && selector.SelectionMode == SelectionMode.Multiple;

    public bool IsSelectionRequired => false;

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        if (Owner is not Selector selector)
        {
            return Array.Empty<AutomationPeer>();
        }

        var peers = new List<AutomationPeer>();
        for (var i = 0; i < selector.SelectedIndices.Count; i++)
        {
            var index = selector.SelectedIndices[i];
            var container = GetSelectorContainerAt(selector, index);
            if (container == null)
            {
                continue;
            }

            var peer = Manager.GetPeer(container);
            if (peer != null)
            {
                peers.Add(peer);
            }
        }

        return peers;
    }

    public bool IsSelected => Owner switch
    {
        ListBoxItem listBoxItem => listBoxItem.IsSelected,
        TreeViewItem treeViewItem => treeViewItem.IsSelected,
        TabItem tabItem => tabItem.IsSelected,
        _ => false
    };

    public AutomationPeer? SelectionContainer
    {
        get
        {
            var selector = FindAncestor<Selector>(Owner);
            return selector == null ? null : Manager.GetPeer(selector);
        }
    }

    public void Select()
    {
        var selector = FindAncestor<Selector>(Owner);
        if (selector == null)
        {
            throw new InvalidOperationException("Selection item is not attached to a selector.");
        }

        var index = GetContainerIndex(selector, Owner);
        if (index < 0)
        {
            throw new InvalidOperationException("Selection item could not resolve its index.");
        }

        selector.SelectedIndex = index;
    }

    public void AddToSelection()
    {
        var selector = FindAncestor<Selector>(Owner);
        if (selector == null)
        {
            throw new InvalidOperationException("Selection item is not attached to a selector.");
        }

        var index = GetContainerIndex(selector, Owner);
        if (index < 0)
        {
            throw new InvalidOperationException("Selection item could not resolve its index.");
        }

        if (selector.SelectionMode != SelectionMode.Multiple)
        {
            selector.SelectedIndex = index;
            return;
        }

        if (IsSelected)
        {
            return;
        }

        var toggleMethod = typeof(Selector).GetMethod("ToggleSelectedIndexInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        toggleMethod?.Invoke(selector, new object[] { index });
    }

    public void RemoveFromSelection()
    {
        if (!IsSelected)
        {
            return;
        }

        var selector = FindAncestor<Selector>(Owner);
        if (selector == null)
        {
            return;
        }

        if (selector.SelectionMode != SelectionMode.Multiple)
        {
            selector.SelectedIndex = -1;
            return;
        }

        var index = GetContainerIndex(selector, Owner);
        if (index < 0)
        {
            return;
        }

        var toggleMethod = typeof(Selector).GetMethod("ToggleSelectedIndexInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        toggleMethod?.Invoke(selector, new object[] { index });
    }

    public ExpandCollapseState ExpandCollapseState => Owner switch
    {
        Expander expander => expander.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
        TreeViewItem treeViewItem when !treeViewItem.HasChildItems() => ExpandCollapseState.LeafNode,
        TreeViewItem treeViewItem => treeViewItem.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
        MenuItem menuItem when !menuItem.HasChildItems => ExpandCollapseState.LeafNode,
        MenuItem menuItem => menuItem.IsSubmenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
        ComboBox comboBox => comboBox.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
        _ => ExpandCollapseState.LeafNode
    };

    public void Expand()
    {
        switch (Owner)
        {
            case Expander expander:
                expander.IsExpanded = true;
                break;
            case TreeViewItem treeViewItem:
                treeViewItem.IsExpanded = true;
                break;
            case MenuItem menuItem:
                menuItem.IsSubmenuOpen = true;
                break;
            case ComboBox comboBox:
                comboBox.IsDropDownOpen = true;
                break;
            default:
                throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support expand.");
        }
    }

    public void Collapse()
    {
        switch (Owner)
        {
            case Expander expander:
                expander.IsExpanded = false;
                break;
            case TreeViewItem treeViewItem:
                treeViewItem.IsExpanded = false;
                break;
            case MenuItem menuItem:
                menuItem.IsSubmenuOpen = false;
                break;
            case ComboBox comboBox:
                comboBox.IsDropDownOpen = false;
                break;
            default:
                throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support collapse.");
        }
    }

    public bool HorizontallyScrollable => TryGetScrollViewer(out var viewer) && viewer.ExtentWidth > viewer.ViewportWidth + 0.01f;

    public bool VerticallyScrollable => TryGetScrollViewer(out var viewer) && viewer.ExtentHeight > viewer.ViewportHeight + 0.01f;

    public float HorizontalScrollPercent
    {
        get
        {
            if (!TryGetScrollViewer(out var viewer))
            {
                return 0f;
            }

            var range = MathF.Max(0f, viewer.ExtentWidth - viewer.ViewportWidth);
            if (range <= 0.01f)
            {
                return 0f;
            }

            return (viewer.HorizontalOffset / range) * 100f;
        }
    }

    public float VerticalScrollPercent
    {
        get
        {
            if (!TryGetScrollViewer(out var viewer))
            {
                return 0f;
            }

            var range = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
            if (range <= 0.01f)
            {
                return 0f;
            }

            return (viewer.VerticalOffset / range) * 100f;
        }
    }

    public void SetScrollPercent(float horizontalPercent, float verticalPercent)
    {
        if (!TryGetScrollViewer(out var viewer))
        {
            throw new InvalidOperationException($"Element '{Owner.GetType().Name}' does not support scroll pattern.");
        }

        var clampedHorizontal = MathF.Max(0f, MathF.Min(100f, horizontalPercent));
        var clampedVertical = MathF.Max(0f, MathF.Min(100f, verticalPercent));

        var horizontalRange = MathF.Max(0f, viewer.ExtentWidth - viewer.ViewportWidth);
        var verticalRange = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);

        viewer.ScrollToHorizontalOffset((clampedHorizontal / 100f) * horizontalRange);
        viewer.ScrollToVerticalOffset((clampedVertical / 100f) * verticalRange);
    }

    private bool SupportsInvoke()
    {
        return Owner is Button or MenuItem;
    }

    private bool SupportsValue()
    {
        return Owner is TextBox or PasswordBox;
    }

    private bool SupportsRangeValue()
    {
        return Owner is Slider or ScrollBar or ProgressBar;
    }

    private bool SupportsSelection()
    {
        return Owner is Selector;
    }

    private bool SupportsSelectionItem()
    {
        return Owner is ListBoxItem or ListViewItem or TreeViewItem or TabItem or ComboBoxItem;
    }

    private bool SupportsExpandCollapse()
    {
        return Owner is Expander or TreeViewItem or MenuItem or ComboBox;
    }

    private bool SupportsScroll()
    {
        return TryGetScrollViewer(out _);
    }

    private bool TryGetScrollViewer(out ScrollViewer viewer)
    {
        if (Owner is ScrollViewer direct)
        {
            viewer = direct;
            return true;
        }

        var ownerType = Owner.GetType();
        var field = ownerType.GetField("_scrollViewer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(Owner) is ScrollViewer reflected)
        {
            viewer = reflected;
            return true;
        }

        viewer = null!;
        return false;
    }

    private static TElement? FindAncestor<TElement>(UIElement element)
        where TElement : UIElement
    {
        for (var current = element.VisualParent ?? element.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static UIElement? GetSelectorContainerAt(Selector selector, int index)
    {
        var property = typeof(ItemsControl).GetProperty("ItemContainers", BindingFlags.Instance | BindingFlags.NonPublic);
        if (property?.GetValue(selector) is not IReadOnlyList<UIElement> containers)
        {
            return null;
        }

        return index >= 0 && index < containers.Count ? containers[index] : null;
    }

    private static int GetContainerIndex(Selector selector, UIElement container)
    {
        var method = typeof(ItemsControl).GetMethod("IndexFromContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method?.Invoke(selector, new object[] { container }) is int index)
        {
            return index;
        }

        return -1;
    }
}
