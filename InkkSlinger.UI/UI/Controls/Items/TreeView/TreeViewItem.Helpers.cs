using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
    private const float TemplateExpanderSnapshotFallbackSlotWidth = 14f;

    internal bool RendersTemplateExpanderSnapshotForDiagnostics => ShouldRenderTemplateExpanderSnapshot();

    internal float HeaderTextOffsetForDiagnostics => GetHeaderTextOffset();

    internal float RenderRowHeightForDiagnostics => GetRenderRowHeight();

    internal float VirtualizedHeaderRenderYForDiagnostics => GetVirtualizedHeaderRenderY(GetRenderRowHeight(), ResolveVirtualizedHeaderRenderSource());

    internal float SnapshotHeaderTextRelativeYForDiagnostics => _snapshotHeaderTextRelativeY;

    internal float SnapshotExpanderTextRelativeXForDiagnostics => _snapshotExpanderTextRelativeX;

    internal float VirtualizedHeaderRenderFontSizeForDiagnostics => ResolveVirtualizedHeaderRenderSource().FontSize;

    internal float VirtualizedExpanderRenderFontSizeForDiagnostics => ResolveVirtualizedExpanderRenderSource().FontSize;

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

    private float GetRenderRowHeight()
    {
        return _virtualizedDisplaySnapshot.HasValue && UseVirtualizedTreeLayout && LayoutSlot.Height > 0f && !ShouldReserveTemplateExpanderSnapshotSlot()
            ? LayoutSlot.Height
            : GetRowHeight();
    }

    private float GetVirtualizedHeaderRenderY(
        float rowHeight,
        (FrameworkElement Element, float FontSize, Color Foreground, TextTrimming TextTrimming, TextWrapping TextWrapping) renderSource)
    {
        if (_virtualizedDisplaySnapshot.HasValue && float.IsFinite(_snapshotHeaderTextRelativeY))
        {
            return LayoutSlot.Y + _snapshotHeaderTextRelativeY;
        }

        return LayoutSlot.Y + MathF.Max(0f, (rowHeight - UiTextRenderer.GetLineHeight(renderSource.Element, renderSource.FontSize)) / 2f);
    }

    private float GetVirtualizedExpanderRenderY(float rowHeight, float glyphHeight)
    {
        return _virtualizedDisplaySnapshot.HasValue && float.IsFinite(_snapshotExpanderRelativeY)
            ? LayoutSlot.Y + _snapshotExpanderRelativeY
            : LayoutSlot.Y + MathF.Max(0f, (rowHeight - glyphHeight) / 2f);
    }

    private float GetVirtualizedExpanderRenderX(float slotWidth, float glyphWidth)
    {
        if (_virtualizedDisplaySnapshot.HasValue && float.IsFinite(_snapshotExpanderTextRelativeX))
        {
            return LayoutSlot.X + GetTemplateRootOffset() + _snapshotExpanderTextRelativeX;
        }

        return LayoutSlot.X + GetVirtualizedDepthOffset() + Padding.Left + MathF.Max(0f, (slotWidth - glyphWidth) / 2f);
    }

    private float MeasureHeaderWidth()
    {
        var padding = Padding;
        var header = GetEffectiveHeader();
        var textWidth = !string.IsNullOrEmpty(header)
            ? UiTextRenderer.MeasureWidth(this, header, FontSize)
            : 0f;
        return padding.Horizontal + GetVirtualizedDepthOffset() + GetBuiltInHeaderSpacingWidth() + textWidth;
    }

    private string GetEffectiveHeader()
    {
        return _virtualizedDisplaySnapshot?.Header ?? Header;
    }

    private bool GetEffectiveIsSelected()
    {
        return _virtualizedDisplaySnapshot?.IsSelected ?? IsSelected;
    }

    private (FrameworkElement Element, float FontSize, Color Foreground, TextTrimming TextTrimming, TextWrapping TextWrapping) ResolveVirtualizedHeaderRenderSource()
    {
        if (GetTemplateChild("HeaderText") is TextBlock headerTextBlock)
        {
            return (headerTextBlock, headerTextBlock.FontSize, headerTextBlock.Foreground, headerTextBlock.TextTrimming, headerTextBlock.TextWrapping);
        }

        if (_virtualizedDisplaySnapshot.HasValue)
        {
            return (this, FontSize, Foreground, TextTrimming.None, TextWrapping.NoWrap);
        }

        if (TemplateRoot is TextBlock textBlock)
        {
            return (textBlock, textBlock.FontSize, textBlock.Foreground, textBlock.TextTrimming, textBlock.TextWrapping);
        }

        return (this, FontSize, Foreground, TextTrimming.None, TextWrapping.NoWrap);
    }

    private (FrameworkElement Element, float FontSize, Color Foreground) ResolveVirtualizedExpanderRenderSource()
    {
        if (GetTemplateChild("PART_Expander") is TextBlock expanderTextBlock)
        {
            return (expanderTextBlock, expanderTextBlock.FontSize, expanderTextBlock.Foreground);
        }

        if (GetTemplateChild("PART_Expander") is FrameworkElement expanderElement)
        {
            return (expanderElement, FontSize, Foreground);
        }

        return (this, FontSize, Foreground);
    }

    private string ResolveVirtualizedHeaderRenderText(
        string header,
        float textX,
        (FrameworkElement Element, float FontSize, Color Foreground, TextTrimming TextTrimming, TextWrapping TextWrapping) renderSource)
    {
        if (renderSource.TextWrapping != TextWrapping.NoWrap ||
            !float.IsFinite(LayoutSlot.Width))
        {
            return header;
        }

        if (renderSource.TextTrimming == TextTrimming.None && !UseVirtualizedTreeLayout)
        {
            return header;
        }

        var availableWidth = MathF.Max(0f, LayoutSlot.Width - (textX - LayoutSlot.X) - Padding.Right + GetTransformScrollHorizontalOffset());
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

    private float GetTransformScrollHorizontalOffset()
    {
        for (UIElement? current = VisualParent; current != null; current = current.VisualParent)
        {
            if (current is not IScrollTransformContent || current is not UIElement transformContent)
            {
                continue;
            }

            if (transformContent.VisualParent is ScrollViewer visualViewer &&
                ReferenceEquals(visualViewer.Content, transformContent) &&
                ScrollViewer.GetUseTransformContentScrolling(transformContent))
            {
                return MathF.Max(0f, visualViewer.HorizontalOffset);
            }

            if (transformContent.LogicalParent is ScrollViewer logicalViewer &&
                ReferenceEquals(logicalViewer.Content, transformContent) &&
                ScrollViewer.GetUseTransformContentScrolling(transformContent))
            {
                return MathF.Max(0f, logicalViewer.HorizontalOffset);
            }
        }

        return 0f;
    }

    private bool GetEffectiveIsExpanded()
    {
        return _virtualizedDisplaySnapshot?.IsExpanded ?? IsExpanded;
    }

    private float MeasureTemplatedHeaderWidth(Vector2 availableSize, float rowHeight)
    {
        var offset = GetTemplateRootOffset();
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
        return GetVirtualizedDepthOffset() + padding.Left + GetSnapshotTemplateExpanderTextSpacing() + GetBuiltInHeaderTextSpacing();
    }

    private bool ShouldReserveTemplateExpanderSnapshotSlot()
    {
        return _virtualizedDisplaySnapshot.HasValue && !ShowsBuiltInExpander && Template != null;
    }

    private bool ShouldRenderTemplateExpanderSnapshot()
    {
        return ShouldReserveTemplateExpanderSnapshotSlot() && HasChildItems();
    }

    private float GetSnapshotTemplateExpanderTextSpacing()
    {
        return ShouldReserveTemplateExpanderSnapshotSlot()
            ? GetTemplateExpanderSnapshotSlotWidth()
            : 0f;
    }

    private float GetTemplateExpanderSnapshotSlotWidth()
    {
        if (GetTemplateChild("PART_Expander") is FrameworkElement expander)
        {
            if (expander.LayoutSlot.Width > 0f)
            {
                return expander.LayoutSlot.Width;
            }

            if (expander.RenderSize.X > 0f)
            {
                return expander.RenderSize.X;
            }

            if (expander.DesiredSize.X > 0f)
            {
                return expander.DesiredSize.X;
            }

            if (float.IsFinite(expander.Width) && expander.Width > 0f)
            {
                return expander.Width;
            }
        }

        return TemplateExpanderSnapshotFallbackSlotWidth;
    }

    private float GetTemplateRootOffset()
    {
        var padding = Padding;
        return ShowsBuiltInExpander
            ? GetHeaderTextOffset()
            : GetVirtualizedDepthOffset() + padding.Left;
    }

    private float GetVirtualizedDepthOffset()
    {
        return UseVirtualizedTreeLayout ? MathF.Max(0f, VirtualizedTreeDepth) * Indent : 0f;
    }

    private float GetBuiltInHeaderTextSpacing()
    {
        if (!ShowsBuiltInExpander)
        {
            return 0f;
        }

        return HasChildItems() ? 16f : 6f;
    }

    private float GetBuiltInHeaderSpacingWidth()
    {
        if (!ShowsBuiltInExpander)
        {
            return 0f;
        }

        return HasChildItems() ? 20f : 10f;
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
