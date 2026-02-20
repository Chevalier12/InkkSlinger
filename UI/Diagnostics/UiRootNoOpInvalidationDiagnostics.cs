using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double NoOpInvalidationIdleFlushMs = 900d;
    private const int NoOpInvalidationMinSamples = 8;
    private static readonly bool IsNoOpInvalidationDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_NOOP_INVALIDATION_LOGS"), "1", StringComparison.Ordinal);

    private int _noOpInvalidationMeasureCount;
    private int _noOpInvalidationArrangeCount;
    private int _noOpInvalidationRenderCount;
    private int _noOpInvalidationRenderNoBoundsCount;
    private int _noOpInvalidationRenderDetachedSourceCount;
    private long _noOpInvalidationLastActivityTimestamp;

    private void ObserveInvalidationDiagnostics(UiInvalidationType invalidationType, UIElement? source)
    {
        if (!IsNoOpInvalidationDiagnosticsEnabled)
        {
            return;
        }

        switch (invalidationType)
        {
            case UiInvalidationType.Measure:
                _noOpInvalidationMeasureCount++;
                break;
            case UiInvalidationType.Arrange:
                _noOpInvalidationArrangeCount++;
                break;
            case UiInvalidationType.Render:
                _noOpInvalidationRenderCount++;
                if (source != null && !IsPartOfVisualTree(source))
                {
                    _noOpInvalidationRenderDetachedSourceCount++;
                }

                break;
        }

        _noOpInvalidationLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveNoOpRenderInvalidationNoBounds()
    {
        if (!IsNoOpInvalidationDiagnosticsEnabled)
        {
            return;
        }

        _noOpInvalidationRenderNoBoundsCount++;
        _noOpInvalidationLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveNoOpInvalidationAfterUpdate()
    {
        if (!IsNoOpInvalidationDiagnosticsEnabled)
        {
            return;
        }

        TryFlushNoOpInvalidationDiagnostics();
    }

    private void ObserveNoOpInvalidationAfterDraw()
    {
        if (!IsNoOpInvalidationDiagnosticsEnabled)
        {
            return;
        }

        TryFlushNoOpInvalidationDiagnostics();
    }

    private void TryFlushNoOpInvalidationDiagnostics()
    {
        var totalInvalidations = _noOpInvalidationMeasureCount + _noOpInvalidationArrangeCount + _noOpInvalidationRenderCount;
        if (totalInvalidations < NoOpInvalidationMinSamples || _noOpInvalidationLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_noOpInvalidationLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < NoOpInvalidationIdleFlushMs)
        {
            return;
        }

        var renderNoOpCandidates = _noOpInvalidationRenderNoBoundsCount + _noOpInvalidationRenderDetachedSourceCount;
        var renderNoOpPct = _noOpInvalidationRenderCount > 0
            ? (renderNoOpCandidates * 100d) / _noOpInvalidationRenderCount
            : 0d;

        var summary =
            $"[NoOpInvalidation] total={totalInvalidations} measure={_noOpInvalidationMeasureCount} arrange={_noOpInvalidationArrangeCount} render={_noOpInvalidationRenderCount} " +
            $"renderNoOpCandidates={renderNoOpCandidates} renderNoOpRate={renderNoOpPct:0.0}% (noBounds={_noOpInvalidationRenderNoBoundsCount},detached={_noOpInvalidationRenderDetachedSourceCount})";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetNoOpInvalidationDiagnostics();
    }

    private void ResetNoOpInvalidationDiagnostics()
    {
        _noOpInvalidationMeasureCount = 0;
        _noOpInvalidationArrangeCount = 0;
        _noOpInvalidationRenderCount = 0;
        _noOpInvalidationRenderNoBoundsCount = 0;
        _noOpInvalidationRenderDetachedSourceCount = 0;
        _noOpInvalidationLastActivityTimestamp = 0L;
    }
}