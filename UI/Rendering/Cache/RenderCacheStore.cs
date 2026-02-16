using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal sealed class RenderCacheStore : IDisposable
{
    private readonly Dictionary<UIElement, RenderCacheEntry> _entries = new();
    private readonly LinkedList<UIElement> _lru = new();
    private readonly Dictionary<UIElement, LinkedListNode<UIElement>> _lruNodes = new();
    private readonly int _maxCacheCount;
    private readonly long _maxCacheBytes;
    private GraphicsDevice? _graphicsDevice;

    public RenderCacheStore(int maxCacheCount, long maxCacheBytes)
    {
        _maxCacheCount = Math.Max(1, maxCacheCount);
        _maxCacheBytes = Math.Max(1L, maxCacheBytes);
    }

    public int Count => _entries.Count;

    public long TotalBytes { get; private set; }

    public void EnsureDevice(GraphicsDevice graphicsDevice)
    {
        if (ReferenceEquals(_graphicsDevice, graphicsDevice))
        {
            return;
        }

        Clear();
        _graphicsDevice = graphicsDevice;
    }

    public bool TryGet(UIElement visual, out RenderCacheEntry entry)
    {
        if (!_entries.TryGetValue(visual, out entry!))
        {
            return false;
        }

        Touch(visual);
        return true;
    }

    public void Upsert(
        UIElement visual,
        RenderTarget2D renderTarget,
        LayoutRect bounds,
        int renderVersionStamp,
        int layoutVersionStamp,
        int renderStateSignature)
    {
        if (_entries.Remove(visual, out var existing))
        {
            TotalBytes = Math.Max(0L, TotalBytes - existing.ByteSize);
            if (!ReferenceEquals(existing.RenderTarget, renderTarget))
            {
                existing.RenderTarget.Dispose();
            }

            if (_lruNodes.Remove(visual, out var existingNode))
            {
                _lru.Remove(existingNode);
            }
        }

        var byteSize = EstimateByteSize(renderTarget.Width, renderTarget.Height);
        var entry = new RenderCacheEntry(
            renderTarget,
            bounds,
            renderVersionStamp,
            layoutVersionStamp,
            renderStateSignature,
            byteSize);
        _entries[visual] = entry;
        TotalBytes += byteSize;
        Touch(visual);
        TrimToBudget();
    }

    public void Remove(UIElement visual)
    {
        if (!_entries.Remove(visual, out var existing))
        {
            return;
        }

        TotalBytes = Math.Max(0L, TotalBytes - existing.ByteSize);
        existing.RenderTarget.Dispose();
        if (_lruNodes.Remove(visual, out var node))
        {
            _lru.Remove(node);
        }
    }

    public void Clear()
    {
        foreach (var pair in _entries)
        {
            pair.Value.RenderTarget.Dispose();
        }

        _entries.Clear();
        _lru.Clear();
        _lruNodes.Clear();
        TotalBytes = 0L;
    }

    public void Dispose()
    {
        Clear();
    }

    private void TrimToBudget()
    {
        while (_entries.Count > _maxCacheCount || TotalBytes > _maxCacheBytes)
        {
            var tail = _lru.Last;
            if (tail == null)
            {
                break;
            }

            Remove(tail.Value);
        }
    }

    private void Touch(UIElement visual)
    {
        if (_lruNodes.Remove(visual, out var existingNode))
        {
            _lru.Remove(existingNode);
        }

        _lruNodes[visual] = _lru.AddFirst(visual);
    }

    private static long EstimateByteSize(int width, int height)
    {
        return (long)Math.Max(1, width) * Math.Max(1, height) * 4L;
    }
}

internal sealed class RenderCacheEntry
{
    public RenderCacheEntry(
        RenderTarget2D renderTarget,
        LayoutRect bounds,
        int renderVersionStamp,
        int layoutVersionStamp,
        int renderStateSignature,
        long byteSize)
    {
        RenderTarget = renderTarget;
        Bounds = bounds;
        RenderVersionStamp = renderVersionStamp;
        LayoutVersionStamp = layoutVersionStamp;
        RenderStateSignature = renderStateSignature;
        ByteSize = byteSize;
    }

    public RenderTarget2D RenderTarget { get; }

    public LayoutRect Bounds { get; }

    public int RenderVersionStamp { get; }

    public int LayoutVersionStamp { get; }

    public int RenderStateSignature { get; }

    public long ByteSize { get; }
}
