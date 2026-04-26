using System;
using System.Diagnostics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void OnDocumentPropertyChanged(FlowDocument? oldDocument, FlowDocument? newDocument)
    {
        SetHoveredHyperlink(null);
        if (oldDocument != null)
        {
            oldDocument.Changed -= OnDocumentChanged;
        }

        var active = newDocument ?? CreateDefaultDocument();
        active.Changed += OnDocumentChanged;
        var currentDocumentRichness = CaptureDocumentRichness(Document);
        PerformDocumentMaintenance(currentDocumentRichness);
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RaiseTextChangedEvent();
        InvalidateAfterDocumentChange();
        _lastDocumentRichness = currentDocumentRichness;
        RecordOperation("DocumentPropertyChanged", $"blocks={Document.Blocks.Count}");
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_documentChangeBatchDepth > 0)
        {
            _hasPendingDocumentChangedEvent = true;
            _hasPendingTextChangedEvent = true;
            _hasPendingDocumentMaintenanceWork = true;
            return;
        }

        var currentDocumentRichness = CaptureDocumentRichness(Document);
        PerformDocumentMaintenance(currentDocumentRichness);
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RaiseTextChangedEvent();
        InvalidateAfterDocumentChange();
        _lastDocumentRichness = currentDocumentRichness;
    }

    private void RaiseTextChangedEvent()
    {
        RaiseRoutedEventInternal(TextChangedEvent, new RoutedSimpleEventArgs(TextChangedEvent));
    }

    private void ExecuteDocumentChangeBatch(Action action)
    {
        BeginDocumentChangeBatch();
        try
        {
            action();
        }
        finally
        {
            EndDocumentChangeBatch();
        }
    }

    private T ExecuteDocumentChangeBatch<T>(Func<T> action)
    {
        BeginDocumentChangeBatch();
        try
        {
            return action();
        }
        finally
        {
            EndDocumentChangeBatch();
        }
    }

    private void ExecuteTextMutationBatch(Action action)
    {
        var previous = _suppressMeasureInvalidationForDocumentBatch;
        _suppressMeasureInvalidationForDocumentBatch = true;
        try
        {
            ExecuteDocumentChangeBatch(action);
        }
        finally
        {
            _suppressMeasureInvalidationForDocumentBatch = previous;
        }
    }

    private void ExecuteTextMutationBatch(Action action, bool deferEventFlush)
    {
        var previous = _deferDocumentChangeBatchFlush;
        _deferDocumentChangeBatchFlush = deferEventFlush;
        try
        {
            ExecuteTextMutationBatch(action);
        }
        finally
        {
            _deferDocumentChangeBatchFlush = previous;
        }
    }

    private T ExecuteTextMutationBatch<T>(Func<T> action)
    {
        var previous = _suppressMeasureInvalidationForDocumentBatch;
        _suppressMeasureInvalidationForDocumentBatch = true;
        try
        {
            return ExecuteDocumentChangeBatch(action);
        }
        finally
        {
            _suppressMeasureInvalidationForDocumentBatch = previous;
        }
    }

    private void BeginDocumentChangeBatch()
    {
        _documentChangeBatchDepth++;
    }

    private void EndDocumentChangeBatch()
    {
        if (_documentChangeBatchDepth <= 0)
        {
            return;
        }

        _documentChangeBatchDepth--;
        if (_documentChangeBatchDepth == 0)
        {
            if (_deferDocumentChangeBatchFlush)
            {
                ScheduleDeferredDocumentChangeFlush();
                return;
            }

            FlushPendingDocumentChangeEvents();
        }
    }

    private void ScheduleDeferredDocumentChangeFlush()
    {
        if (!_hasPendingDocumentChangedEvent && !_hasPendingTextChangedEvent)
        {
            return;
        }

        var flushVersion = ++_deferredDocumentChangeFlushVersion;
        Dispatcher.EnqueueDeferred(
            () =>
            {
                if (flushVersion != _deferredDocumentChangeFlushVersion)
                {
                    return;
                }

                FlushPendingDocumentChangeEvents();
            });
    }

    private void FlushPendingDocumentChangeEvents()
    {
        if (!_hasPendingDocumentChangedEvent && !_hasPendingTextChangedEvent)
        {
            return;
        }

        var flushStart = Stopwatch.GetTimestamp();
        var raiseTextChanged = _hasPendingTextChangedEvent;
        var currentDocumentRichness = _lastDocumentRichness;
        var maintenanceMs = 0d;
        var documentChangedEventMs = 0d;
        var textChangedEventMs = 0d;
        var invalidateAfterDocumentChangeMs = 0d;
        _hasPendingDocumentChangedEvent = false;
        _hasPendingTextChangedEvent = false;
        if (_hasPendingDocumentMaintenanceWork)
        {
            _hasPendingDocumentMaintenanceWork = false;
            var maintenanceStart = Stopwatch.GetTimestamp();
            currentDocumentRichness = CaptureDocumentRichness(Document);
            PerformDocumentMaintenance(currentDocumentRichness);
            maintenanceMs = Stopwatch.GetElapsedTime(maintenanceStart).TotalMilliseconds;
        }

        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        var documentChangedEventStart = Stopwatch.GetTimestamp();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        documentChangedEventMs = Stopwatch.GetElapsedTime(documentChangedEventStart).TotalMilliseconds;
        if (raiseTextChanged)
        {
            var textChangedEventStart = Stopwatch.GetTimestamp();
            RaiseTextChangedEvent();
            textChangedEventMs = Stopwatch.GetElapsedTime(textChangedEventStart).TotalMilliseconds;
        }

        var invalidateAfterDocumentChangeStart = Stopwatch.GetTimestamp();
        InvalidateAfterDocumentChange();
        invalidateAfterDocumentChangeMs = Stopwatch.GetElapsedTime(invalidateAfterDocumentChangeStart).TotalMilliseconds;
        _lastDocumentRichness = currentDocumentRichness;
        _perfTracker.RecordStructuredEnterFlushBreakdown(
            Stopwatch.GetElapsedTime(flushStart).TotalMilliseconds,
            maintenanceMs,
            documentChangedEventMs,
            textChangedEventMs,
            invalidateAfterDocumentChangeMs);
    }

    private void PerformDocumentMaintenance(DocumentRichnessSnapshot currentDocumentRichness)
    {
        if (_lastDocumentRichness.HostedChildCount > 0 ||
            currentDocumentRichness.HostedChildCount > 0 ||
            _documentHostedVisualChildren.Count > 0)
        {
            SyncHostedDocumentChildren();
        }

        if (_lastDocumentRichness.HyperlinkCount > 0 ||
            currentDocumentRichness.HyperlinkCount > 0 ||
            _appliedImplicitHyperlinkStyles.Count > 0 ||
            _hoveredHyperlink != null)
        {
            ApplyHyperlinkImplicitStyles();
        }
    }
}
