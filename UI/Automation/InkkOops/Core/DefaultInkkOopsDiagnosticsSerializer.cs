using System;
using System.Text;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsSerializer : IInkkOopsDiagnosticsSerializer
{
    public string SerializeVisualTree(InkkOopsVisualTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();
        for (var i = 0; i < snapshot.Nodes.Count; i++)
        {
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
}
