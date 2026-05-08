using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class VisualRecordStore
{
    private readonly Dictionary<UIElement, RetainedDrawRecord> _records = new(ReferenceEqualityComparer.Instance);

    public int RecordCount => _records.Count;

    public int RebuildCount { get; private set; }

    public int ReuseCount { get; private set; }

    public int LastRecordedCommandCount { get; private set; }

    public string LastRecordedVisualType { get; private set; } = "none";

    public string LastRecordedVisualName { get; private set; } = string.Empty;

    public VisualCommandList RecordOrReuse(UIElement visual)
    {
        var contentVersion = visual.RenderVersionStamp;
        if (_records.TryGetValue(visual, out var existing) &&
            existing.ContentVersion == contentVersion)
        {
            ReuseCount++;
            CaptureLastRecordedVisual(visual, existing.Commands.Count);
            return existing.Commands;
        }

        var commands = new VisualCommandList();
        visual.RecordVisual(new VisualRecordingContext(commands));
        _records[visual] = new RetainedDrawRecord(visual, contentVersion, commands);
        RebuildCount++;
        CaptureLastRecordedVisual(visual, commands.Count);
        return commands;
    }

    public bool TryGetRecord(UIElement visual, out VisualCommandList commands)
    {
        if (_records.TryGetValue(visual, out var record))
        {
            commands = record.Commands;
            return true;
        }

        commands = null!;
        return false;
    }

    public void RetainOnly(IReadOnlyList<UIElement> visuals)
    {
        if (_records.Count == 0)
        {
            return;
        }

        var retained = new HashSet<UIElement>(visuals, ReferenceEqualityComparer.Instance);
        var stale = new List<UIElement>();
        foreach (var visual in _records.Keys)
        {
            if (!retained.Contains(visual))
            {
                stale.Add(visual);
            }
        }

        for (var i = 0; i < stale.Count; i++)
        {
            _records.Remove(stale[i]);
        }
    }

    public void ResetTelemetry()
    {
        RebuildCount = 0;
        ReuseCount = 0;
        LastRecordedCommandCount = 0;
        LastRecordedVisualType = "none";
        LastRecordedVisualName = string.Empty;
    }

    private void CaptureLastRecordedVisual(UIElement visual, int commandCount)
    {
        LastRecordedCommandCount = commandCount;
        LastRecordedVisualType = visual.GetType().Name;
        LastRecordedVisualName = visual is FrameworkElement { Name.Length: > 0 } frameworkElement
            ? frameworkElement.Name
            : string.Empty;
    }

}
