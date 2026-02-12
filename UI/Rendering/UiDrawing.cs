using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class UiDrawing
{
    private static readonly Dictionary<GraphicsDevice, Texture2D> SolidTextures = new();
    private static readonly Dictionary<GraphicsDevice, Stack<Rectangle>> ClipStacks = new();
    private static readonly Dictionary<GraphicsDevice, Stack<Matrix>> TransformStacks = new();

    public static void ResetState(GraphicsDevice graphicsDevice)
    {
        var clipStack = GetClipStack(graphicsDevice);
        clipStack.Clear();
        graphicsDevice.ScissorRectangle = GetViewportRectangle(graphicsDevice);

        var transformStack = GetTransformStack(graphicsDevice);
        transformStack.Clear();
    }

    public static void PushTransform(SpriteBatch spriteBatch, Matrix transform)
    {
        var stack = GetTransformStack(spriteBatch.GraphicsDevice);
        var combined = stack.Count == 0
            ? transform
            : transform * stack.Peek();
        stack.Push(combined);
    }

    public static void PopTransform(SpriteBatch spriteBatch)
    {
        var stack = GetTransformStack(spriteBatch.GraphicsDevice);
        if (stack.Count > 0)
        {
            stack.Pop();
        }
    }

    public static Vector2 TransformPoint(SpriteBatch spriteBatch, Vector2 point)
    {
        var transform = GetCurrentTransform(spriteBatch.GraphicsDevice);
        return Vector2.Transform(point, transform);
    }

    public static float GetScaleX(SpriteBatch spriteBatch)
    {
        return GetCurrentTransform(spriteBatch.GraphicsDevice).M11;
    }

    public static float GetScaleY(SpriteBatch spriteBatch)
    {
        return GetCurrentTransform(spriteBatch.GraphicsDevice).M22;
    }

    public static void DrawFilledRect(SpriteBatch spriteBatch, LayoutRect rect, Color color, float opacity = 1f)
    {
        var transformed = TransformRect(spriteBatch.GraphicsDevice, rect);
        var width = (int)System.MathF.Round(transformed.Width);
        var height = (int)System.MathF.Round(transformed.Height);
        if (width <= 0 || height <= 0 || color.A == 0)
        {
            return;
        }

        var x = (int)System.MathF.Round(transformed.X);
        var y = (int)System.MathF.Round(transformed.Y);
        var texture = GetSolidTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(texture, new Rectangle(x, y, width, height), color * opacity);
    }

    public static void DrawTexture(
        SpriteBatch spriteBatch,
        Texture2D texture,
        LayoutRect destinationRect,
        Rectangle? sourceRect = null,
        Color? color = null,
        float opacity = 1f)
    {
        var transformed = TransformRect(spriteBatch.GraphicsDevice, destinationRect);
        var width = (int)System.MathF.Round(transformed.Width);
        var height = (int)System.MathF.Round(transformed.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x = (int)System.MathF.Round(transformed.X);
        var y = (int)System.MathF.Round(transformed.Y);
        var tint = (color ?? Color.White) * opacity;
        spriteBatch.Draw(texture, new Rectangle(x, y, width, height), sourceRect, tint);
    }

    public static void DrawRectStroke(
        SpriteBatch spriteBatch,
        LayoutRect rect,
        float thickness,
        Color color,
        float opacity = 1f)
    {
        if (thickness <= 0f || color.A == 0)
        {
            return;
        }

        DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, rect.Width, thickness), color, opacity);
        DrawFilledRect(
            spriteBatch,
            new LayoutRect(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness),
            color,
            opacity);
        DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, thickness, rect.Height), color, opacity);
        DrawFilledRect(
            spriteBatch,
            new LayoutRect(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height),
            color,
            opacity);
    }

    public static void DrawFilledCircle(
        SpriteBatch spriteBatch,
        Vector2 center,
        float radius,
        Color color,
        float opacity = 1f)
    {
        if (radius <= 0f || color.A == 0)
        {
            return;
        }

        var minY = (int)System.MathF.Floor(center.Y - radius);
        var maxY = (int)System.MathF.Ceiling(center.Y + radius);
        for (var y = minY; y <= maxY; y++)
        {
            var dy = y - center.Y;
            var span = radius * radius - (dy * dy);
            if (span < 0f)
            {
                continue;
            }

            var dx = System.MathF.Sqrt(span);
            var minX = center.X - dx;
            var maxX = center.X + dx;
            DrawFilledRect(spriteBatch, new LayoutRect(minX, y, (maxX - minX) + 1f, 1f), color, opacity);
        }
    }

    public static void DrawCircleStroke(
        SpriteBatch spriteBatch,
        Vector2 center,
        float radius,
        float thickness,
        Color color,
        float opacity = 1f)
    {
        if (thickness <= 0f || radius <= 0f || color.A == 0)
        {
            return;
        }

        var innerRadius = System.MathF.Max(0f, radius - thickness);
        var minY = (int)System.MathF.Floor(center.Y - radius);
        var maxY = (int)System.MathF.Ceiling(center.Y + radius);
        for (var y = minY; y <= maxY; y++)
        {
            var dy = y - center.Y;
            var outerSpan = radius * radius - (dy * dy);
            if (outerSpan < 0f)
            {
                continue;
            }

            var outerDx = System.MathF.Sqrt(outerSpan);
            var innerSpan = innerRadius * innerRadius - (dy * dy);
            var innerDx = innerSpan > 0f ? System.MathF.Sqrt(innerSpan) : 0f;

            DrawFilledRect(
                spriteBatch,
                new LayoutRect(center.X - outerDx, y, System.MathF.Max(0f, outerDx - innerDx), 1f),
                color,
                opacity);
            DrawFilledRect(
                spriteBatch,
                new LayoutRect(center.X + innerDx, y, System.MathF.Max(0f, outerDx - innerDx), 1f),
                color,
                opacity);
        }
    }

    public static void DrawLine(
        SpriteBatch spriteBatch,
        Vector2 start,
        Vector2 end,
        float thickness,
        Color color,
        float opacity = 1f)
    {
        if (thickness <= 0f || color.A == 0)
        {
            return;
        }

        var transformedStart = TransformPoint(spriteBatch, start);
        var transformedEnd = TransformPoint(spriteBatch, end);
        var direction = transformedEnd - transformedStart;
        var length = direction.Length();
        if (length <= 0.0001f)
        {
            return;
        }

        var angle = System.MathF.Atan2(direction.Y, direction.X);
        var texture = GetSolidTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(
            texture,
            transformedStart,
            null,
            color * opacity,
            angle,
            new Vector2(0f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    public static void DrawPolyline(
        SpriteBatch spriteBatch,
        IReadOnlyList<Vector2> points,
        bool closed,
        float thickness,
        Color color,
        float opacity = 1f)
    {
        if (points.Count < 2 || thickness <= 0f || color.A == 0)
        {
            return;
        }

        for (var i = 1; i < points.Count; i++)
        {
            DrawLine(spriteBatch, points[i - 1], points[i], thickness, color, opacity);
        }

        if (closed)
        {
            DrawLine(spriteBatch, points[^1], points[0], thickness, color, opacity);
        }
    }

    public static void DrawFilledPolygon(
        SpriteBatch spriteBatch,
        IReadOnlyList<Vector2> points,
        Color color,
        float opacity = 1f)
    {
        if (points.Count < 3 || color.A == 0)
        {
            return;
        }

        var transformed = new Vector2[points.Count];
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        for (var i = 0; i < points.Count; i++)
        {
            var point = TransformPoint(spriteBatch, points[i]);
            transformed[i] = point;
            minY = System.MathF.Min(minY, point.Y);
            maxY = System.MathF.Max(maxY, point.Y);
        }

        var scanMin = (int)System.MathF.Floor(minY);
        var scanMax = (int)System.MathF.Ceiling(maxY);
        for (var y = scanMin; y <= scanMax; y++)
        {
            var intersections = new List<float>();
            for (var i = 0; i < transformed.Length; i++)
            {
                var a = transformed[i];
                var b = transformed[(i + 1) % transformed.Length];
                if (System.MathF.Abs(a.Y - b.Y) < 0.0001f)
                {
                    continue;
                }

                var minEdgeY = System.MathF.Min(a.Y, b.Y);
                var maxEdgeY = System.MathF.Max(a.Y, b.Y);
                if (y < minEdgeY || y >= maxEdgeY)
                {
                    continue;
                }

                var t = (y - a.Y) / (b.Y - a.Y);
                intersections.Add(a.X + (t * (b.X - a.X)));
            }

            if (intersections.Count < 2)
            {
                continue;
            }

            intersections.Sort();
            for (var i = 0; i + 1 < intersections.Count; i += 2)
            {
                var start = intersections[i];
                var end = intersections[i + 1];
                if (end <= start)
                {
                    continue;
                }

                var width = end - start;
                var texture = GetSolidTexture(spriteBatch.GraphicsDevice);
                spriteBatch.Draw(
                    texture,
                    new Rectangle(
                        (int)System.MathF.Round(start),
                        y,
                        System.Math.Max(1, (int)System.MathF.Round(width)),
                        1),
                    color * opacity);
            }
        }
    }

    private static Texture2D GetSolidTexture(GraphicsDevice graphicsDevice)
    {
        if (SolidTextures.TryGetValue(graphicsDevice, out var texture))
        {
            return texture;
        }

        texture = new Texture2D(graphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        SolidTextures[graphicsDevice] = texture;
        return texture;
    }

    public static void PushClip(SpriteBatch spriteBatch, LayoutRect rect)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        var stack = GetClipStack(graphicsDevice);
        var clip = ToRectangle(TransformRect(graphicsDevice, rect));
        if (stack.Count > 0)
        {
            clip = Rectangle.Intersect(stack.Peek(), clip);
        }
        else
        {
            clip = Rectangle.Intersect(GetViewportRectangle(graphicsDevice), clip);
        }

        if (clip.Width < 0 || clip.Height < 0)
        {
            clip = Rectangle.Empty;
        }

        stack.Push(clip);
        graphicsDevice.ScissorRectangle = clip;
    }

    public static void PopClip(SpriteBatch spriteBatch)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        var stack = GetClipStack(graphicsDevice);
        if (stack.Count > 0)
        {
            stack.Pop();
        }

        graphicsDevice.ScissorRectangle = stack.Count > 0
            ? stack.Peek()
            : GetViewportRectangle(graphicsDevice);
    }

    private static Stack<Rectangle> GetClipStack(GraphicsDevice graphicsDevice)
    {
        if (ClipStacks.TryGetValue(graphicsDevice, out var stack))
        {
            return stack;
        }

        stack = new Stack<Rectangle>();
        ClipStacks[graphicsDevice] = stack;
        return stack;
    }

    private static Stack<Matrix> GetTransformStack(GraphicsDevice graphicsDevice)
    {
        if (TransformStacks.TryGetValue(graphicsDevice, out var stack))
        {
            return stack;
        }

        stack = new Stack<Matrix>();
        TransformStacks[graphicsDevice] = stack;
        return stack;
    }

    private static Matrix GetCurrentTransform(GraphicsDevice graphicsDevice)
    {
        var stack = GetTransformStack(graphicsDevice);
        return stack.Count == 0 ? Matrix.Identity : stack.Peek();
    }

    private static LayoutRect TransformRect(GraphicsDevice graphicsDevice, LayoutRect rect)
    {
        var transform = GetCurrentTransform(graphicsDevice);
        if (transform == Matrix.Identity)
        {
            return rect;
        }

        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = System.MathF.Min(System.MathF.Min(topLeft.X, topRight.X), System.MathF.Min(bottomLeft.X, bottomRight.X));
        var maxX = System.MathF.Max(System.MathF.Max(topLeft.X, topRight.X), System.MathF.Max(bottomLeft.X, bottomRight.X));
        var minY = System.MathF.Min(System.MathF.Min(topLeft.Y, topRight.Y), System.MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxY = System.MathF.Max(System.MathF.Max(topLeft.Y, topRight.Y), System.MathF.Max(bottomLeft.Y, bottomRight.Y));

        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rectangle ToRectangle(LayoutRect rect)
    {
        var x = (int)System.MathF.Round(rect.X);
        var y = (int)System.MathF.Round(rect.Y);
        var width = System.Math.Max(0, (int)System.MathF.Round(rect.Width));
        var height = System.Math.Max(0, (int)System.MathF.Round(rect.Height));
        return new Rectangle(x, y, width, height);
    }

    private static Rectangle GetViewportRectangle(GraphicsDevice graphicsDevice)
    {
        var viewport = graphicsDevice.Viewport;
        return new Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height);
    }
}
