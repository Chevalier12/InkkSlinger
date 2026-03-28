using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public enum InkkOopsTargetResolutionSource
{
    None,
    XName,
    AutomationId,
    AutomationName
}

public sealed class InkkOopsTargetResolutionReport
{
    public InkkOopsTargetResolutionReport(
        InkkOopsTargetSelector selector,
        InkkOopsTargetResolutionStatus status,
        InkkOopsTargetResolutionSource source,
        UIElement? element,
        AutomationPeer? peer,
        IReadOnlyList<string> notes,
        IReadOnlyList<string> candidates)
    {
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        Status = status;
        Source = source;
        Element = element;
        Peer = peer;
        Notes = notes ?? Array.Empty<string>();
        Candidates = candidates ?? Array.Empty<string>();
    }

    public InkkOopsTargetSelector Selector { get; }

    public InkkOopsTargetResolutionStatus Status { get; }

    public InkkOopsTargetResolutionSource Source { get; }

    public UIElement? Element { get; }

    public AutomationPeer? Peer { get; }

    public IReadOnlyList<string> Notes { get; }

    public IReadOnlyList<string> Candidates { get; }

    public string Describe()
    {
        var lines = new List<string>
        {
            $"selector={Selector} status={Status} source={Source}"
        };

        lines.AddRange(Notes);
        if (Candidates.Count > 0)
        {
            lines.Add($"candidates: {string.Join(", ", Candidates)}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class InkkOopsTargetResolver
{
    public static InkkOopsTargetResolutionReport Resolve(IInkkOopsHost host, InkkOopsTargetReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Resolve(host, target.Selector);
    }

    public static InkkOopsTargetResolutionReport Resolve(IInkkOopsHost host, InkkOopsTargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(selector);

        return host.QueryOnUiThreadAsync(() => ResolveCore(host, selector)).GetAwaiter().GetResult();
    }

    private static InkkOopsTargetResolutionReport ResolveCore(IInkkOopsHost host, InkkOopsTargetSelector selector)
    {
        var notes = new List<string>();
        var candidates = new List<string>();
        var allPeers = host.GetAutomationPeersSnapshot();
        var root = host.GetVisualRootElement();
        if (root == null)
        {
            notes.Add("visual root is unavailable");
            return new InkkOopsTargetResolutionReport(
                selector,
                InkkOopsTargetResolutionStatus.Unresolved,
                InkkOopsTargetResolutionSource.None,
                null,
                null,
                notes,
                candidates);
        }

        return ResolveSelector(host, selector, root, includeScopeRoot: true, allPeers, notes, candidates);
    }

    private static InkkOopsTargetResolutionReport ResolveSelector(
        IInkkOopsHost host,
        InkkOopsTargetSelector selector,
        UIElement scopeRoot,
        bool includeScopeRoot,
        IReadOnlyList<AutomationPeer> allPeers,
        List<string> notes,
        List<string> candidates)
    {
        if (selector.Kind is InkkOopsTargetSelectorKind.Within or InkkOopsTargetSelectorKind.DescendantOf)
        {
            var containerSelector = selector.Container ?? throw new InvalidOperationException("Scoped selector is missing a container selector.");
            var targetSelector = selector.Target ?? throw new InvalidOperationException("Scoped selector is missing a target selector.");
            var containerReport = ResolveSelector(host, containerSelector, scopeRoot, includeScopeRoot, allPeers, new List<string>(), new List<string>());
            notes.Add($"scope selector {containerSelector} -> {containerReport.Status}");
            notes.AddRange(containerReport.Notes.Select(static note => "scope: " + note));

            if (containerReport.Status != InkkOopsTargetResolutionStatus.Resolved || containerReport.Element == null)
            {
                return new InkkOopsTargetResolutionReport(
                    selector,
                    containerReport.Status,
                    containerReport.Source,
                    null,
                    null,
                    notes,
                    containerReport.Candidates);
            }

            return ResolveSelector(
                host,
                targetSelector,
                containerReport.Element,
                includeScopeRoot: false,
                allPeers,
                notes,
                candidates);
        }

        var scopedElements = EnumerateVisuals(scopeRoot, includeScopeRoot).ToArray();
        var scopeSet = new HashSet<UIElement>(scopedElements);
        var identifier = selector.Identifier;
        var index = selector.Index;

        var xNameMatches = scopedElements
            .OfType<FrameworkElement>()
            .Where(element => string.Equals(element.Name, identifier, StringComparison.Ordinal))
            .Cast<UIElement>()
            .ToArray();
        if (xNameMatches.Length > 0)
        {
            notes.Add($"x:Name '{identifier}' -> {xNameMatches.Length} match(es)");
            return FinalizeMatches(selector, InkkOopsTargetResolutionSource.XName, xNameMatches, allPeers, index, notes, candidates);
        }

        notes.Add($"x:Name '{identifier}' -> no match");

        var idMatches = allPeers
            .Where(peer => scopeSet.Contains(peer.Owner) &&
                           string.Equals(peer.GetAutomationId(), identifier, StringComparison.Ordinal))
            .Select(static peer => peer.Owner)
            .Distinct()
            .ToArray();
        if (idMatches.Length > 0)
        {
            notes.Add($"automation id '{identifier}' -> {idMatches.Length} match(es)");
            return FinalizeMatches(selector, InkkOopsTargetResolutionSource.AutomationId, idMatches, allPeers, index, notes, candidates);
        }

        notes.Add($"automation id '{identifier}' -> no match");

        var nameMatches = allPeers
            .Where(peer => scopeSet.Contains(peer.Owner) &&
                           string.Equals(peer.GetName(), identifier, StringComparison.Ordinal))
            .Select(static peer => peer.Owner)
            .Distinct()
            .ToArray();
        if (nameMatches.Length > 0)
        {
            notes.Add($"automation name '{identifier}' -> {nameMatches.Length} match(es)");
            return FinalizeMatches(selector, InkkOopsTargetResolutionSource.AutomationName, nameMatches, allPeers, index, notes, candidates);
        }

        notes.Add($"automation name '{identifier}' -> no match");
        candidates.AddRange(allPeers
            .Where(peer => scopeSet.Contains(peer.Owner))
            .Select(DescribePeer)
            .Take(8));

        return new InkkOopsTargetResolutionReport(
            selector,
            InkkOopsTargetResolutionStatus.Unresolved,
            InkkOopsTargetResolutionSource.None,
            null,
            null,
            notes,
            candidates);
    }

    private static InkkOopsTargetResolutionReport FinalizeMatches(
        InkkOopsTargetSelector selector,
        InkkOopsTargetResolutionSource source,
        IReadOnlyList<UIElement> matches,
        IReadOnlyList<AutomationPeer> allPeers,
        int? requestedIndex,
        List<string> notes,
        List<string> candidates)
    {
        if (requestedIndex is int index)
        {
            if (index >= 0 && index < matches.Count)
            {
                var indexed = matches[index];
                notes.Add($"selector index {index} -> {DescribeElement(indexed)}");
                return new InkkOopsTargetResolutionReport(
                    selector,
                    InkkOopsTargetResolutionStatus.Resolved,
                    source,
                    indexed,
                    FindPeer(allPeers, indexed),
                    notes,
                    candidates);
            }

            notes.Add($"selector index {index} is outside match count {matches.Count}");
            candidates.AddRange(matches.Select(DescribeElement));
            return new InkkOopsTargetResolutionReport(
                selector,
                InkkOopsTargetResolutionStatus.Unresolved,
                source,
                null,
                null,
                notes,
                candidates);
        }

        if (matches.Count == 1)
        {
            var match = matches[0];
            notes.Add($"resolved -> {DescribeElement(match)}");
            return new InkkOopsTargetResolutionReport(
                selector,
                InkkOopsTargetResolutionStatus.Resolved,
                source,
                match,
                FindPeer(allPeers, match),
                notes,
                candidates);
        }

        candidates.AddRange(matches.Select(DescribeElement));
        notes.Add($"ambiguous selector -> {matches.Count} candidates");
        return new InkkOopsTargetResolutionReport(
            selector,
            InkkOopsTargetResolutionStatus.Ambiguous,
            source,
            null,
            null,
            notes,
            candidates);
    }

    private static AutomationPeer? FindPeer(IReadOnlyList<AutomationPeer> peers, UIElement element)
    {
        for (var i = 0; i < peers.Count; i++)
        {
            if (ReferenceEquals(peers[i].Owner, element))
            {
                return peers[i];
            }
        }

        return null;
    }

    private static IEnumerable<UIElement> EnumerateVisuals(UIElement root, bool includeRoot)
    {
        if (includeRoot)
        {
            yield return root;
        }

        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child, includeRoot: true))
            {
                yield return descendant;
            }
        }
    }

    internal static string DescribePeer(AutomationPeer peer)
    {
        return $"{DescribeElement(peer.Owner)}[name='{peer.GetName()}', id='{peer.GetAutomationId()}']";
    }

    internal static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        return element is FrameworkElement { Name.Length: > 0 } frameworkElement
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }
}
