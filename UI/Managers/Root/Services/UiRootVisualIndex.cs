using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class UiRootVisualIndex
{
    private readonly Dictionary<UIElement, UiIndexedVisualNode> _nodesByVisual = new();
    private readonly List<UiIndexedVisualNode> _nodes = new();
    private readonly List<UIElement> _topLevelVisuals = new();
    private readonly List<UIElement> _overlayVisuals = new();
    private readonly List<Menu> _menus = new();
    private readonly List<ContextMenu> _contextMenus = new();
    private readonly List<UIElement> _wheelCapableVisuals = new();
    private readonly List<UiIndexedUpdateParticipant> _updateParticipants = new();
    private UIElement? _root;

    public bool IsDirty { get; private set; } = true;

    public int Version { get; private set; }

    public IReadOnlyList<UiIndexedVisualNode> Nodes => _nodes;

    public IReadOnlyList<UIElement> TopLevelVisuals => _topLevelVisuals;

    public IReadOnlyList<UIElement> OverlayVisuals => _overlayVisuals;

    public IReadOnlyList<Menu> Menus => _menus;

    public IReadOnlyList<ContextMenu> ContextMenus => _contextMenus;

    public IReadOnlyList<UIElement> WheelCapableVisuals => _wheelCapableVisuals;

    public IReadOnlyList<UiIndexedUpdateParticipant> UpdateParticipants => _updateParticipants;

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void EnsureCurrent(UIElement root)
    {
        if (!IsDirty && ReferenceEquals(_root, root))
        {
            return;
        }

        Rebuild(root);
    }

    public bool TryGetNode(UIElement visual, out UiIndexedVisualNode node)
    {
        return _nodesByVisual.TryGetValue(visual, out node);
    }

    private void Rebuild(UIElement root)
    {
        _root = root;
        _nodesByVisual.Clear();
        _nodes.Clear();
        _topLevelVisuals.Clear();
        _overlayVisuals.Clear();
        _menus.Clear();
        _contextMenus.Clear();
        _wheelCapableVisuals.Clear();
        _updateParticipants.Clear();

        _ = BuildSubtree(root, parent: null, topLevelRootChild: null, depth: 0, preorderIndex: 0);

        Version++;
        IsDirty = false;
    }

    private int BuildSubtree(
        UIElement visual,
        UIElement? parent,
        UIElement? topLevelRootChild,
        int depth,
        int preorderIndex)
    {
        var node = new UiIndexedVisualNode(
            visual,
            parent,
            topLevelRootChild,
            preorderIndex,
            preorderIndex + 1,
            depth);
        _nodesByVisual[visual] = node;
        _nodes.Add(node);
        if (depth == 1)
        {
            _topLevelVisuals.Add(visual);
            topLevelRootChild = visual;
        }

        Classify(node);

        var nextIndex = preorderIndex + 1;
        foreach (var child in visual.GetVisualChildren())
        {
            nextIndex = BuildSubtree(child, visual, topLevelRootChild, depth + 1, nextIndex);
        }

        var finalized = node with { SubtreeEndIndexExclusive = nextIndex };
        _nodesByVisual[visual] = finalized;
        _nodes[finalized.PreorderIndex] = finalized;
        return nextIndex;
    }

    private void Classify(UiIndexedVisualNode node)
    {
        switch (node.Visual)
        {
            case Popup:
                _overlayVisuals.Add(node.Visual);
                break;
            case ContextMenu contextMenu:
                _overlayVisuals.Add(node.Visual);
                _contextMenus.Add(contextMenu);
                break;
        }

        if (node.Visual is Menu menu)
        {
            _menus.Add(menu);
        }

        if (node.Visual is ITextInputControl or ScrollViewer)
        {
            _wheelCapableVisuals.Add(node.Visual);
        }

        if (node.Visual is IUiRootUpdateParticipant participant)
        {
            _updateParticipants.Add(new UiIndexedUpdateParticipant(node.Visual, participant));
        }
    }
}

internal readonly record struct UiIndexedVisualNode(
    UIElement Visual,
    UIElement? Parent,
    UIElement? TopLevelRootChild,
    int PreorderIndex,
    int SubtreeEndIndexExclusive,
    int Depth);

internal readonly record struct UiIndexedUpdateParticipant(
    UIElement Visual,
    IUiRootUpdateParticipant Participant);
