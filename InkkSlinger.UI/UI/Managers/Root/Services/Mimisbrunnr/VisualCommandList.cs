using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal enum VisualCommandKind
{
    Unsupported,
    FilledRect,
    RectStroke,
    TextPlaceholder,
    Texture
}

internal readonly record struct VisualCommand(
    VisualCommandKind Kind,
    LayoutRect Rect,
    Color Color,
    float Thickness,
    float Opacity,
    string Text,
    Texture2D? Texture,
    Rectangle? SourceRect)
{
    public static VisualCommand Unsupported(string reason)
    {
        return new VisualCommand(
            VisualCommandKind.Unsupported,
            default,
            Color.Transparent,
            0f,
            1f,
            reason,
            null,
            null);
    }

    public static VisualCommand FilledRect(LayoutRect rect, Color color, float opacity)
    {
        return new VisualCommand(
            VisualCommandKind.FilledRect,
            rect,
            color,
            0f,
            opacity,
            string.Empty,
            null,
            null);
    }

    public static VisualCommand RectStroke(LayoutRect rect, float thickness, Color color, float opacity)
    {
        return new VisualCommand(
            VisualCommandKind.RectStroke,
            rect,
            color,
            thickness,
            opacity,
            string.Empty,
            null,
            null);
    }

    public static VisualCommand TextPlaceholder(LayoutRect rect, string text, Color color, float opacity)
    {
        return new VisualCommand(
            VisualCommandKind.TextPlaceholder,
            rect,
            color,
            0f,
            opacity,
            text,
            null,
            null);
    }

    public static VisualCommand DrawTexture(Texture2D texture, LayoutRect rect, Rectangle? sourceRect, Color color, float opacity)
    {
        return new VisualCommand(
            VisualCommandKind.Texture,
            rect,
            color,
            0f,
            opacity,
            string.Empty,
            texture,
            sourceRect);
    }
}

internal sealed class VisualCommandList
{
    private readonly List<VisualCommand> _commands = new();

    public IReadOnlyList<VisualCommand> Commands => _commands;

    public int Count => _commands.Count;

    public int UnsupportedCommandCount { get; private set; }

    internal void Add(VisualCommand command)
    {
        if (command.Kind == VisualCommandKind.Unsupported ||
            command.Kind == VisualCommandKind.TextPlaceholder)
        {
            UnsupportedCommandCount++;
        }

        _commands.Add(command);
    }
}
