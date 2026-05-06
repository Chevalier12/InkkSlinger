using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
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

            var rendered = ResolveVirtualizedHeaderRenderText(
                header,
                LayoutSlot.X + GetHeaderTextOffset(),
                ResolveVirtualizedHeaderRenderSource());
            return rendered;
        }
    }

    public bool HitExpander(Vector2 point)
    {
        if (!HasChildItems())
        {
            return false;
        }

        if (!UseVirtualizedTreeLayout && !ShowsBuiltInExpander)
        {
            return HitTemplateExpander(point);
        }

        var rowHeight = GetRowHeight();
        // Hit zone is centred on the glyph centre (glyphCx = X + padding.Left + 7, glyphCy = Y + rowHeight/2)
        // Use a 14×14 box so the small triangle remains easy to click.
        var padding = Padding;
        var hitWidth = MathF.Max(14f, GetVirtualizedExpanderColumnWidth());
        var glyphCx = LayoutSlot.X + GetVirtualizedDepthOffset() + padding.Left + (hitWidth / 2f);
        var glyphCy = LayoutSlot.Y + (rowHeight / 2f);
        var rect = new LayoutRect(glyphCx - (hitWidth / 2f), glyphCy - 7f, hitWidth, 14f);
        return point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }

    private bool HitTemplateExpander(Vector2 point)
    {
        if (GetTemplateChild("PART_Expander") is not UIElement expander ||
            !expander.IsVisible ||
            !expander.IsHitTestVisible ||
            !expander.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            return false;
        }

        return point.X >= bounds.X && point.X <= bounds.X + bounds.Width &&
               point.Y >= bounds.Y && point.Y <= bounds.Y + bounds.Height;
    }

    public bool HasChildItems()
    {
        return HasVirtualizedChildItems || ItemContainers.Count > 0;
    }

    internal void SetVirtualizedHeaderElement(UIElement? element)
    {
        if (ReferenceEquals(_virtualizedHeaderElement, element))
        {
            return;
        }

        if (_virtualizedHeaderElement != null)
        {
            _virtualizedHeaderElement.SetVisualParent(null);
            _virtualizedHeaderElement.SetLogicalParent(null);
        }

        _virtualizedHeaderElement = element;
        _virtualizedHeaderMinRowHeight = 0f;
        if (_virtualizedHeaderElement != null)
        {
            _virtualizedHeaderElement.SetVisualParent(this);
            _virtualizedHeaderElement.SetLogicalParent(this);
            if (_virtualizedHeaderElement is TextBlock textBlock)
            {
                _virtualizedHeaderMinRowHeight = UiTextRenderer.GetLineHeight(textBlock, textBlock.FontSize) + Padding.Vertical + 4f;
            }
        }

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

}
