using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class RetainedCompositionGraph
{
    public static readonly RetainedCompositionGraph Empty = new(
        Array.Empty<RetainedCompositionNode>(),
        new Dictionary<UIElement, int>(ReferenceEqualityComparer.Instance));

    public RetainedCompositionGraph(
        IReadOnlyList<RetainedCompositionNode> nodes,
        IReadOnlyDictionary<UIElement, int> nodeIndices)
    {
        Nodes = nodes;
        NodeIndices = nodeIndices;
    }

    public IReadOnlyList<RetainedCompositionNode> Nodes { get; }

    public IReadOnlyDictionary<UIElement, int> NodeIndices { get; }

    public int NodeCount => Nodes.Count;

    public bool TryGetNode(UIElement visual, out RetainedCompositionNode node)
    {
        if (NodeIndices.TryGetValue(visual, out var nodeIndex) &&
            (uint)nodeIndex < (uint)Nodes.Count)
        {
            node = Nodes[nodeIndex];
            return true;
        }

        node = default;
        return false;
    }
}
