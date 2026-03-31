using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public sealed class InkkOopsVisualTreeDiagnostics
{
    private readonly IReadOnlyList<IInkkOopsDiagnosticsContributor> _contributors;

    public InkkOopsVisualTreeDiagnostics(IEnumerable<IInkkOopsDiagnosticsContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        _contributors = contributors
            .OrderBy(static contributor => contributor.Order)
            .ToArray();
    }

    public InkkOopsVisualTreeSnapshot Capture(UIElement? root, InkkOopsDiagnosticsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (root == null)
        {
            return new InkkOopsVisualTreeSnapshot
            {
                Nodes = Array.Empty<InkkOopsVisualTreeNodeSnapshot>(),
                IsFiltered = context.Filter.IsActive,
                NodeRetention = context.Filter.NodeRetention
            };
        }

        var nodes = new List<InkkOopsVisualTreeNodeSnapshot>();
        Append(nodes, root, depth: 0, context);
        return new InkkOopsVisualTreeSnapshot
        {
            Nodes = nodes,
            IsFiltered = context.Filter.IsActive,
            NodeRetention = context.Filter.NodeRetention
        };
    }

    private void Append(List<InkkOopsVisualTreeNodeSnapshot> nodes, UIElement element, int depth, InkkOopsDiagnosticsContext context)
    {
        var displayName = InkkOopsTargetResolver.DescribeElement(element);
        var builder = new InkkOopsElementDiagnosticsBuilder(displayName, element.GetType().Name, context.Filter);
        for (var i = 0; i < _contributors.Count; i++)
        {
            _contributors[i].Contribute(context, element, builder);
        }

        nodes.Add(new InkkOopsVisualTreeNodeSnapshot
        {
            Depth = depth,
            DisplayName = displayName,
            Facts = builder.Facts.ToArray(),
            MatchedFilter = builder.MatchedFilter
        });

        foreach (var child in element.GetVisualChildren())
        {
            Append(nodes, child, depth + 1, context);
        }
    }
}
