using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class VisualCommandReplayer
{
    public static bool CanReplay(VisualCommandList commands)
    {
        if (commands.UnsupportedCommandCount > 0)
        {
            return false;
        }

        for (var i = 0; i < commands.Commands.Count; i++)
        {
            var command = commands.Commands[i];
            if (command.Kind == VisualCommandKind.Texture && command.Texture == null)
            {
                return false;
            }

            if (command.Kind is not (VisualCommandKind.FilledRect or VisualCommandKind.RectStroke or VisualCommandKind.Texture))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryReplay(SpriteBatch spriteBatch, VisualCommandList commands)
    {
        return TryReplay(spriteBatch, commands, opacityMultiplier: 1f);
    }

    public static bool TryReplay(SpriteBatch spriteBatch, VisualCommandList commands, float opacityMultiplier)
    {
        if (!CanReplay(commands))
        {
            return false;
        }

        for (var i = 0; i < commands.Commands.Count; i++)
        {
            var command = commands.Commands[i];
            switch (command.Kind)
            {
                case VisualCommandKind.FilledRect:
                    UiDrawing.DrawFilledRect(spriteBatch, command.Rect, command.Color, command.Opacity * opacityMultiplier);
                    break;
                case VisualCommandKind.RectStroke:
                    UiDrawing.DrawRectStroke(spriteBatch, command.Rect, command.Thickness, command.Color, command.Opacity * opacityMultiplier);
                    break;
                case VisualCommandKind.Texture:
                    if (command.Texture == null)
                    {
                        return false;
                    }

                    UiDrawing.DrawTexture(
                        spriteBatch,
                        command.Texture,
                        command.Rect,
                        command.SourceRect,
                        command.Color,
                        command.Opacity * opacityMultiplier);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
