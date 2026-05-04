using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
    internal bool HasVirtualizedDisplaySnapshot => _virtualizedDisplaySnapshot.HasValue;

    internal bool HasVirtualizedDisplaySnapshotForDiagnostics => HasVirtualizedDisplaySnapshot;

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
            if (HasVirtualizedDisplaySnapshotForDiagnostics &&
                string.Equals(rendered, header, StringComparison.Ordinal) &&
                header.Length > 12)
            {
                return header[..12] + "...";
            }

            return rendered;
        }
    }

    public bool HitExpander(Vector2 point)
    {
        if (!HasChildItems())
        {
            return false;
        }

        if (!ShowsBuiltInExpander)
        {
            return HitTemplateExpander(point);
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
        return _virtualizedDisplaySnapshot.HasValue
            ? _virtualizedDisplaySnapshot.Value.HasChildren
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
        _virtualizedDisplaySnapshot = new VirtualizedDisplaySnapshot(
            header,
            hasChildren,
            isExpanded,
            isSelected);
        _suppressExpanderPresentationUpdates = true;
        try
        {
            HasItems = hasChildren;
        }
        finally
        {
            _suppressExpanderPresentationUpdates = false;
        }

        UseVirtualizedTreeLayout = true;
        VirtualizedTreeDepth = depth;
        VirtualizedTreeRowIndex = rowIndex;
        InvalidateVisual();
    }

    internal void ClearVirtualizedDisplaySnapshot(bool updateHasItems = true)
    {
        if (!_virtualizedDisplaySnapshot.HasValue)
        {
            return;
        }

        _virtualizedDisplaySnapshot = null;
        if (updateHasItems)
        {
            UpdateHasItems();
        }
        InvalidateVisual();
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
