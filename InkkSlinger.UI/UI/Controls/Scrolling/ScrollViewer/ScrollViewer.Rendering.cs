using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    protected override void OnArrangedSubtreeTranslated(Vector2 delta)
    {
        if (!AreFloatsClose(delta.X, 0f) || !AreFloatsClose(delta.Y, 0f))
        {
            _contentViewportRect = OffsetCachedRect(_contentViewportRect, delta);
            _contentPresenter.OnOwnerTranslated(delta);
        }

        base.OnArrangedSubtreeTranslated(delta);
    }
}
