using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal static class AccessKeyService
{
    internal static bool TryExecute(char accessKey, UIElement visualRoot, UIElement? focusedElement)
    {
        var expectedKey = char.ToUpperInvariant(accessKey);
        if (focusedElement != null &&
            TryExecuteMatchingCandidate(expectedKey, visualRoot, focusedElement, requireFocusedRoute: true))
        {
            return true;
        }

        return TryExecuteMatchingCandidate(expectedKey, visualRoot, focusedElement, requireFocusedRoute: false);
    }

    private static bool TryExecuteMatchingCandidate(
        char expectedKey,
        UIElement visualRoot,
        UIElement? focusedElement,
        bool requireFocusedRoute)
    {
        foreach (var accessText in EnumerateAccessTextCandidates(visualRoot))
        {
            if (accessText.AccessKey is not char candidateKey ||
                char.ToUpperInvariant(candidateKey) != expectedKey)
            {
                continue;
            }

            var target = ResolveTarget(accessText);
            if (target == null || !target.IsEnabled || !target.IsVisible)
            {
                continue;
            }

            if (requireFocusedRoute && !IsOnFocusedAncestorRoute(target, focusedElement))
            {
                continue;
            }

            if (TryActivateTarget(target))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<AccessText> EnumerateAccessTextCandidates(UIElement root)
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is AccessText accessText && accessText.IsVisible && accessText.IsEnabled)
            {
                yield return accessText;
            }

            var children = new List<UIElement>();
            foreach (var child in current.GetVisualChildren())
            {
                children.Add(child);
            }

            for (var i = children.Count - 1; i >= 0; i--)
            {
                pending.Push(children[i]);
            }
        }
    }

    private static FrameworkElement? ResolveTarget(AccessText accessText)
    {
        if (!string.IsNullOrWhiteSpace(accessText.TargetName))
        {
            return ResolveTargetByName(accessText, accessText.TargetName);
        }

        for (var current = accessText.VisualParent ?? accessText.LogicalParent;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is FrameworkElement frameworkElement &&
                frameworkElement.RecognizesAccessKey)
            {
                return frameworkElement;
            }
        }

        return null;
    }

    private static FrameworkElement? ResolveTargetByName(AccessText accessText, string targetName)
    {
        for (var current = accessText as FrameworkElement;
             current != null;
             current = current.VisualParent as FrameworkElement ?? current.LogicalParent as FrameworkElement)
        {
            var resolved = NameScopeService.FindName(current, targetName);
            if (resolved is FrameworkElement frameworkElement)
            {
                return frameworkElement;
            }

            var inTree = current.FindName(targetName);
            if (inTree != null)
            {
                return inTree;
            }
        }

        return null;
    }

    private static bool TryActivateTarget(FrameworkElement target)
    {
        if (target is Button button)
        {
            button.InvokeFromInput();
            return true;
        }

        if (target.Focusable)
        {
            FocusManager.SetFocus(target);
            return true;
        }

        return false;
    }

    private static bool IsOnFocusedAncestorRoute(FrameworkElement target, UIElement? focusedElement)
    {
        if (focusedElement == null)
        {
            return false;
        }

        for (var current = focusedElement;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }
}
