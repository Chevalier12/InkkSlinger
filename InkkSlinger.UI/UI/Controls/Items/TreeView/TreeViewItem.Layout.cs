using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var baseRowHeight = GetRowHeight();
        var rowHeight = MathF.Max(baseRowHeight, _virtualizedHeaderMinRowHeight);
        var rowWidth = UseVirtualizedTreeLayout
            ? MeasureHeaderWidth()
            : HasTemplateRoot
            ? MeasureTemplatedHeaderWidth(availableSize, rowHeight)
            : MeasureHeaderWidth();

        if (_virtualizedHeaderElement is FrameworkElement headerElement)
        {
            var headerOffset = GetHeaderTextOffset();
            headerElement.Measure(new Vector2(
                MathF.Max(0f, availableSize.X - headerOffset - Padding.Right),
                availableSize.Y));
            var headerDesiredHeight = headerElement.DesiredSize.Y;
            var headerDesiredWidth = headerElement.DesiredSize.X;
            if (headerElement is TextBlock textBlock)
            {
                headerDesiredHeight = MathF.Max(headerDesiredHeight, UiTextRenderer.GetLineHeight(textBlock, textBlock.FontSize));
                headerDesiredWidth = MathF.Max(headerDesiredWidth, UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize));
            }

            rowHeight = MathF.Max(rowHeight, headerDesiredHeight + Padding.Vertical + 4f);
            rowWidth = MathF.Max(rowWidth, headerOffset + headerDesiredWidth + Padding.Right);
        }

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

        if (_virtualizedHeaderElement is FrameworkElement headerElement)
        {
            var textX = LayoutSlot.X + GetHeaderTextOffset();
            headerElement.Arrange(new LayoutRect(
                textX,
                LayoutSlot.Y + MathF.Max(0f, (finalSize.Y - headerElement.DesiredSize.Y) / 2f),
                MathF.Max(0f, finalSize.X - (textX - LayoutSlot.X) - Padding.Right),
                headerElement.DesiredSize.Y));
        }

        if (!UseVirtualizedTreeLayout &&
            HasTemplateRoot &&
            TryGetTemplateRoot(out var templateRoot))
        {
            var padding = Padding;
            var textX = LayoutSlot.X + GetTemplateRootOffset();
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

}
