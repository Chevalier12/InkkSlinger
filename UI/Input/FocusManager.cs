using System.Collections.Generic;

namespace InkkSlinger;

public static class FocusManager
{
    public static event System.EventHandler<FocusChangedEventArgs>? FocusChanged;

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
        var candidates = new List<UIElement>();
        CollectFocusableCandidates(root, candidates, new HashSet<UIElement>());
        if (candidates.Count == 0)
        {
            return SetFocusedElement(null);
        }

        var currentIndex = FocusedElement == null ? -1 : candidates.IndexOf(FocusedElement);
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
