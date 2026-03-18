using System;
using System.Buffers;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class UiDrawing
{
    private static readonly Dictionary<GraphicsDevice, Texture2D> SolidTextures = new();
    private static readonly Dictionary<GraphicsDevice, Stack<Rectangle>> ClipStacks = new();
    private static readonly Dictionary<GraphicsDevice, Stack<Matrix>> TransformStacks = new();
    private static readonly Dictionary<GraphicsDevice, SpriteBatchState> ActiveBatchStates = new();
    private static readonly Dictionary<GraphicsDevice, Vector2[]> PolygonVertexBuffers = new();
    private static readonly Dictionary<GraphicsDevice, float[]> PolygonIntersectionBuffers = new();
    private static readonly Dictionary<CircleCacheKey, Vector2[]> CirclePointTemplates = new();
    private static int _frameClipPushCount;
    private static int _frameSpriteBatchRestartCount;

    internal readonly record struct DrawingStateSnapshot(
        Rectangle[] ClipStack,
        Matrix[] TransformStack);

    internal readonly record struct SuspendedSpriteBatchState(
        SpriteSortMode SortMode,
        BlendState BlendState,
        SamplerState SamplerState,
        DepthStencilState DepthStencilState,
        RasterizerState RasterizerState,
        Rectangle ScissorRectangle,
        RenderTargetBinding[] RenderTargets,
        DrawingStateSnapshot DrawingState);

    private readonly record struct SpriteBatchState(
        SpriteSortMode SortMode,
        BlendState BlendState,
        SamplerState SamplerState,
        DepthStencilState DepthStencilState,
        RasterizerState RasterizerState);

    public static void SetActiveBatchState(
        GraphicsDevice graphicsDevice,
        SpriteSortMode sortMode,
        BlendState blendState,
        SamplerState samplerState,
        DepthStencilState depthStencilState,
        RasterizerState rasterizerState)
    {
        ActiveBatchStates[graphicsDevice] = new SpriteBatchState(
            sortMode, blendState, samplerState, depthStencilState, rasterizerState);
    }

    public static void ClearActiveBatchState(GraphicsDevice graphicsDevice)
    {
        ActiveBatchStates.Remove(graphicsDevice);
    }

    internal static void ResetFrameTelemetry()
    {
        _frameClipPushCount = 0;
        _frameSpriteBatchRestartCount = 0;
    }

    internal static (int ClipPushCount, int SpriteBatchRestartCount) ConsumeFrameTelemetry()
    {
        var snapshot = (_frameClipPushCount, _frameSpriteBatchRestartCount);
        ResetFrameTelemetry();
        return snapshot;
    }

    private static void FlushAndRestartBatch(SpriteBatch spriteBatch)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!ActiveBatchStates.TryGetValue(graphicsDevice, out var state))
        {
            return;
        }

        _frameSpriteBatchRestartCount++;
        spriteBatch.End();
        spriteBatch.Begin(
            state.SortMode,
            state.BlendState,
            state.SamplerState,
            state.DepthStencilState,
            state.RasterizerState);
    }

    internal static SuspendedSpriteBatchState SuspendActiveBatch(SpriteBatch spriteBatch)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!ActiveBatchStates.TryGetValue(graphicsDevice, out var state))
        {
            throw new InvalidOperationException("Cannot suspend a SpriteBatch without tracked active state.");
        }

        var suspendedState = new SuspendedSpriteBatchState(
            state.SortMode,
            state.BlendState,
            state.SamplerState,
            state.DepthStencilState,
            state.RasterizerState,
            graphicsDevice.ScissorRectangle,
            graphicsDevice.GetRenderTargets(),
            CaptureDrawingState(graphicsDevice));

        spriteBatch.End();
        ClearActiveBatchState(graphicsDevice);
        return suspendedState;
    }

    internal static void ResumeSuspendedBatch(SpriteBatch spriteBatch, SuspendedSpriteBatchState suspendedState)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (suspendedState.RenderTargets.Length == 0)
        {
            graphicsDevice.SetRenderTarget(null);
        }
        else
        {
            graphicsDevice.SetRenderTargets(suspendedState.RenderTargets);
        }

        graphicsDevice.ScissorRectangle = suspendedState.ScissorRectangle;
        RestoreDrawingState(graphicsDevice, suspendedState.DrawingState);
        spriteBatch.Begin(
            suspendedState.SortMode,
            suspendedState.BlendState,
            suspendedState.SamplerState,
            suspendedState.DepthStencilState,
            suspendedState.RasterizerState);
        SetActiveBatchState(
            graphicsDevice,
            suspendedState.SortMode,
            suspendedState.BlendState,
            suspendedState.SamplerState,
            suspendedState.DepthStencilState,
            suspendedState.RasterizerState);
    }

    public static void ResetState(GraphicsDevice graphicsDevice)
    {
        var clipStack = GetClipStack(graphicsDevice);
        clipStack.Clear();
        graphicsDevice.ScissorRectangle = GetViewportRectangle(graphicsDevice);

        var transformStack = GetTransformStack(graphicsDevice);
        transformStack.Clear();
    }

    internal static void ReleaseDeviceResources(GraphicsDevice? graphicsDevice)
    {
        if (graphicsDevice == null)
        {
            return;
        }

        ReleaseDeviceResourcesCore(graphicsDevice, disposeSolidTexture: true);
    }

    internal static void ReleaseDeviceResourcesForTests(GraphicsDevice graphicsDevice)
    {
        ReleaseDeviceResourcesCore(graphicsDevice, disposeSolidTexture: false);
    }

    internal static DrawingStateSnapshot CaptureDrawingStateForTests(GraphicsDevice graphicsDevice)
    {
        return CaptureDrawingState(graphicsDevice);
    }

    internal static void RestoreDrawingStateForTests(GraphicsDevice graphicsDevice, DrawingStateSnapshot snapshot)
    {
        RestoreDrawingState(graphicsDevice, snapshot);
    }

    internal static void ClearDrawingStateForTests(GraphicsDevice graphicsDevice)
    {
        GetClipStack(graphicsDevice).Clear();
        GetTransformStack(graphicsDevice).Clear();
    }

    internal static void ConfigureDrawingStateForTests(
        GraphicsDevice graphicsDevice,
        IReadOnlyList<Rectangle> clips,
        IReadOnlyList<Matrix> transforms,
        bool includePolygonBuffer = false)
    {
        var clipStack = GetClipStack(graphicsDevice);
        clipStack.Clear();
        for (var i = 0; i < clips.Count; i++)
        {
            clipStack.Push(clips[i]);
        }

        var transformStack = GetTransformStack(graphicsDevice);
        transformStack.Clear();
        for (var i = 0; i < transforms.Count; i++)
        {
            transformStack.Push(transforms[i]);
        }

        if (includePolygonBuffer)
        {
            if (PolygonIntersectionBuffers.TryGetValue(graphicsDevice, out var existing))
            {
                ArrayPool<float>.Shared.Return(existing, clearArray: false);
            }

            PolygonIntersectionBuffers[graphicsDevice] = ArrayPool<float>.Shared.Rent(16);
        }
    }

    internal static (int ClipCount, int TransformCount, bool HasPolygonBuffer) GetDrawingStateInfoForTests(GraphicsDevice graphicsDevice)
    {
        return (
            GetClipStack(graphicsDevice).Count,
            GetTransformStack(graphicsDevice).Count,
            PolygonIntersectionBuffers.ContainsKey(graphicsDevice));
    }

    internal static Rectangle GetCurrentClipForTests(GraphicsDevice graphicsDevice)
    {
        var stack = GetClipStack(graphicsDevice);
        return stack.Count > 0 ? stack.Peek() : Rectangle.Empty;
    }

    internal static Rectangle PushLocalStateForTests(
        GraphicsDevice graphicsDevice,
        bool hasTransform,
        Matrix localTransform,
        bool hasClip,
        LayoutRect clipRect)
    {
        PushLocalStateCore(graphicsDevice, null, hasTransform, localTransform, hasClip, clipRect);
        return GetCurrentClipForTests(graphicsDevice);
    }

    internal static Rectangle PopLocalStateForTests(
        GraphicsDevice graphicsDevice,
        bool hasTransform,
        bool hasClip)
    {
        PopLocalStateCore(graphicsDevice, null, hasTransform, hasClip);
        return GetCurrentClipForTests(graphicsDevice);
    }

    internal static void PushLocalState(
        SpriteBatch spriteBatch,
        bool hasTransform,
        Matrix localTransform,
        bool hasClip,
        LayoutRect clipRect)
    {
        PushLocalStateCore(spriteBatch.GraphicsDevice, spriteBatch, hasTransform, localTransform, hasClip, clipRect);
    }

    internal static void PopLocalState(
        SpriteBatch spriteBatch,
        bool hasTransform,
        bool hasClip)
    {
        PopLocalStateCore(spriteBatch.GraphicsDevice, spriteBatch, hasTransform, hasClip);
    }

    public static void PushTransform(SpriteBatch spriteBatch, Matrix transform)
    {
        PushTransformCore(spriteBatch.GraphicsDevice, transform);
    }

    private static void PushLocalStateCore(
        GraphicsDevice graphicsDevice,
        SpriteBatch? spriteBatch,
        bool hasTransform,
        Matrix localTransform,
        bool hasClip,
        LayoutRect clipRect)
    {
        if (hasTransform)
        {
            PushTransformCore(graphicsDevice, localTransform);
        }

        if (hasClip)
        {
            _ = PushClipCore(graphicsDevice, spriteBatch, clipRect, transformRect: true);
        }
    }

    private static void PopLocalStateCore(
        GraphicsDevice graphicsDevice,
        SpriteBatch? spriteBatch,
        bool hasTransform,
        bool hasClip)
    {
        if (hasClip)
        {
            _ = PopClipCore(graphicsDevice, spriteBatch);
        }

        if (hasTransform)
        {
            PopTransformCore(graphicsDevice);
        }
    }

    private static void PushTransformCore(GraphicsDevice graphicsDevice, Matrix transform)
    {
        var stack = GetTransformStack(graphicsDevice);
        var combined = stack.Count == 0
            ? transform
            : transform * stack.Peek();
        stack.Push(combined);
    }

    public static void PopTransform(SpriteBatch spriteBatch)
    {
        PopTransformCore(spriteBatch.GraphicsDevice);
    }

    private static void PopTransformCore(GraphicsDevice graphicsDevice)
    {
        var stack = GetTransformStack(graphicsDevice);
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

    internal static LayoutRect TransformRectBounds(SpriteBatch spriteBatch, LayoutRect rect)
    {
        return TransformRect(spriteBatch.GraphicsDevice, rect);
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

    internal static void DrawFilledRectPixels(SpriteBatch spriteBatch, Rectangle rect, Color color, float opacity = 1f)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0)
        {
            return;
        }

        var texture = GetSolidTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(texture, rect, color * opacity);
    }

    internal static void DrawTexturePixels(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle destinationRect,
        Rectangle? sourceRect = null,
        Color? color = null,
        float opacity = 1f)
    {
        if (destinationRect.Width <= 0 || destinationRect.Height <= 0)
        {
            return;
        }

        var tint = (color ?? Color.White) * opacity;
        spriteBatch.Draw(texture, destinationRect, sourceRect, tint);
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

        var points = GetCirclePoints(radius);
        PushTransform(spriteBatch, Matrix.CreateTranslation(center.X, center.Y, 0f));
        try
        {
            DrawFilledPolygon(spriteBatch, points, color, opacity);
        }
        finally
        {
            PopTransform(spriteBatch);
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

        var points = GetCirclePoints(radius);
        PushTransform(spriteBatch, Matrix.CreateTranslation(center.X, center.Y, 0f));
        try
        {
            DrawPolyline(spriteBatch, points, closed: true, thickness, color, opacity);
        }
        finally
        {
            PopTransform(spriteBatch);
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

        var pointCount = points.Count;
        var transformed = GetPolygonVertexBuffer(spriteBatch.GraphicsDevice, pointCount);
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        for (var i = 0; i < pointCount; i++)
        {
            var point = TransformPoint(spriteBatch, points[i]);
            transformed[i] = point;
            minY = System.MathF.Min(minY, point.Y);
            maxY = System.MathF.Max(maxY, point.Y);
        }

        FillPolygon(spriteBatch, transformed, pointCount, minY, maxY, color, opacity);
    }

    public static void DrawFilledPolygon(
        SpriteBatch spriteBatch,
        ReadOnlySpan<Vector2> points,
        Color color,
        float opacity = 1f)
    {
        if (points.Length < 3 || color.A == 0)
        {
            return;
        }

        var pointCount = points.Length;
        var transformed = GetPolygonVertexBuffer(spriteBatch.GraphicsDevice, pointCount);
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        for (var i = 0; i < pointCount; i++)
        {
            var point = TransformPoint(spriteBatch, points[i]);
            transformed[i] = point;
            minY = System.MathF.Min(minY, point.Y);
            maxY = System.MathF.Max(maxY, point.Y);
        }

        FillPolygon(spriteBatch, transformed, pointCount, minY, maxY, color, opacity);
    }

    private static void FillPolygon(
        SpriteBatch spriteBatch,
        Vector2[] transformed,
        int pointCount,
        float minY,
        float maxY,
        Color color,
        float opacity)
    {

        var scanMin = (int)System.MathF.Floor(minY);
        var scanMax = (int)System.MathF.Ceiling(maxY);
        var intersectionBuffer = GetPolygonIntersectionBuffer(spriteBatch.GraphicsDevice, pointCount);
        for (var y = scanMin; y <= scanMax; y++)
        {
            var intersectionCount = 0;
            for (var i = 0; i < pointCount; i++)
            {
                var a = transformed[i];
                var b = transformed[(i + 1) % pointCount];
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
                if (intersectionCount == intersectionBuffer.Length)
                {
                    intersectionBuffer = GrowPolygonIntersectionBuffer(spriteBatch.GraphicsDevice, intersectionBuffer.Length * 2);
                }

                intersectionBuffer[intersectionCount++] = a.X + (t * (b.X - a.X));
            }

            if (intersectionCount < 2)
            {
                continue;
            }

            Array.Sort(intersectionBuffer, 0, intersectionCount);
            for (var i = 0; i + 1 < intersectionCount; i += 2)
            {
                var start = intersectionBuffer[i];
                var end = intersectionBuffer[i + 1];
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
        _ = PushClipCore(spriteBatch.GraphicsDevice, spriteBatch, rect, transformRect: true);
    }

    internal static void PushAbsoluteClip(SpriteBatch spriteBatch, LayoutRect rect)
    {
        _ = PushClipCore(spriteBatch.GraphicsDevice, spriteBatch, rect, transformRect: false);
    }

    private static Rectangle PushClipCore(
        GraphicsDevice graphicsDevice,
        SpriteBatch? spriteBatch,
        LayoutRect rect,
        bool transformRect)
    {
        var stack = GetClipStack(graphicsDevice);
        var clip = transformRect
            ? ToRectangle(TransformRect(graphicsDevice, rect))
            : ToRectangle(rect);
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
        _frameClipPushCount++;
        if (spriteBatch != null && graphicsDevice.ScissorRectangle != clip)
        {
            FlushAndRestartBatch(spriteBatch);
            graphicsDevice.ScissorRectangle = clip;
        }

        return clip;
    }

    public static void PopClip(SpriteBatch spriteBatch)
    {
        _ = PopClipCore(spriteBatch.GraphicsDevice, spriteBatch);
    }

    private static Rectangle PopClipCore(GraphicsDevice graphicsDevice, SpriteBatch? spriteBatch)
    {
        var stack = GetClipStack(graphicsDevice);
        if (stack.Count > 0)
        {
            stack.Pop();
        }

        var nextClip = stack.Count > 0
            ? stack.Peek()
            : GetViewportRectangle(graphicsDevice);
        if (spriteBatch != null && graphicsDevice.ScissorRectangle != nextClip)
        {
            FlushAndRestartBatch(spriteBatch);
            graphicsDevice.ScissorRectangle = nextClip;
        }

        return nextClip;
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

    private static DrawingStateSnapshot CaptureDrawingState(GraphicsDevice graphicsDevice)
    {
        return new DrawingStateSnapshot(
            SnapshotStack(GetClipStack(graphicsDevice)),
            SnapshotStack(GetTransformStack(graphicsDevice)));
    }

    private static void RestoreDrawingState(GraphicsDevice graphicsDevice, DrawingStateSnapshot snapshot)
    {
        RestoreStack(GetClipStack(graphicsDevice), snapshot.ClipStack);
        RestoreStack(GetTransformStack(graphicsDevice), snapshot.TransformStack);
    }

    private static T[] SnapshotStack<T>(Stack<T> stack)
    {
        if (stack.Count == 0)
        {
            return Array.Empty<T>();
        }

        var snapshot = stack.ToArray();
        Array.Reverse(snapshot);
        return snapshot;
    }

    private static void RestoreStack<T>(Stack<T> stack, IReadOnlyList<T> snapshot)
    {
        stack.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            stack.Push(snapshot[i]);
        }
    }

    private static void ReleaseDeviceResourcesCore(GraphicsDevice graphicsDevice, bool disposeSolidTexture)
    {
        if (SolidTextures.Remove(graphicsDevice, out var texture) && disposeSolidTexture)
        {
            texture.Dispose();
        }

        if (PolygonVertexBuffers.Remove(graphicsDevice, out var vertexBuffer))
        {
            ArrayPool<Vector2>.Shared.Return(vertexBuffer, clearArray: false);
        }

        if (PolygonIntersectionBuffers.Remove(graphicsDevice, out var buffer))
        {
            ArrayPool<float>.Shared.Return(buffer, clearArray: false);
        }

        ClipStacks.Remove(graphicsDevice);
        TransformStacks.Remove(graphicsDevice);
        ActiveBatchStates.Remove(graphicsDevice);
    }

    private static float[] GetPolygonIntersectionBuffer(GraphicsDevice graphicsDevice, int requiredSize)
    {
        if (PolygonIntersectionBuffers.TryGetValue(graphicsDevice, out var buffer) && buffer.Length >= requiredSize)
        {
            return buffer;
        }

        return GrowPolygonIntersectionBuffer(graphicsDevice, Math.Max(requiredSize, 16));
    }

    private static Vector2[] GetPolygonVertexBuffer(GraphicsDevice graphicsDevice, int requiredSize)
    {
        if (PolygonVertexBuffers.TryGetValue(graphicsDevice, out var buffer) && buffer.Length >= requiredSize)
        {
            return buffer;
        }

        return GrowPolygonVertexBuffer(graphicsDevice, Math.Max(requiredSize, 16));
    }

    private static Vector2[] GrowPolygonVertexBuffer(GraphicsDevice graphicsDevice, int requiredSize)
    {
        var newBuffer = ArrayPool<Vector2>.Shared.Rent(requiredSize);
        if (PolygonVertexBuffers.TryGetValue(graphicsDevice, out var existing))
        {
            ArrayPool<Vector2>.Shared.Return(existing, clearArray: false);
        }

        PolygonVertexBuffers[graphicsDevice] = newBuffer;
        return newBuffer;
    }

    private static float[] GrowPolygonIntersectionBuffer(GraphicsDevice graphicsDevice, int requiredSize)
    {
        var newBuffer = ArrayPool<float>.Shared.Rent(requiredSize);
        if (PolygonIntersectionBuffers.TryGetValue(graphicsDevice, out var existing))
        {
            ArrayPool<float>.Shared.Return(existing, clearArray: false);
        }

        PolygonIntersectionBuffers[graphicsDevice] = newBuffer;
        return newBuffer;
    }

    private static IReadOnlyList<Vector2> GetCirclePoints(float radius)
    {
        var segmentCount = Math.Clamp((int)MathF.Ceiling(radius * 1.5f), 12, 48);
        var radiusBucket = Math.Clamp((int)MathF.Round(radius * 100f), 1, 8192);
        var key = new CircleCacheKey(radiusBucket, segmentCount);
        if (CirclePointTemplates.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bucketedRadius = radiusBucket / 100f;
        var points = new Vector2[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var angle = (MathF.PI * 2f * i) / segmentCount;
            points[i] = new Vector2(
                MathF.Cos(angle) * bucketedRadius,
                MathF.Sin(angle) * bucketedRadius);
        }

        CirclePointTemplates[key] = points;
        return points;
    }

    private readonly record struct CircleCacheKey(int RadiusBucket, int SegmentCount);

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
