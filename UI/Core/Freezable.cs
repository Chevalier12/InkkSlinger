using System;
using System.Diagnostics;

namespace InkkSlinger;

public abstract class Freezable
{
    private static int _batchBeginCount;
    private static int _batchEndCount;
    private static int _pendingChangedDuringBatchCount;
    private static int _onChangedCallCount;
    private static int _endBatchFlushCount;
    private static long _onChangedElapsedTicks;
    private static long _endBatchElapsedTicks;
    private static string _hottestOnChangedType = "none";
    private static long _hottestOnChangedTicks;
    private static string _hottestEndBatchType = "none";
    private static long _hottestEndBatchTicks;
    private bool _isFrozen;
    private int _batchUpdateDepth;
    private bool _hasPendingChangedDuringBatch;

    internal event Action? Changed;

    public bool IsFrozen => _isFrozen;

    public bool CanFreeze => FreezeCore(isChecking: true);

    public void Freeze()
    {
        if (_isFrozen)
        {
            return;
        }

        if (!FreezeCore(isChecking: true))
        {
            throw new InvalidOperationException($"{GetType().Name} cannot be frozen.");
        }

        _ = FreezeCore(isChecking: false);
        _isFrozen = true;
    }

    public Freezable Clone()
    {
        var clone = CreateInstanceCore();
        clone.CloneCore(this);
        clone._isFrozen = false;
        return clone;
    }

    public Freezable CloneCurrentValue()
    {
        var clone = CreateInstanceCore();
        clone.CloneCurrentValueCore(this);
        clone._isFrozen = false;
        return clone;
    }

    protected void ReadPreamble()
    {
    }

    protected void WritePreamble()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException($"Cannot modify frozen {GetType().Name}.");
        }
    }

    protected void WritePostscript()
    {
        if (_batchUpdateDepth > 0)
        {
            _hasPendingChangedDuringBatch = true;
            _pendingChangedDuringBatchCount++;
            return;
        }

        OnChanged();
    }

    protected virtual void OnChanged()
    {
        var startTicks = Stopwatch.GetTimestamp();
        try
        {
            Changed?.Invoke();
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _onChangedCallCount++;
            _onChangedElapsedTicks += elapsedTicks;
            if (elapsedTicks > _hottestOnChangedTicks)
            {
                _hottestOnChangedTicks = elapsedTicks;
                _hottestOnChangedType = GetType().Name;
            }
        }
    }

    protected abstract Freezable CreateInstanceCore();

    protected virtual void CloneCore(Freezable source)
    {
    }

    protected virtual void CloneCurrentValueCore(Freezable source)
    {
        CloneCore(source);
    }

    protected virtual bool FreezeCore(bool isChecking)
    {
        return true;
    }

    protected static bool FreezeValue(Freezable? value, bool isChecking)
    {
        if (value == null || value.IsFrozen)
        {
            return true;
        }

        if (!value.FreezeCore(isChecking: true))
        {
            return false;
        }

        if (!isChecking)
        {
            value.Freeze();
        }

        return true;
    }

    internal void BeginBatchUpdate()
    {
        _batchBeginCount++;
        _batchUpdateDepth++;
    }

    internal void EndBatchUpdate()
    {
        var startTicks = Stopwatch.GetTimestamp();
        if (_batchUpdateDepth <= 0)
        {
            return;
        }

        _batchEndCount++;
        _batchUpdateDepth--;
        if (_batchUpdateDepth == 0 && _hasPendingChangedDuringBatch)
        {
            _hasPendingChangedDuringBatch = false;
            _endBatchFlushCount++;
            OnChanged();
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _endBatchElapsedTicks += elapsedTicks;
        if (elapsedTicks > _hottestEndBatchTicks)
        {
            _hottestEndBatchTicks = elapsedTicks;
            _hottestEndBatchType = GetType().Name;
        }
    }

    internal static FreezableTelemetrySnapshot GetTelemetrySnapshotForTests()
    {
        return new FreezableTelemetrySnapshot(
            _batchBeginCount,
            _batchEndCount,
            _pendingChangedDuringBatchCount,
            _onChangedCallCount,
            _endBatchFlushCount,
            TicksToMilliseconds(_onChangedElapsedTicks),
            TicksToMilliseconds(_endBatchElapsedTicks),
            _hottestOnChangedType,
            TicksToMilliseconds(_hottestOnChangedTicks),
            _hottestEndBatchType,
            TicksToMilliseconds(_hottestEndBatchTicks));
    }

    internal static void ResetTelemetryForTests()
    {
        _batchBeginCount = 0;
        _batchEndCount = 0;
        _pendingChangedDuringBatchCount = 0;
        _onChangedCallCount = 0;
        _endBatchFlushCount = 0;
        _onChangedElapsedTicks = 0L;
        _endBatchElapsedTicks = 0L;
        _hottestOnChangedType = "none";
        _hottestOnChangedTicks = 0L;
        _hottestEndBatchType = "none";
        _hottestEndBatchTicks = 0L;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}

internal readonly record struct FreezableTelemetrySnapshot(
    int BatchBeginCount,
    int BatchEndCount,
    int PendingChangedDuringBatchCount,
    int OnChangedCallCount,
    int EndBatchFlushCount,
    double OnChangedMilliseconds,
    double EndBatchMilliseconds,
    string HottestOnChangedType,
    double HottestOnChangedMilliseconds,
    string HottestEndBatchType,
    double HottestEndBatchMilliseconds);
