using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal sealed class VisualRecordingContext
{
    private readonly VisualCommandList _commands;

    public VisualRecordingContext(VisualCommandList commands)
    {
        _commands = commands;
    }

    public void DrawFilledRect(LayoutRect rect, Color color, float opacity = 1f)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || color.A == 0 || opacity <= 0f)
        {
            return;
        }

        _commands.Add(VisualCommand.FilledRect(rect, color, opacity));
    }

    public void DrawRectStroke(LayoutRect rect, float thickness, Color color, float opacity = 1f)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || thickness <= 0f || color.A == 0 || opacity <= 0f)
        {
            return;
        }

        _commands.Add(VisualCommand.RectStroke(rect, thickness, color, opacity));
    }

    public void DrawTextPlaceholder(LayoutRect rect, string text, Color color, float opacity = 1f)
    {
        if (string.IsNullOrEmpty(text) || rect.Width <= 0f || rect.Height <= 0f || color.A == 0 || opacity <= 0f)
        {
            return;
        }

        _commands.Add(VisualCommand.TextPlaceholder(rect, text, color, opacity));
    }

    public void DrawTexture(Texture2D texture, LayoutRect destinationRect, Rectangle? sourceRect = null, Color? color = null, float opacity = 1f)
    {
        if (texture == null || destinationRect.Width <= 0f || destinationRect.Height <= 0f || opacity <= 0f)
        {
            return;
        }

        _commands.Add(VisualCommand.DrawTexture(texture, destinationRect, sourceRect, color ?? Color.White, opacity));
    }

    public void Unsupported(string reason)
    {
        _commands.Add(VisualCommand.Unsupported(reason));
    }
}
