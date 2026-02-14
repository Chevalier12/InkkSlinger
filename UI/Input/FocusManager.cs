using System.Collections.Generic;

namespace InkkSlinger;

public static class FocusManager
{
    public static event System.EventHandler<FocusChangedEventArgs>? FocusChanged;
    private static UIElement? _cachedTraversalRoot;
    private static int _focusGraphVersion;
    private static int _cachedTraversalVersion = -1;
    private static readonly List<UIElement> CachedTraversalCandidates = new();
    private static readonly Dictionary<UIElement, int> CachedCandidateIndices = new();
    private static int _traversalCacheBuilds;
    private static int _traversalCacheHits;

    public static UIElement? FocusedElement { get; private set; }

    public static bool SetFocusedElement(UIElement? element)
    {
        if (element == FocusedElement)
        {
            return true;
        }

        if (element != null && !IsFocusableElement(element))
        {
            return false;
        }

        var oldFocused = FocusedElement;
        FocusedElement = element;

        oldFocused?.NotifyLostFocus(element);
        element?.NotifyGotFocus(oldFocused);

        FocusChanged?.Invoke(null, new FocusChangedEventArgs(oldFocused, element));
        return true;
    }

    public static bool MoveFocus(UIElement root, bool backwards = false)
    {
        GetTraversalCache(root, out var candidates, out var candidateIndices);
        if (candidates.Count == 0)
        {
            return SetFocusedElement(null);
        }

        var currentIndex = -1;
        if (FocusedElement != null &&
            candidateIndices.TryGetValue(FocusedElement, out var focusedIndex))
        {
            currentIndex = focusedIndex;
        }

        var nextIndex = ResolveNextFocusIndex(candidates.Count, currentIndex, backwards);
        return SetFocusedElement(candidates[nextIndex]);
    }

    internal static bool IsFocusableElement(UIElement element)
    {
        return element.Focusable && element.IsEnabled && element.IsVisible;
    }

    internal static void ResetForTests()
    {
        FocusedElement = null;
        _cachedTraversalRoot = null;
        _focusGraphVersion = 0;
        _cachedTraversalVersion = -1;
        CachedTraversalCandidates.Clear();
        CachedCandidateIndices.Clear();
        _traversalCacheBuilds = 0;
        _traversalCacheHits = 0;
    }

    internal static void NotifyFocusGraphInvalidated()
    {
        unchecked
        {
            _focusGraphVersion++;
        }
    }

    internal static (int Builds, int Hits) GetTraversalCacheStatsForTests()
    {
        return (_traversalCacheBuilds, _traversalCacheHits);
    }

    private static void GetTraversalCache(
        UIElement root,
        out List<UIElement> candidates,
        out Dictionary<UIElement, int> candidateIndices)
    {
        if (ReferenceEquals(_cachedTraversalRoot, root) &&
            _cachedTraversalVersion == _focusGraphVersion)
        {
            _traversalCacheHits++;
            candidates = CachedTraversalCandidates;
            candidateIndices = CachedCandidateIndices;
            return;
        }

        CachedTraversalCandidates.Clear();
        CachedCandidateIndices.Clear();
        CollectFocusableCandidates(root, CachedTraversalCandidates, new HashSet<UIElement>());
        for (var i = 0; i < CachedTraversalCandidates.Count; i++)
        {
            CachedCandidateIndices[CachedTraversalCandidates[i]] = i;
        }

        _cachedTraversalRoot = root;
        _cachedTraversalVersion = _focusGraphVersion;
        _traversalCacheBuilds++;
        candidates = CachedTraversalCandidates;
        candidateIndices = CachedCandidateIndices;
    }

    private static void CollectFocusableCandidates(UIElement element, IList<UIElement> candidates, ISet<UIElement> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        if (IsFocusableElement(element))
        {
            candidates.Add(element);
        }

        foreach (var child in element.GetVisualChildren())
        {
            CollectFocusableCandidates(child, candidates, visited);
        }

        foreach (var child in element.GetLogicalChildren())
        {
            CollectFocusableCandidates(child, candidates, visited);
        }
    }

    private static int ResolveNextFocusIndex(int count, int currentIndex, bool backwards)
    {
        if (count == 1)
        {
            return 0;
        }

        if (currentIndex < 0)
        {
            return backwards ? count - 1 : 0;
        }

        if (backwards)
        {
            return currentIndex == 0 ? count - 1 : currentIndex - 1;
        }

        return currentIndex == count - 1 ? 0 : currentIndex + 1;
    }
}

public sealed class FocusChangedEventArgs : System.EventArgs
{
    public FocusChangedEventArgs(UIElement? oldFocusedElement, UIElement? newFocusedElement)
    {
        OldFocusedElement = oldFocusedElement;
        NewFocusedElement = newFocusedElement;
    }

    public UIElement? OldFocusedElement { get; }

    public UIElement? NewFocusedElement { get; }
}
