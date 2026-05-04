using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
    internal bool HasVirtualizedDisplaySnapshot => _hasVirtualizedDisplaySnapshot;

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
