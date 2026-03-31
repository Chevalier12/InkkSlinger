using System;
using System.Collections.Generic;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public sealed class AutomationManager
{
    private readonly Dictionary<UIElement, AutomationPeer> _peerByElement = new();
    private readonly List<QueuedAutomationEvent> _queuedEvents = new();
    private readonly Dictionary<AutomationEventDedupKey, int> _queuedEventIndexByDedupKey = new();
    private readonly List<AutomationEventRecord> _eventLog = new();
    private readonly bool _enableLogs = string.Equals(
        Environment.GetEnvironmentVariable("INKKSLINGER_AUTOMATION_LOGS"),
        "1",
        StringComparison.Ordinal);

    private UIElement _visualRoot;
    private int _nextPeerRuntimeId = 1;
    private int _treeRebuildCount;
    private int _emittedEventCountLastFrame;
    private int _coalescedEventDiscardCountLastFrame;

    public AutomationManager(UIElement visualRoot)
    {
        _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
        RebuildTree();
    }

    public event EventHandler<AutomationEventArgs>? AutomationEventRaised;

    public void BeginFrame()
    {
        _emittedEventCountLastFrame = 0;
        _coalescedEventDiscardCountLastFrame = 0;
    }

    public void EndFrameAndFlush()
    {
        for (var i = 0; i < _queuedEvents.Count; i++)
        {
            var next = _queuedEvents[i];
            EmitEvent(next);
        }

        _queuedEvents.Clear();
        _queuedEventIndexByDedupKey.Clear();
    }

    public void Shutdown()
    {
        foreach (var pair in _peerByElement)
        {
            pair.Key.DependencyPropertyChanged -= OnElementDependencyPropertyChanged;
        }

        _peerByElement.Clear();
        _queuedEvents.Clear();
        _queuedEventIndexByDedupKey.Clear();
        _eventLog.Clear();
    }

    public AutomationPeer? GetPeer(UIElement element)
    {
        if (element == null)
        {
            return null;
        }

        if (_peerByElement.TryGetValue(element, out var existing))
        {
            if (!ReferenceEquals(element, _visualRoot) && !IsConnectedToRoot(element))
            {
                element.DependencyPropertyChanged -= OnElementDependencyPropertyChanged;
                _peerByElement.Remove(element);
                return null;
            }

            return existing;
        }

        if (!IsConnectedToRoot(element))
        {
            return null;
        }

        var created = EnsurePeer(element);
        RebuildParentChildLinks();
        return created;
    }

    public IReadOnlyList<AutomationPeer> GetTreeSnapshot()
    {
        var result = new List<AutomationPeer>();
        if (_peerByElement.TryGetValue(_visualRoot, out var rootPeer))
        {
            CollectPeerTree(rootPeer, result);
        }

        return result;
    }

    internal IReadOnlyList<AutomationEventRecord> GetAndClearEventLogForTests()
    {
        var snapshot = new List<AutomationEventRecord>(_eventLog);
        _eventLog.Clear();
        return snapshot;
    }

    internal AutomationMetricsSnapshot GetMetricsSnapshot()
    {
        return new AutomationMetricsSnapshot(
            _peerByElement.Count,
            _treeRebuildCount,
            _emittedEventCountLastFrame,
            _coalescedEventDiscardCountLastFrame);
    }

    internal int GetQueuedEventCountForDiagnostics()
    {
        return _queuedEvents.Count;
    }

    internal void NotifyVisualStructureChanged(UIElement element, UIElement? oldParent, UIElement? newParent)
    {
        _ = oldParent;
        _ = newParent;

        if (ReferenceEquals(element, _visualRoot) || _peerByElement.ContainsKey(element) || IsConnectedToRoot(element))
        {
            _peerByElement.TryGetValue(element, out var changedPeer);
            RebuildTree();
            changedPeer ??= GetPeer(element);
            if (changedPeer != null)
            {
                QueueEvent(AutomationEventType.StructureChanged, changedPeer);
            }
        }
    }

    internal void NotifyFocusChanged(UIElement? oldElement, UIElement? newElement)
    {
        if (newElement == null)
        {
            return;
        }

        var peer = GetPeer(newElement);
        if (peer == null)
        {
            return;
        }

        var oldPeer = oldElement != null ? GetPeer(oldElement) : null;
        var dedupKey = new AutomationEventDedupKey(AutomationEventType.FocusChanged, peer.RuntimeId, null);
        QueueEvent(new QueuedAutomationEvent(
            dedupKey,
            AutomationEventType.FocusChanged,
            peer,
            PropertyName: null,
            OldValue: null,
            NewValue: null,
            oldPeer,
            NewPeer: peer));
    }

    internal void NotifyInvoke(UIElement element)
    {
        var peer = GetPeer(element);
        if (peer == null)
        {
            return;
        }

        QueueEvent(AutomationEventType.Invoke, peer);
    }

    private void RebuildTree()
    {
        _treeRebuildCount++;

        var visited = new HashSet<UIElement>();
        TraverseVisualTree(_visualRoot, visited);

        var toRemove = new List<UIElement>();
        foreach (var pair in _peerByElement)
        {
            if (!visited.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
            }
        }

        for (var i = 0; i < toRemove.Count; i++)
        {
            var element = toRemove[i];
            element.DependencyPropertyChanged -= OnElementDependencyPropertyChanged;
            _peerByElement.Remove(element);
        }

        RebuildParentChildLinks();
    }

    private void TraverseVisualTree(UIElement element, HashSet<UIElement> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        _ = EnsurePeer(element);

        foreach (var child in element.GetVisualChildren())
        {
            TraverseVisualTree(child, visited);
        }
    }

    private AutomationPeer EnsurePeer(UIElement element)
    {
        if (_peerByElement.TryGetValue(element, out var existing))
        {
            return existing;
        }

        var created = AutomationPeerFactory.CreatePeer(this, element);
        created.RuntimeId = _nextPeerRuntimeId++;
        _peerByElement[element] = created;
        element.DependencyPropertyChanged += OnElementDependencyPropertyChanged;
        return created;
    }

    private void RebuildParentChildLinks()
    {
        foreach (var pair in _peerByElement)
        {
            pair.Value.Parent = null;
            pair.Value.SetChildren(Array.Empty<AutomationPeer>());
        }

        foreach (var pair in _peerByElement)
        {
            var element = pair.Key;
            var peer = pair.Value;
            var children = new List<AutomationPeer>();
            foreach (var child in element.GetVisualChildren())
            {
                if (_peerByElement.TryGetValue(child, out var childPeer))
                {
                    childPeer.Parent = peer;
                    children.Add(childPeer);
                }
            }

            peer.SetChildren(children);
        }
    }

    private void OnElementDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var peer = GetPeer(element);
        if (peer == null)
        {
            return;
        }

        var propertyName = args.Property.Name;
        var oldValue = args.OldValue;
        var newValue = args.NewValue;
        if (propertyName == "Password")
        {
            oldValue = null;
            newValue = null;
        }

        QueueEvent(AutomationEventType.PropertyChanged, peer, propertyName, oldValue, newValue);

        switch (propertyName)
        {
            case "Value":
            case "Text":
            case "Password":
                QueueEvent(AutomationEventType.ValueChanged, peer, propertyName, oldValue, newValue);
                break;
            case "SelectedIndex":
            case "IsSelected":
                QueueEvent(AutomationEventType.SelectionChanged, peer, propertyName, oldValue, newValue);
                break;
            case "IsExpanded":
            case "IsSubmenuOpen":
            case "IsDropDownOpen":
                QueueEvent(AutomationEventType.ExpandCollapseStateChanged, peer, propertyName, oldValue, newValue);
                break;
            case "HorizontalOffset":
            case "VerticalOffset":
                QueueEvent(AutomationEventType.ScrollChanged, peer, propertyName, oldValue, newValue);
                break;
        }
    }

    private bool IsConnectedToRoot(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, _visualRoot))
            {
                return true;
            }
        }

        return false;
    }

    private void QueueEvent(
        AutomationEventType eventType,
        AutomationPeer peer,
        string? propertyName = null,
        object? oldValue = null,
        object? newValue = null,
        AutomationPeer? oldPeer = null,
        AutomationPeer? newPeer = null)
    {
        var dedupKey = new AutomationEventDedupKey(eventType, peer.RuntimeId, propertyName);
        QueueEvent(new QueuedAutomationEvent(
            dedupKey,
            eventType,
            peer,
            propertyName,
            oldValue,
            newValue,
            oldPeer,
            newPeer));
    }

    private void QueueEvent(QueuedAutomationEvent queuedEvent)
    {
        if (_queuedEventIndexByDedupKey.TryGetValue(queuedEvent.DedupKey, out var existingIndex))
        {
            var existingEvent = _queuedEvents[existingIndex];
            _queuedEvents[existingIndex] = MergeQueuedEvents(existingEvent, queuedEvent);
            _coalescedEventDiscardCountLastFrame++;
            return;
        }

        _queuedEventIndexByDedupKey[queuedEvent.DedupKey] = _queuedEvents.Count;
        _queuedEvents.Add(queuedEvent);
    }

    private static QueuedAutomationEvent MergeQueuedEvents(QueuedAutomationEvent existingEvent, QueuedAutomationEvent latestEvent)
    {
        return latestEvent with
        {
            OldValue = existingEvent.OldValue,
            OldPeer = existingEvent.OldPeer
        };
    }

    private void EmitEvent(QueuedAutomationEvent queued)
    {
        var args = new AutomationEventArgs(
            queued.EventType,
            queued.Peer,
            queued.PropertyName,
            queued.OldValue,
            queued.NewValue,
            queued.OldPeer,
            queued.NewPeer);

        _emittedEventCountLastFrame++;

        _eventLog.Add(new AutomationEventRecord(
            queued.EventType,
            queued.Peer.RuntimeId,
            queued.PropertyName,
            queued.OldValue,
            queued.NewValue,
            queued.OldPeer?.RuntimeId,
            queued.NewPeer?.RuntimeId));

        AutomationEventRaised?.Invoke(this, args);

        if (_enableLogs)
        {
            Console.WriteLine($"[Automation] Event={queued.EventType} Peer={queued.Peer.Owner.GetType().Name}#{queued.Peer.RuntimeId} Property={queued.PropertyName}");
        }
    }

    private static void CollectPeerTree(AutomationPeer root, List<AutomationPeer> result)
    {
        result.Add(root);
        for (var i = 0; i < root.Children.Count; i++)
        {
            CollectPeerTree(root.Children[i], result);
        }
    }

    private readonly record struct AutomationEventDedupKey(
        AutomationEventType EventType,
        int PeerRuntimeId,
        string? PropertyName);

    private readonly record struct QueuedAutomationEvent(
        AutomationEventDedupKey DedupKey,
        AutomationEventType EventType,
        AutomationPeer Peer,
        string? PropertyName,
        object? OldValue,
        object? NewValue,
        AutomationPeer? OldPeer,
        AutomationPeer? NewPeer);
}
