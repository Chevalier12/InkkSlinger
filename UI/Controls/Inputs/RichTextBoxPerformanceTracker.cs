using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class RichTextBoxPerformanceTracker
{
    private const int PerfSampleCap = 256;
    private readonly List<double> _layoutBuildSamplesMs = [];
    private int _layoutCacheHitCount;
    private int _layoutCacheMissCount;
    private int _layoutBuildSampleCount;
    private double _layoutBuildTotalMs;
    private double _layoutBuildMaxMs;
    private int _renderSampleCount;
    private double _renderTotalMs;
    private double _renderLastMs;
    private double _renderMaxMs;
    private int _selectionGeometrySampleCount;
    private double _selectionGeometryTotalMs;
    private double _selectionGeometryLastMs;
    private double _selectionGeometryMaxMs;
    private int _clipboardSerializeSampleCount;
    private double _clipboardSerializeTotalMs;
    private double _clipboardSerializeLastMs;
    private double _clipboardSerializeMaxMs;
    private int _clipboardDeserializeSampleCount;
    private double _clipboardDeserializeTotalMs;
    private double _clipboardDeserializeLastMs;
    private double _clipboardDeserializeMaxMs;
    private int _editSampleCount;
    private double _editTotalMs;
    private double _editLastMs;
    private double _editMaxMs;

    public RichTextBoxPerformanceSnapshot GetSnapshot(DocumentUndoManager undoManager)
    {
        return new RichTextBoxPerformanceSnapshot(
            _layoutCacheHitCount,
            _layoutCacheMissCount,
            _layoutBuildSampleCount,
            Average(_layoutBuildTotalMs, _layoutBuildSampleCount),
            Percentile(_layoutBuildSamplesMs, 0.95),
            Percentile(_layoutBuildSamplesMs, 0.99),
            _layoutBuildMaxMs,
            _renderSampleCount,
            _renderLastMs,
            Average(_renderTotalMs, _renderSampleCount),
            _renderMaxMs,
            _selectionGeometrySampleCount,
            _selectionGeometryLastMs,
            Average(_selectionGeometryTotalMs, _selectionGeometrySampleCount),
            _selectionGeometryMaxMs,
            _clipboardSerializeSampleCount,
            _clipboardSerializeLastMs,
            Average(_clipboardSerializeTotalMs, _clipboardSerializeSampleCount),
            _clipboardSerializeMaxMs,
            _clipboardDeserializeSampleCount,
            _clipboardDeserializeLastMs,
            Average(_clipboardDeserializeTotalMs, _clipboardDeserializeSampleCount),
            _clipboardDeserializeMaxMs,
            _editSampleCount,
            _editLastMs,
            Average(_editTotalMs, _editSampleCount),
            _editMaxMs,
            undoManager.UndoDepth,
            undoManager.RedoDepth,
            undoManager.UndoOperationCount,
            undoManager.RedoOperationCount);
    }

    public void Reset()
    {
        _layoutCacheHitCount = 0;
        _layoutCacheMissCount = 0;
        _layoutBuildSampleCount = 0;
        _layoutBuildTotalMs = 0d;
        _layoutBuildMaxMs = 0d;
        _layoutBuildSamplesMs.Clear();
        _renderSampleCount = 0;
        _renderTotalMs = 0d;
        _renderLastMs = 0d;
        _renderMaxMs = 0d;
        _selectionGeometrySampleCount = 0;
        _selectionGeometryTotalMs = 0d;
        _selectionGeometryLastMs = 0d;
        _selectionGeometryMaxMs = 0d;
        _clipboardSerializeSampleCount = 0;
        _clipboardSerializeTotalMs = 0d;
        _clipboardSerializeLastMs = 0d;
        _clipboardSerializeMaxMs = 0d;
        _clipboardDeserializeSampleCount = 0;
        _clipboardDeserializeTotalMs = 0d;
        _clipboardDeserializeLastMs = 0d;
        _clipboardDeserializeMaxMs = 0d;
        _editSampleCount = 0;
        _editTotalMs = 0d;
        _editLastMs = 0d;
        _editMaxMs = 0d;
    }

    public void RecordLayoutCacheHit() => _layoutCacheHitCount++;

    public void RecordLayoutCacheMiss() => _layoutCacheMissCount++;

    public void RecordLayoutBuild(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _layoutBuildSampleCount++;
        _layoutBuildTotalMs += bounded;
        _layoutBuildMaxMs = Math.Max(_layoutBuildMaxMs, bounded);
        AppendSample(_layoutBuildSamplesMs, bounded);
    }

    public void RecordRender(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _renderSampleCount++;
        _renderTotalMs += bounded;
        _renderLastMs = bounded;
        _renderMaxMs = Math.Max(_renderMaxMs, bounded);
    }

    public void RecordSelectionGeometry(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _selectionGeometrySampleCount++;
        _selectionGeometryTotalMs += bounded;
        _selectionGeometryLastMs = bounded;
        _selectionGeometryMaxMs = Math.Max(_selectionGeometryMaxMs, bounded);
    }

    public void RecordClipboardSerialize(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _clipboardSerializeSampleCount++;
        _clipboardSerializeTotalMs += bounded;
        _clipboardSerializeLastMs = bounded;
        _clipboardSerializeMaxMs = Math.Max(_clipboardSerializeMaxMs, bounded);
    }

    public void RecordClipboardDeserialize(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _clipboardDeserializeSampleCount++;
        _clipboardDeserializeTotalMs += bounded;
        _clipboardDeserializeLastMs = bounded;
        _clipboardDeserializeMaxMs = Math.Max(_clipboardDeserializeMaxMs, bounded);
    }

    public void RecordEdit(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _editSampleCount++;
        _editTotalMs += bounded;
        _editLastMs = bounded;
        _editMaxMs = Math.Max(_editMaxMs, bounded);
    }

    private static void AppendSample(List<double> samples, double value)
    {
        if (samples.Count >= PerfSampleCap)
        {
            samples.RemoveAt(0);
        }

        samples.Add(value);
    }

    private static double Average(double total, int count)
    {
        if (count <= 0)
        {
            return 0d;
        }

        return total / count;
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0d;
        }

        var ordered = samples.ToArray();
        Array.Sort(ordered);
        var rawIndex = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(rawIndex);
        var upper = (int)Math.Ceiling(rawIndex);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = rawIndex - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }
}
