using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem
{
    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var rowHeight = GetRenderRowHeight();
        var padding = Padding;
        var rowRect = new LayoutRect(LayoutSlot.X, LayoutSlot.Y, LayoutSlot.Width, rowHeight);
        if (GetEffectiveIsSelected())
        {
            UiDrawing.DrawFilledRect(spriteBatch, rowRect, SelectedBackground, Opacity);
        }

        if (ShowsBuiltInExpander && HasChildItems())
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

        if (ShouldRenderTemplateExpanderSnapshot())
        {
            if (CanRenderTemplateExpanderTextSnapshot())
            {
                var glyph = GetEffectiveIsExpanded() ? ExpandedExpanderGlyph : CollapsedExpanderGlyph;
                if (!string.IsNullOrEmpty(glyph))
                {
                    var slotWidth = GetTemplateExpanderSnapshotSlotWidth();
                    var renderSource = ResolveVirtualizedExpanderRenderSource();
                    var glyphWidth = UiTextRenderer.MeasureWidth(renderSource.Element, glyph, renderSource.FontSize);
                    var glyphHeight = UiTextRenderer.GetLineHeight(renderSource.Element, renderSource.FontSize);
                    var glyphX = GetVirtualizedExpanderRenderX(slotWidth, glyphWidth);
                    var glyphY = GetVirtualizedExpanderRenderY(rowHeight, glyphHeight);
                    UiTextRenderer.DrawString(
                        spriteBatch,
                        renderSource.Element,
                        glyph,
                        new Vector2(glyphX, glyphY),
                        renderSource.Foreground * Opacity,
                        renderSource.FontSize,
                        opaqueBackground: true);
                }
            }
            else
            {
                DrawTemplateExpanderSnapshotCaret(spriteBatch, rowHeight);
            }
        }

        var header = GetEffectiveHeader();
        if (!string.IsNullOrEmpty(header) && (_virtualizedHeaderElement == null || _virtualizedDisplaySnapshot.HasValue))
        {
            if (HasTemplateRoot && !_virtualizedDisplaySnapshot.HasValue)
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

            var textY = GetVirtualizedHeaderRenderY(rowHeight, renderSource);
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

    private void DrawTemplateExpanderSnapshotCaret(SpriteBatch spriteBatch, float rowHeight)
    {
        var slotWidth = GetTemplateExpanderSnapshotSlotWidth();
        var slotHeight = GetTemplateExpanderSnapshotSlotHeight(rowHeight);
        var slotX = GetVirtualizedExpanderRenderX(slotWidth, slotWidth);
        var slotY = _virtualizedDisplaySnapshot.HasValue && float.IsFinite(_snapshotExpanderRelativeY)
            ? LayoutSlot.Y + _snapshotExpanderRelativeY
            : LayoutSlot.Y + MathF.Max(0f, (rowHeight - slotHeight) / 2f);
        var glyphCx = slotX + (slotWidth / 2f);
        var glyphCy = slotY + (slotHeight / 2f);
        var halfWidth = MathF.Min(4f, MathF.Max(2f, slotWidth * 0.36f));
        var halfHeight = MathF.Min(3f, MathF.Max(2f, slotHeight * 0.28f));
        var glyphColor = Foreground;

        if (GetEffectiveIsExpanded())
        {
            ReadOnlySpan<Vector2> tri =
            [
                new Vector2(glyphCx - halfWidth, glyphCy - halfHeight),
                new Vector2(glyphCx + halfWidth, glyphCy - halfHeight),
                new Vector2(glyphCx, glyphCy + halfHeight),
            ];
            UiDrawing.DrawFilledPolygon(spriteBatch, tri, glyphColor, Opacity);
        }
        else
        {
            ReadOnlySpan<Vector2> tri =
            [
                new Vector2(glyphCx - halfWidth, glyphCy + halfHeight),
                new Vector2(glyphCx + halfWidth, glyphCy + halfHeight),
                new Vector2(glyphCx, glyphCy - halfHeight),
            ];
            UiDrawing.DrawFilledPolygon(spriteBatch, tri, glyphColor, Opacity);
        }
    }

}
