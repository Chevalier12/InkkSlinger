using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal sealed class RetainedCompositionCompositor
{
    public RetainedCompositionDrawMetrics Draw(
        RetainedCompositionGraph graph,
        VisualRecordStore records,
        SpriteBatch? spriteBatch,
        LayoutRect clipRect,
        List<UIElement>? drawOrder = null)
    {
        if (graph.NodeCount == 0 || clipRect.Width <= 0f || clipRect.Height <= 0f)
        {
            return RetainedCompositionDrawMetrics.Empty;
        }

        var metrics = new MutableMetrics();
        var activePath = new List<RetainedCompositionNode>();
        var transformStack = new List<Matrix> { Matrix.Identity };
        var opacityStack = new List<float> { 1f };

        try
        {
            var nodes = graph.Nodes;
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                PopToDepth(spriteBatch, activePath, transformStack, opacityStack, node.Depth);
                metrics.NodesVisited++;

                if (!node.IsEffectivelyVisible || node.Opacity <= 0f)
                {
                    nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                    metrics.SubtreesCulled++;
                    continue;
                }

                var parentTransform = transformStack[^1];
                var nodeTransform = node.HasLocalTransform
                    ? node.LocalTransform * parentTransform
                    : parentTransform;
                var effectiveOpacity = opacityStack[^1] * node.Opacity;

                var cullStart = Stopwatch.GetTimestamp();
                if (node.HasSubtreeBounds &&
                    !Intersects(node.SubtreeBounds, clipRect))
                {
                    metrics.CullingTicks += Stopwatch.GetTimestamp() - cullStart;
                    nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                    metrics.SubtreesCulled++;
                    continue;
                }

                var shouldDrawSelf = ShouldDrawSelf(node, clipRect);
                metrics.CullingTicks += Stopwatch.GetTimestamp() - cullStart;

                PushNodeState(spriteBatch, node, activePath, transformStack, opacityStack, nodeTransform, effectiveOpacity, ref metrics);
                if (!shouldDrawSelf)
                {
                    metrics.SelfCulled++;
                    continue;
                }

                drawOrder?.Add(node.Visual);
                metrics.NodesDrawn++;
                metrics.LastDrawOpacity = effectiveOpacity;
                ClassifyCacheBoundary(spriteBatch, node, ref metrics);
                if (spriteBatch == null)
                {
                    ClassifyDryRunRecord(records, node, ref metrics);
                    continue;
                }

                var replayStart = Stopwatch.GetTimestamp();
                DrawNodeSelf(spriteBatch, records, node, effectiveOpacity, ref metrics);
                metrics.CommandReplayTicks += Stopwatch.GetTimestamp() - replayStart;
            }
        }
        finally
        {
            PopToDepth(spriteBatch, activePath, transformStack, opacityStack, 0);
        }

        return metrics.ToImmutable();
    }

    private static void PushNodeState(
        SpriteBatch? spriteBatch,
        RetainedCompositionNode node,
        List<RetainedCompositionNode> activePath,
        List<Matrix> transformStack,
        List<float> opacityStack,
        Matrix nodeTransform,
        float effectiveOpacity,
        ref MutableMetrics metrics)
    {
        if (spriteBatch != null)
        {
            UiDrawing.PushLocalState(
                spriteBatch,
                node.HasLocalTransform,
                node.LocalTransform,
                node.HasLocalClip,
                node.LocalClip);
        }

        if (node.HasLocalTransform)
        {
            metrics.TransformPushCount++;
        }

        if (node.HasLocalClip)
        {
            metrics.ClipPushCount++;
        }

        if (node.Opacity < 1f)
        {
            metrics.OpacityPushCount++;
        }

        activePath.Add(node);
        transformStack.Add(nodeTransform);
        opacityStack.Add(effectiveOpacity);
        metrics.MaxStackDepth = Math.Max(metrics.MaxStackDepth, activePath.Count);
    }

    private static void PopToDepth(
        SpriteBatch? spriteBatch,
        List<RetainedCompositionNode> activePath,
        List<Matrix> transformStack,
        List<float> opacityStack,
        int depth)
    {
        while (activePath.Count > depth)
        {
            var node = activePath[^1];
            if (spriteBatch != null)
            {
                UiDrawing.PopLocalState(spriteBatch, node.HasLocalTransform, node.HasLocalClip);
            }

            activePath.RemoveAt(activePath.Count - 1);
            transformStack.RemoveAt(transformStack.Count - 1);
            opacityStack.RemoveAt(opacityStack.Count - 1);
        }
    }

    private static bool ShouldDrawSelf(RetainedCompositionNode node, LayoutRect clipRect)
    {
        return !node.HasBounds || Intersects(node.Bounds, clipRect);
    }

    private static void DrawNodeSelf(
        SpriteBatch spriteBatch,
        VisualRecordStore records,
        RetainedCompositionNode node,
        float effectiveOpacity,
        ref MutableMetrics metrics)
    {
        if (!records.TryGetRecord(node.Visual, out var commands))
        {
            metrics.CommandReplayFallbackCount++;
            node.Visual.DrawSelf(spriteBatch);
            return;
        }

        var slot = node.Visual.LayoutSlot;
        var hasSlotTranslation = slot.X != 0f || slot.Y != 0f;
        if (hasSlotTranslation)
        {
            UiDrawing.PushTransform(spriteBatch, Matrix.CreateTranslation(slot.X, slot.Y, 0f));
        }

        try
        {
            if (VisualCommandReplayer.TryReplay(spriteBatch, commands, effectiveOpacity))
            {
                metrics.CommandReplayCount++;
                return;
            }
        }
        finally
        {
            if (hasSlotTranslation)
            {
                UiDrawing.PopTransform(spriteBatch);
            }
        }

        metrics.CommandReplayFallbackCount++;
        if (commands.UnsupportedCommandCount > 0)
        {
            metrics.UnsupportedCommandFallbackCount++;
        }

        node.Visual.DrawSelf(spriteBatch);
    }

    private static void ClassifyDryRunRecord(
        VisualRecordStore records,
        RetainedCompositionNode node,
        ref MutableMetrics metrics)
    {
        if (!records.TryGetRecord(node.Visual, out var commands))
        {
            metrics.CommandReplayFallbackCount++;
            return;
        }

        if (VisualCommandReplayer.CanReplay(commands))
        {
            metrics.CommandReplayCount++;
            return;
        }

        metrics.CommandReplayFallbackCount++;
        if (commands.UnsupportedCommandCount > 0)
        {
            metrics.UnsupportedCommandFallbackCount++;
        }
    }

    private static void ClassifyCacheBoundary(
        SpriteBatch? spriteBatch,
        RetainedCompositionNode node,
        ref MutableMetrics metrics)
    {
        if (node.CacheMode == RetainedCompositionCacheMode.None)
        {
            return;
        }

        metrics.CacheModeBoundaryCount++;
        if (node.CacheMode == RetainedCompositionCacheMode.Bitmap)
        {
            metrics.BitmapCacheBoundaryCount++;
            metrics.DeferredBitmapCacheBoundaryCount++;
        }

        var key = node.CacheKey;
        if (spriteBatch != null)
        {
            var viewport = spriteBatch.GraphicsDevice.Viewport;
            key = key with
            {
                DeviceWidth = viewport.Width,
                DeviceHeight = viewport.Height
            };
        }

        metrics.LastCacheKey = key;
    }


    private static bool Intersects(LayoutRect first, LayoutRect second)
    {
        return first.Width > 0f &&
               first.Height > 0f &&
               second.Width > 0f &&
               second.Height > 0f &&
               first.X < second.X + second.Width &&
               first.X + first.Width > second.X &&
               first.Y < second.Y + second.Height &&
               first.Y + first.Height > second.Y;
    }

    private struct MutableMetrics
    {
        public int NodesVisited;
        public int NodesDrawn;
        public int ClipPushCount;
        public int TransformPushCount;
        public int OpacityPushCount;
        public int SubtreesCulled;
        public int SelfCulled;
        public int CommandReplayCount;
        public int CommandReplayFallbackCount;
        public int UnsupportedCommandFallbackCount;
        public int CacheModeBoundaryCount;
        public int BitmapCacheBoundaryCount;
        public int DeferredBitmapCacheBoundaryCount;
        public int MaxStackDepth;
        public float LastDrawOpacity;
        public RetainedCompositionCacheKey LastCacheKey;
        public long CullingTicks;
        public long CommandReplayTicks;

        public RetainedCompositionDrawMetrics ToImmutable()
        {
            return new RetainedCompositionDrawMetrics(
                NodesVisited,
                NodesDrawn,
                ClipPushCount,
                TransformPushCount,
                OpacityPushCount,
                SubtreesCulled,
                SelfCulled,
                CommandReplayCount,
                CommandReplayFallbackCount,
                UnsupportedCommandFallbackCount,
                CacheModeBoundaryCount,
                BitmapCacheBoundaryCount,
                DeferredBitmapCacheBoundaryCount,
                MaxStackDepth,
                LastDrawOpacity,
                LastCacheKey,
                TicksToMilliseconds(CullingTicks),
                TicksToMilliseconds(CommandReplayTicks));
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks <= 0L ? 0d : ticks * 1000d / Stopwatch.Frequency;
        }
    }
}

