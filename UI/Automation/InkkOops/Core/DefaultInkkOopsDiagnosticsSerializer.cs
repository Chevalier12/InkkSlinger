using System;
using System.Collections.Generic;
using System.Text;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsSerializer : IInkkOopsDiagnosticsSerializer
{
    public string SerializeVisualTree(InkkOopsVisualTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var includedNodeIndexes = GetIncludedNodeIndexes(snapshot);
        var builder = new StringBuilder();
        for (var i = 0; i < snapshot.Nodes.Count; i++)
        {
            if (!includedNodeIndexes[i])
            {
                continue;
            }

            var node = snapshot.Nodes[i];
            builder.Append(new string(' ', node.Depth * 2));
            builder.Append(node.DisplayName);
            for (var factIndex = 0; factIndex < node.Facts.Count; factIndex++)
            {
                var fact = node.Facts[factIndex];
                builder.Append(' ');
                builder.Append(fact.Key);
                builder.Append('=');
                builder.Append(fact.Value);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static bool[] GetIncludedNodeIndexes(InkkOopsVisualTreeSnapshot snapshot)
    {
        var include = new bool[snapshot.Nodes.Count];
        if (!snapshot.IsFiltered || snapshot.NodeRetention == InkkOopsDiagnosticsNodeRetention.All)
        {
            Array.Fill(include, true);
            return include;
        }

        if (snapshot.Nodes.Count == 0)
        {
            return include;
        }

        var ancestorIndexes = new List<int>();
        var hasMatchedNode = false;
        for (var i = 0; i < snapshot.Nodes.Count; i++)
        {
            var node = snapshot.Nodes[i];
            while (ancestorIndexes.Count > node.Depth)
            {
                ancestorIndexes.RemoveAt(ancestorIndexes.Count - 1);
            }

            if (node.MatchedFilter)
            {
                hasMatchedNode = true;
                include[i] = true;
                for (var ancestorIndex = 0; ancestorIndex < ancestorIndexes.Count; ancestorIndex++)
                {
                    include[ancestorIndexes[ancestorIndex]] = true;
                }
            }

            if (ancestorIndexes.Count == node.Depth)
            {
                ancestorIndexes.Add(i);
            }
            else
            {
                ancestorIndexes[node.Depth] = i;
            }
        }

        if (!hasMatchedNode)
        {
            include[0] = true;
        }

        return include;
    }
}
