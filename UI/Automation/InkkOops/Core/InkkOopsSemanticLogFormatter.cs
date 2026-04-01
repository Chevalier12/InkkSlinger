using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal static class InkkOopsSemanticLogFormatter
{
    public static InkkOopsSemanticLogEntry? Format(
        IInkkOopsCommand command,
        int index,
        string commandDescription,
        InkkOopsSemanticSnapshot before,
        InkkOopsSemanticSnapshot after,
        IReadOnlyList<IInkkOopsSemanticLogContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandDescription);
        ArgumentNullException.ThrowIfNull(contributors);

        var kind = GetKind(command);
        var shouldAlwaysLog = ShouldAlwaysLog(command);
        var rawPropsBefore = before.RawProperties;
        var rawPropsAfter = after.RawProperties;
        var ownerPropsBefore = before.OwnerProperties;
        var ownerPropsAfter = after.OwnerProperties;
        var hoveredPropsBefore = before.HoveredProperties;
        var hoveredPropsAfter = after.HoveredProperties;
        var capturedPropsBefore = before.CapturedProperties;
        var capturedPropsAfter = after.CapturedProperties;

        if (!shouldAlwaysLog &&
            !HasMeaningfulDelta(
                before,
                after,
                rawPropsBefore,
                rawPropsAfter,
                ownerPropsBefore,
                ownerPropsAfter,
                hoveredPropsBefore,
                hoveredPropsAfter,
                capturedPropsBefore,
                capturedPropsAfter))
        {
            return null;
        }

        var subject = DescribeSemanticSubject(after.RawTarget);
        var subjectBefore = DescribeSemanticSubject(before.RawTarget);
        var ownerBefore = DescribeElement(before.Owner);
        var ownerAfter = DescribeElement(after.Owner);
        var rawBefore = DescribeElement(before.RawTarget);
        var rawAfter = DescribeElement(after.RawTarget);
        var capturedBefore = DescribeElement(before.Captured);
        var capturedAfter = DescribeElement(after.Captured);
        var parts = new List<string>
        {
            $"{kind}[{index}]",
            $"owner={ownerAfter}"
        };

        if (shouldAlwaysLog)
        {
            parts.Add($"command={commandDescription}");
        }

        if (!string.Equals(rawAfter, ownerAfter, StringComparison.Ordinal))
        {
            parts.Add($"raw={rawAfter}");
        }

        if (!string.Equals(ownerBefore, ownerAfter, StringComparison.Ordinal))
        {
            parts.Add($"fromOwner={ownerBefore}");
        }

        if (!string.Equals(subjectBefore, subject, StringComparison.Ordinal) &&
            !string.Equals(subjectBefore, rawBefore, StringComparison.Ordinal))
        {
            parts.Add($"fromSubject={subjectBefore}");
        }

        if (!string.Equals(rawBefore, rawAfter, StringComparison.Ordinal) &&
            !string.Equals(rawBefore, ownerBefore, StringComparison.Ordinal))
        {
            parts.Add($"fromRaw={rawBefore}");
        }

        if (!string.Equals(capturedBefore, capturedAfter, StringComparison.Ordinal))
        {
            parts.Add($"capture={capturedBefore}->{capturedAfter}");
        }

        AppendPropertyDelta(parts, shouldAlwaysLog, "rawProps", rawPropsBefore, rawPropsAfter);
        AppendPropertyDelta(parts, shouldAlwaysLog, "ownerProps", ownerPropsBefore, ownerPropsAfter);
        AppendPropertyDelta(parts, shouldAlwaysLog, "hoveredProps", hoveredPropsBefore, hoveredPropsAfter);
        AppendPropertyDelta(parts, shouldAlwaysLog, "capturedProps", capturedPropsBefore, capturedPropsAfter);

        return new InkkOopsSemanticLogEntry(subject, string.Join(" ", parts));
    }

    public static InkkOopsSemanticSnapshot Capture(
        IInkkOopsCommand command,
        UiRoot uiRoot,
        IReadOnlyList<IInkkOopsSemanticLogContributor> contributors,
        int commandIndex,
        string commandDescription)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(uiRoot);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(commandDescription);

        var hovered = uiRoot.GetHoveredElementForDiagnostics();
        var captured = FocusManager.GetCapturedPointerElement();
        var clickDown = uiRoot.GetLastClickDownTargetForDiagnostics();
        var clickUp = uiRoot.GetLastClickUpTargetForDiagnostics();
        var rawTarget = ResolveRawTarget(command, hovered, captured, clickDown, clickUp);
        var owner = ResolveSemanticOwner(captured ?? rawTarget);
        var kind = GetKind(command);

        return new InkkOopsSemanticSnapshot(
            rawTarget,
            owner,
            hovered,
            captured,
            FormatProperties(rawTarget, contributors, commandIndex, commandDescription, kind, InkkOopsSemanticLogTarget.RawTarget),
            FormatProperties(owner, contributors, commandIndex, commandDescription, kind, InkkOopsSemanticLogTarget.Owner),
            FormatProperties(hovered, contributors, commandIndex, commandDescription, kind, InkkOopsSemanticLogTarget.Hovered),
            FormatProperties(captured, contributors, commandIndex, commandDescription, kind, InkkOopsSemanticLogTarget.Captured));
    }

    private static string GetKind(IInkkOopsCommand command)
    {
        return command switch
        {
            InkkOopsHoverTargetCommand => "Hover",
            InkkOopsMovePointerCommand => "MovePointer",
            InkkOopsClickTargetCommand => "Click",
            InkkOopsPointerDownCommand => "PointerDown",
            InkkOopsPointerUpCommand => "PointerUp",
            InkkOopsWheelCommand => "Wheel",
            InkkOopsDragTargetCommand => "Drag",
            InkkOopsInvokeTargetCommand => "Invoke",
            InkkOopsScrollByCommand => "ScrollBy",
            InkkOopsScrollToCommand => "ScrollTo",
            InkkOopsScrollIntoViewCommand => "ScrollIntoView",
            _ => command.GetType().Name.Replace("InkkOops", string.Empty, StringComparison.Ordinal).Replace("Command", string.Empty, StringComparison.Ordinal)
        };
    }

    private static bool ShouldAlwaysLog(IInkkOopsCommand command)
    {
        return command is not InkkOopsHoverTargetCommand and not InkkOopsMovePointerCommand;
    }

    private static bool HasMeaningfulDelta(
        InkkOopsSemanticSnapshot before,
        InkkOopsSemanticSnapshot after,
        string rawPropsBefore,
        string rawPropsAfter,
        string ownerPropsBefore,
        string ownerPropsAfter,
        string hoveredPropsBefore,
        string hoveredPropsAfter,
        string capturedPropsBefore,
        string capturedPropsAfter)
    {
        return !ReferenceEquals(before.Owner, after.Owner) ||
               !ReferenceEquals(before.Captured, after.Captured) ||
               !string.Equals(rawPropsBefore, rawPropsAfter, StringComparison.Ordinal) ||
               !string.Equals(ownerPropsBefore, ownerPropsAfter, StringComparison.Ordinal) ||
               !string.Equals(hoveredPropsBefore, hoveredPropsAfter, StringComparison.Ordinal) ||
               !string.Equals(capturedPropsBefore, capturedPropsAfter, StringComparison.Ordinal);
    }

    private static void AppendPropertyDelta(List<string> parts, bool shouldAlwaysLog, string label, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            parts.Add($"{label}={before}->{after}");
            return;
        }

        if (shouldAlwaysLog && !string.Equals(after, "none", StringComparison.Ordinal))
        {
            parts.Add($"{label}={after}");
        }
    }

    private static UIElement? ResolveRawTarget(
        IInkkOopsCommand command,
        UIElement? hovered,
        UIElement? captured,
        UIElement? clickDown,
        UIElement? clickUp)
    {
        return command switch
        {
            InkkOopsClickTargetCommand => clickUp ?? clickDown ?? hovered ?? captured,
            InkkOopsPointerUpCommand => clickUp ?? clickDown ?? captured ?? hovered,
            InkkOopsPointerDownCommand => clickDown ?? captured ?? hovered,
            InkkOopsInvokeTargetCommand => clickUp ?? clickDown ?? hovered ?? captured,
            InkkOopsWheelCommand => captured ?? hovered,
            InkkOopsHoverTargetCommand => hovered ?? captured,
            InkkOopsMovePointerCommand => hovered ?? captured,
            InkkOopsDragTargetCommand => captured ?? clickDown ?? hovered,
            _ => hovered ?? captured ?? clickUp ?? clickDown
        };
    }

    private static UIElement? ResolveSemanticOwner(UIElement? element)
    {
        if (element == null)
        {
            return null;
        }

        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (HasSemanticIdentity(current))
            {
                return current;
            }
        }

        return element;
    }

    private static bool HasSemanticIdentity(UIElement element)
    {
        if (element is FrameworkElement { Name.Length: > 0 })
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(element)) ||
               !string.IsNullOrWhiteSpace(AutomationProperties.GetName(element));
    }

    private static string FormatProperties(
        UIElement? element,
        IReadOnlyList<IInkkOopsSemanticLogContributor> contributors,
        int commandIndex,
        string commandDescription,
        string commandKind,
        InkkOopsSemanticLogTarget target)
    {
        if (element == null || contributors.Count == 0)
        {
            return "none";
        }

        var builder = new InkkOopsSemanticLogPropertyBuilder();
        var context = new InkkOopsSemanticLogContext
        {
            CommandIndex = commandIndex,
            CommandDescription = commandDescription,
            CommandKind = commandKind,
            Target = target
        };

        foreach (var contributor in contributors)
        {
            contributor.Contribute(context, element, builder);
        }

        return builder.Properties.Count == 0
            ? "none"
            : string.Join(" ", builder.Properties);
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        var description = InkkOopsTargetResolver.DescribeElement(element);
        var automationId = AutomationProperties.GetAutomationId(element);
        var automationName = AutomationProperties.GetName(element);

        if (string.IsNullOrWhiteSpace(automationId) && string.IsNullOrWhiteSpace(automationName))
        {
            return description;
        }

        if (string.IsNullOrWhiteSpace(automationId))
        {
            return $"{description}[autoName='{automationName}']";
        }

        if (string.IsNullOrWhiteSpace(automationName))
        {
            return $"{description}[autoId='{automationId}']";
        }

        return $"{description}[autoName='{automationName}', autoId='{automationId}']";
    }

    private static string DescribeSemanticSubject(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        if (HasSemanticIdentity(element))
        {
            return DescribeElement(element);
        }

        var segments = new Stack<string>();
        var current = element;
        while (current != null)
        {
            var parent = current.VisualParent ?? current.LogicalParent;
            if (parent == null)
            {
                segments.Push(DescribeElement(current));
                break;
            }

            segments.Push(DescribeIndexedSegment(parent, current));
            if (HasSemanticIdentity(parent))
            {
                segments.Push(DescribeElement(parent));
                break;
            }

            current = parent;
        }

        return string.Join(" / ", segments);
    }

    private static string DescribeIndexedSegment(UIElement parent, UIElement child)
    {
        if (HasSemanticIdentity(child))
        {
            return DescribeElement(child);
        }

        var index = GetChildIndex(parent, child);
        return index >= 0
            ? $"{child.GetType().Name}[{index}]"
            : child.GetType().Name;
    }

    private static int GetChildIndex(UIElement parent, UIElement child)
    {
        var traversalChildCount = parent.GetVisualChildCountForTraversal();
        if (traversalChildCount >= 0)
        {
            for (var i = 0; i < traversalChildCount; i++)
            {
                if (ReferenceEquals(parent.GetVisualChildAtForTraversal(i), child))
                {
                    return i;
                }
            }
        }

        var visualIndex = 0;
        foreach (var visualChild in parent.GetVisualChildren())
        {
            if (ReferenceEquals(visualChild, child))
            {
                return visualIndex;
            }

            visualIndex++;
        }

        return -1;
    }
}

internal readonly record struct InkkOopsSemanticSnapshot(
    UIElement? RawTarget,
    UIElement? Owner,
    UIElement? Hovered,
    UIElement? Captured,
    string RawProperties,
    string OwnerProperties,
    string HoveredProperties,
    string CapturedProperties);

internal readonly record struct InkkOopsSemanticLogEntry(string Subject, string Details);