internal readonly record struct RetainedCompositionDrawMetrics(
    int NodesVisited,
    int NodesDrawn,
    int ClipPushCount,
    int TransformPushCount,
    int OpacityPushCount,
    int SubtreesCulled,
    int SelfCulled,
    int CommandReplayCount,
    int CommandReplayFallbackCount,
    int UnsupportedCommandFallbackCount,
    int CacheModeBoundaryCount,
    int BitmapCacheBoundaryCount,
    int DeferredBitmapCacheBoundaryCount,
    int MaxStackDepth,
    float LastDrawOpacity,
    RetainedCompositionCacheKey LastCacheKey,
    double CullingMilliseconds,
    double CommandReplayMilliseconds)
{
    public static RetainedCompositionDrawMetrics Empty { get; } = new(
        NodesVisited: 0,
        NodesDrawn: 0,
        ClipPushCount: 0,
        TransformPushCount: 0,
        OpacityPushCount: 0,
        SubtreesCulled: 0,
        SelfCulled: 0,
        CommandReplayCount: 0,
        CommandReplayFallbackCount: 0,
        UnsupportedCommandFallbackCount: 0,
        CacheModeBoundaryCount: 0,
        BitmapCacheBoundaryCount: 0,
        DeferredBitmapCacheBoundaryCount: 0,
        MaxStackDepth: 0,
        LastDrawOpacity: 0f,
        LastCacheKey: default,
        CullingMilliseconds: 0d,
        CommandReplayMilliseconds: 0d);
}
