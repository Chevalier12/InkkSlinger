using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class TreeViewItem : ItemsControl
{
    internal event EventHandler? ExpandedStateChanged;
    private bool _isApplyingPropagatedForeground;
    private bool _hasVirtualizedDisplaySnapshot;
    private string _virtualizedDisplayHeader = string.Empty;
    private bool _virtualizedDisplayHasChildren;
    private bool _virtualizedDisplayIsExpanded;
    private bool _virtualizedDisplayIsSelected;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(
                Color.White,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is TreeViewItem treeViewItem &&
                        !treeViewItem._isApplyingPropagatedForeground &&
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        treeViewItem.PropagateTypographyToChildren(
                            oldColor,
                            newColor);
                    }
                }));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedBackground),
            typeof(Color),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(new Color(60, 98, 141), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndentProperty =
        DependencyProperty.Register(
            nameof(Indent),
            typeof(float),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(16f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    protected override bool IncludeGeneratedChildrenInVisualTree => IsExpanded && !UseVirtualizedTreeLayout;

    internal bool UseVirtualizedTreeLayout { get; set; }

    internal bool HasVirtualizedChildItems { get; set; }

    internal float RowHitHeightForInput => GetRowHeight();

    internal int VirtualizedTreeDepth { get; set; }

    internal int VirtualizedTreeRowIndex { get; set; } = -1;

    public string Header
    {
        get => GetValue<string>(HeaderProperty) ?? string.Empty;
        set => SetValue(HeaderProperty, value);
    }

    internal string DisplayHeaderForDiagnostics => GetEffectiveHeader();

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_hasVirtualizedDisplaySnapshot)
        {
            yield break;
        }

        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _hasVirtualizedDisplaySnapshot ? 0 : base.GetVisualChildCountForTraversal();
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        return _hasVirtualizedDisplaySnapshot
            ? throw new ArgumentOutOfRangeException(nameof(index))
            : base.GetVisualChildAtForTraversal(index);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color SelectedBackground
    {
        get => GetValue<Color>(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
    }

    public float Indent
    {
        get => GetValue<float>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property != IsExpandedProperty)
        {
            return;
        }

        ExpandedStateChanged?.Invoke(this, EventArgs.Empty);
        UiRoot.Current?.NotifyVisualStructureChanged(this, VisualParent, VisualParent);
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is TreeViewItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        return new TreeViewItem
        {
            Header = item?.ToString() ?? string.Empty
        };
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is not TreeViewItem treeViewItem)
        {
            return;
        }

        ApplyTypographyToItem(treeViewItem, null, Foreground);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var rowHeight = GetRowHeight();
        var rowWidth = HasTemplateRoot && !_hasVirtualizedDisplaySnapshot
            ? MeasureTemplatedHeaderWidth(availableSize, rowHeight)
            : MeasureHeaderWidth();

        if (!IsExpanded || UseVirtualizedTreeLayout)
        {
            return new Vector2(rowWidth, rowHeight);
        }

        var maxWidth = rowWidth;
        var totalHeight = rowHeight;

        foreach (var child in ItemContainers)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(new Vector2(MathF.Max(0f, availableSize.X - Indent), availableSize.Y));
            maxWidth = MathF.Max(maxWidth, Indent + element.DesiredSize.X);
            totalHeight += element.DesiredSize.Y;
        }

        return new Vector2(maxWidth, totalHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var currentY = LayoutSlot.Y + GetRowHeight();

        if (!_hasVirtualizedDisplaySnapshot && HasTemplateRoot && TryGetTemplateRoot(out var templateRoot))
        {
            var padding = Padding;
            var textX = LayoutSlot.X + GetHeaderTextOffset();
            templateRoot.Arrange(new LayoutRect(
                textX,
                LayoutSlot.Y,
                MathF.Max(0f, finalSize.X - (textX - LayoutSlot.X) - padding.Right),
                GetRowHeight()));
        }

        if (IsExpanded && !UseVirtualizedTreeLayout)
        {
            foreach (var child in ItemContainers)
            {
                if (child is not FrameworkElement element)
                {
                    continue;
                }

                var height = element.DesiredSize.Y;
                element.Arrange(new LayoutRect(
                    LayoutSlot.X + Indent,
                    currentY,
                    MathF.Max(0f, finalSize.X - Indent),
                    height));
                currentY += height;
            }
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var rowHeight = GetRowHeight();
        var padding = Padding;
        var rowRect = new LayoutRect(LayoutSlot.X, LayoutSlot.Y, LayoutSlot.Width, rowHeight);
        if (GetEffectiveIsSelected())
        {
            UiDrawing.DrawFilledRect(spriteBatch, rowRect, SelectedBackground, Opacity);
        }

        if (HasChildItems())
        {
            // Modern chevron glyph: solid filled triangle
            //   ▶  right-pointing  →  branch is collapsed
            //   ▼  down-pointing   →  branch is expanded
            // The glyph is rendered at 65 % of the row's Foreground so it reads
            // as a subtle affordance rather than competing with the label text.
            var depthOffset = GetVirtualizedDepthOffset();
            var glyphCx = LayoutSlot.X + depthOffset + padding.Left + 7f;   // horizontal centre of the glyph zone
            var glyphCy = LayoutSlot.Y + (rowHeight / 2f);    // vertical centre of the row
            var glyphColor = Foreground * 0.65f;

            if (GetEffectiveIsExpanded())
            {
                // ▼  down-pointing triangle  (8 wide × 5.5 tall)
                ReadOnlySpan<Vector2> tri =
                [
                    new Vector2(glyphCx - 4f,  glyphCy - 2.75f),
                    new Vector2(glyphCx + 4f,  glyphCy - 2.75f),
                    new Vector2(glyphCx,        glyphCy + 2.75f),
                ];
                UiDrawing.DrawFilledPolygon(spriteBatch, tri, glyphColor, Opacity);
            }
            else
            {
                // ▶  right-pointing triangle  (5.5 wide × 8 tall)
                ReadOnlySpan<Vector2> tri =
                [
                    new Vector2(glyphCx - 2.75f, glyphCy - 4f),
                    new Vector2(glyphCx - 2.75f, glyphCy + 4f),
                    new Vector2(glyphCx + 2.75f, glyphCy),
                ];
                UiDrawing.DrawFilledPolygon(spriteBatch, tri, glyphColor, Opacity);
            }
        }

        var header = GetEffectiveHeader();
        if (!string.IsNullOrEmpty(header))
        {
            if (HasTemplateRoot && !_hasVirtualizedDisplaySnapshot)
            {
                return;
            }

            var textX = LayoutSlot.X + GetHeaderTextOffset();
            var renderSource = ResolveVirtualizedHeaderRenderSource();
            var renderFontSize = renderSource.FontSize;
            var renderText = ResolveVirtualizedHeaderRenderText(header, textX, renderSource);
            if (string.IsNullOrEmpty(renderText))
            {
                return;
            }

            var textY = LayoutSlot.Y + ((rowHeight - UiTextRenderer.GetLineHeight(renderSource.Element, renderFontSize)) / 2f);
            UiTextRenderer.DrawString(
                spriteBatch,
                renderSource.Element,
                renderText,
                new Vector2(textX, textY),
                renderSource.Foreground * Opacity,
                renderFontSize,
                opaqueBackground: true);
        }
    }

    internal bool HasVirtualizedDisplaySnapshotForDiagnostics => _hasVirtualizedDisplaySnapshot;

    internal bool IsSelectedForRenderDiagnostics => GetEffectiveIsSelected();

    internal string RenderedHeaderForDiagnostics
    {
        get
        {
            var header = GetEffectiveHeader();
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            return ResolveVirtualizedHeaderRenderText(
                header,
                LayoutSlot.X + GetHeaderTextOffset(),
                ResolveVirtualizedHeaderRenderSource());
        }
    }

    public bool HitExpander(Vector2 point)
    {
        if (!HasChildItems())
        {
            return false;
        }

        var rowHeight = GetRowHeight();
        // Hit zone is centred on the glyph centre (glyphCx = X + padding.Left + 7, glyphCy = Y + rowHeight/2)
        // Use a 14×14 box so the small triangle remains easy to click.
        var padding = Padding;
        var glyphCx = LayoutSlot.X + GetVirtualizedDepthOffset() + padding.Left + 7f;
        var glyphCy = LayoutSlot.Y + (rowHeight / 2f);
        var rect = new LayoutRect(glyphCx - 7f, glyphCy - 7f, 14f, 14f);
        return point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }

    public bool HasChildItems()
    {
        return _hasVirtualizedDisplaySnapshot
            ? _virtualizedDisplayHasChildren
            : HasVirtualizedChildItems || ItemContainers.Count > 0;
    }

    internal void ApplyVirtualizedDisplaySnapshot(
        string header,
        bool hasChildren,
        bool isExpanded,
        bool isSelected,
        int depth,
        int rowIndex)
    {
        _hasVirtualizedDisplaySnapshot = true;
        _virtualizedDisplayHeader = header;
        _virtualizedDisplayHasChildren = hasChildren;
        _virtualizedDisplayIsExpanded = isExpanded;
        _virtualizedDisplayIsSelected = isSelected;
        UseVirtualizedTreeLayout = true;
        VirtualizedTreeDepth = depth;
        VirtualizedTreeRowIndex = rowIndex;
        InvalidateVisual();
    }

    internal void ClearVirtualizedDisplaySnapshot()
    {
        if (!_hasVirtualizedDisplaySnapshot)
        {
            return;
        }

        _hasVirtualizedDisplaySnapshot = false;
        _virtualizedDisplayHeader = string.Empty;
        _virtualizedDisplayHasChildren = false;
        _virtualizedDisplayIsExpanded = false;
        _virtualizedDisplayIsSelected = false;
        InvalidateVisual();
    }

    public IReadOnlyList<TreeViewItem> GetChildTreeItems()
    {
        var result = new List<TreeViewItem>();
        foreach (var child in ItemContainers)
        {
            if (child is TreeViewItem treeViewItem)
            {
                result.Add(treeViewItem);
            }
        }

        return result;
    }

    private float GetRowHeight()
    {
        var padding = Padding;
        return MathF.Max(18f, UiTextRenderer.GetLineHeight(this, FontSize) + 4f + padding.Vertical);
    }

    private float MeasureHeaderWidth()
    {
        var padding = Padding;
        var header = GetEffectiveHeader();
        var textWidth = !string.IsNullOrEmpty(header)
            ? UiTextRenderer.MeasureWidth(this, header, FontSize)
            : 0f;
        return padding.Horizontal + GetVirtualizedDepthOffset() + (HasChildItems() ? 20f : 10f) + textWidth;
    }

    private string GetEffectiveHeader()
    {
        return _hasVirtualizedDisplaySnapshot ? _virtualizedDisplayHeader : Header;
    }

    private bool GetEffectiveIsSelected()
    {
        return _hasVirtualizedDisplaySnapshot ? _virtualizedDisplayIsSelected : IsSelected;
    }

    private (FrameworkElement Element, float FontSize, Color Foreground, TextTrimming TextTrimming, TextWrapping TextWrapping) ResolveVirtualizedHeaderRenderSource()
    {
        if (TemplateRoot is TextBlock textBlock)
        {
            return (textBlock, textBlock.FontSize, textBlock.Foreground, textBlock.TextTrimming, textBlock.TextWrapping);
        }

        return (this, FontSize, Foreground, TextTrimming.None, TextWrapping.NoWrap);
    }

    private string ResolveVirtualizedHeaderRenderText(
        string header,
        float textX,
        (FrameworkElement Element, float FontSize, Color Foreground, TextTrimming TextTrimming, TextWrapping TextWrapping) renderSource)
    {
        if (renderSource.TextTrimming == TextTrimming.None ||
            renderSource.TextWrapping != TextWrapping.NoWrap ||
            !float.IsFinite(LayoutSlot.Width))
        {
            return header;
        }

        var availableWidth = MathF.Max(0f, LayoutSlot.Width - (textX - LayoutSlot.X) - Padding.Right);
        if (availableWidth <= 0f)
        {
            return string.Empty;
        }

        if (UiTextRenderer.MeasureWidth(renderSource.Element, header, renderSource.FontSize) <= availableWidth)
        {
            return header;
        }

        const string ellipsis = "...";
        if (UiTextRenderer.MeasureWidth(renderSource.Element, ellipsis, renderSource.FontSize) > availableWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = header.Length;
        while (low < high)
        {
            var mid = low + ((high - low + 1) / 2);
            if (UiTextRenderer.MeasureWidth(renderSource.Element, header[..mid] + ellipsis, renderSource.FontSize) <= availableWidth)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return header[..low] + ellipsis;
    }

    private bool GetEffectiveIsExpanded()
    {
        return _hasVirtualizedDisplaySnapshot ? _virtualizedDisplayIsExpanded : IsExpanded;
    }

    private float MeasureTemplatedHeaderWidth(Vector2 availableSize, float rowHeight)
    {
        var offset = GetHeaderTextOffset();
        if (!TryGetTemplateRoot(out var templateRoot))
        {
            return offset;
        }

        var templateAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - offset - Padding.Right),
            rowHeight);
        templateRoot.Measure(templateAvailable);
        return offset + templateRoot.DesiredSize.X + Padding.Right;
    }

    private bool TryGetTemplateRoot(out FrameworkElement templateRoot)
    {
        if (!HasTemplateRoot && Template != null)
        {
            ApplyTemplate();
        }

        if (TemplateRoot is FrameworkElement element)
        {
            templateRoot = element;
            return true;
        }

        templateRoot = null!;
        return false;
    }

    private float GetHeaderTextOffset()
    {
        var padding = Padding;
        return GetVirtualizedDepthOffset() + padding.Left + (HasChildItems() ? 16f : 6f);
    }

    private float GetVirtualizedDepthOffset()
    {
        return UseVirtualizedTreeLayout ? MathF.Max(0f, VirtualizedTreeDepth) * Indent : 0f;
    }

    private void PropagateTypographyToChildren(
        Color? oldForeground,
        Color? newForeground)
    {
        foreach (var child in GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyRecursive(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        ApplyTypographyToItem(item, oldForeground, newForeground);
        foreach (var child in item.GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyToItem(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        item.ApplyPropagatedForeground(oldForeground, newForeground);
    }

    internal void ApplyPropagatedForeground(
        Color? oldForeground,
        Color? newForeground)
    {
        if (newForeground.HasValue && oldForeground.HasValue)
        {
            if (!HasLocalValue(ForegroundProperty) || Foreground == oldForeground.Value)
            {
                _isApplyingPropagatedForeground = true;
                try
                {
                    Foreground = newForeground.Value;
                }
                finally
                {
                    _isApplyingPropagatedForeground = false;
                }
            }
        }
    }
}


