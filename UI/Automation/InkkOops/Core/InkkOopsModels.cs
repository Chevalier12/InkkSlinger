using System;
using System.Collections.Generic;
using System.Numerics;

namespace InkkSlinger;

public enum InkkOopsExecutionMode
{
    Diagnostic,
    Pointer,
    Semantic
}

public enum InkkOopsFailureCategory
{
    None,
    Unresolved,
    Ambiguous,
    Unrealized,
    Offscreen,
    Clipped,
    Disabled,
    NotInteractive,
    SemanticProviderMissing,
    UiThreadViolation,
    Timeout
}

public enum InkkOopsIdlePolicy
{
    LayoutAndRender,
    InputStable,
    DiagnosticsStable
}

public enum InkkOopsTargetSelectorKind
{
    Name,
    AutomationId,
    AutomationName,
    Within,
    DescendantOf
}

public enum InkkOopsTargetResolutionStatus
{
    Resolved,
    Unresolved,
    Ambiguous
}

public enum InkkOopsScrollLocatorKind
{
    ElementSelector,
    ItemText,
    ItemIndex
}

public enum InkkOopsWaitCondition
{
    Exists,
    Visible,
    Enabled,
    InViewport,
    Interactive
}

public enum InkkOopsPointerAnchorKind
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Offset
}

public sealed class InkkOopsTargetSelector
{
    private InkkOopsTargetSelector(
        InkkOopsTargetSelectorKind kind,
        string identifier,
        InkkOopsTargetSelector? container,
        InkkOopsTargetSelector? target,
        int? index)
    {
        Kind = kind;
        Identifier = identifier ?? string.Empty;
        Container = container;
        Target = target;
        Index = index;
    }

    public InkkOopsTargetSelectorKind Kind { get; }

    public string Identifier { get; }

    public InkkOopsTargetSelector? Container { get; }

    public InkkOopsTargetSelector? Target { get; }

    public int? Index { get; }

    public static InkkOopsTargetSelector Name(string name)
    {
        return new(InkkOopsTargetSelectorKind.Name, name, null, null, null);
    }

    public static InkkOopsTargetSelector AutomationId(string automationId)
    {
        return new(InkkOopsTargetSelectorKind.AutomationId, automationId, null, null, null);
    }

    public static InkkOopsTargetSelector AutomationName(string automationName)
    {
        return new(InkkOopsTargetSelectorKind.AutomationName, automationName, null, null, null);
    }

    public static InkkOopsTargetSelector Within(InkkOopsTargetSelector container, InkkOopsTargetSelector target)
    {
        return new(InkkOopsTargetSelectorKind.Within, string.Empty, container, target, null);
    }

    public static InkkOopsTargetSelector DescendantOf(InkkOopsTargetSelector ancestor, InkkOopsTargetSelector target)
    {
        return new(InkkOopsTargetSelectorKind.DescendantOf, string.Empty, ancestor, target, null);
    }

    public InkkOopsTargetSelector WithIndex(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new(Kind, Identifier, Container, Target, index);
    }

    public override string ToString()
    {
        var baseText = Kind switch
        {
            InkkOopsTargetSelectorKind.Name => $"Name('{Identifier}')",
            InkkOopsTargetSelectorKind.AutomationId => $"AutomationId('{Identifier}')",
            InkkOopsTargetSelectorKind.AutomationName => $"AutomationName('{Identifier}')",
            InkkOopsTargetSelectorKind.Within => $"Within({Container}, {Target})",
            InkkOopsTargetSelectorKind.DescendantOf => $"DescendantOf({Container}, {Target})",
            _ => Kind.ToString()
        };

        return Index is int index ? $"{baseText}[{index}]" : baseText;
    }
}

public readonly record struct InkkOopsPointerAnchor(InkkOopsPointerAnchorKind Kind, Vector2 Offset)
{
    public static InkkOopsPointerAnchor Center => new(InkkOopsPointerAnchorKind.Center, Vector2.Zero);

    public static InkkOopsPointerAnchor TopLeft => new(InkkOopsPointerAnchorKind.TopLeft, Vector2.Zero);

    public static InkkOopsPointerAnchor TopRight => new(InkkOopsPointerAnchorKind.TopRight, Vector2.Zero);

    public static InkkOopsPointerAnchor BottomLeft => new(InkkOopsPointerAnchorKind.BottomLeft, Vector2.Zero);

    public static InkkOopsPointerAnchor BottomRight => new(InkkOopsPointerAnchorKind.BottomRight, Vector2.Zero);

    public static InkkOopsPointerAnchor OffsetBy(float x, float y)
    {
        return new(InkkOopsPointerAnchorKind.Offset, new Vector2(x, y));
    }

    public override string ToString()
    {
        return Kind == InkkOopsPointerAnchorKind.Offset
            ? $"Offset({Offset.X:0.###}, {Offset.Y:0.###})"
            : Kind.ToString();
    }
}

public sealed class InkkOopsScrollLocator
{
    private InkkOopsScrollLocator(
        InkkOopsScrollLocatorKind kind,
        InkkOopsTargetSelector? elementSelector,
        string itemText,
        int? itemIndex)
    {
        Kind = kind;
        ElementSelector = elementSelector;
        ItemText = itemText ?? string.Empty;
        ItemIndex = itemIndex;
    }

    public InkkOopsScrollLocatorKind Kind { get; }

    public InkkOopsTargetSelector? ElementSelector { get; }

    public string ItemText { get; }

    public int? ItemIndex { get; }

    public static InkkOopsScrollLocator ForElement(InkkOopsTargetSelector selector)
    {
        return new(InkkOopsScrollLocatorKind.ElementSelector, selector, string.Empty, null);
    }

    public static InkkOopsScrollLocator ByItemText(string itemText)
    {
        return new(InkkOopsScrollLocatorKind.ItemText, null, itemText, null);
    }

    public static InkkOopsScrollLocator ByItemIndex(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new(InkkOopsScrollLocatorKind.ItemIndex, null, string.Empty, index);
    }

    public override string ToString()
    {
        return Kind switch
        {
            InkkOopsScrollLocatorKind.ElementSelector => ElementSelector?.ToString() ?? "Element(<null>)",
            InkkOopsScrollLocatorKind.ItemText => $"ItemText('{ItemText}')",
            InkkOopsScrollLocatorKind.ItemIndex => $"ItemIndex({ItemIndex})",
            _ => Kind.ToString()
        };
    }
}

public sealed class InkkOopsTargetStateSnapshot
{
    public static InkkOopsTargetStateSnapshot Unresolved(
        InkkOopsTargetResolutionReport resolution,
        InkkOopsFailureCategory category)
    {
        return new InkkOopsTargetStateSnapshot
        {
            Resolution = resolution,
            FailureCategory = category
        };
    }

    public InkkOopsTargetResolutionReport Resolution { get; init; } = null!;

    public UIElement? Element { get; init; }

    public AutomationPeer? Peer { get; init; }

    public LayoutRect Bounds { get; init; }

    public bool HasBounds { get; init; }

    public LayoutRect ViewportBounds { get; init; }

    public bool HasViewportBounds { get; init; }

    public Vector2 ActionPoint { get; init; }

    public bool HasActionPoint { get; init; }

    public bool IsVisible { get; init; }

    public bool IsEnabled { get; init; }

    public bool IsInViewport { get; init; }

    public bool IsHitTestVisibleAtActionPoint { get; init; }

    public bool IsInteractive { get; init; }

    public InkkOopsFailureCategory FailureCategory { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed class InkkOopsCommandDiagnostics
{
    public int CommandIndex { get; set; }

    public string Description { get; set; } = string.Empty;

    public InkkOopsExecutionMode ExecutionMode { get; set; }

    public string Status { get; set; } = "Pending";

    public InkkOopsFailureCategory FailureCategory { get; set; }

    public string FailureMessage { get; set; } = string.Empty;

    public string? Selector { get; set; }

    public string ResolutionStatus { get; set; } = string.Empty;

    public string ResolutionSource { get; set; } = string.Empty;

    public List<string> ResolutionNotes { get; } = new();

    public List<string> ResolutionCandidates { get; } = new();

    public string MatchedElement { get; set; } = string.Empty;

    public string MatchedPeer { get; set; } = string.Empty;

    public string HoveredBefore { get; set; } = string.Empty;

    public string HoveredAfter { get; set; } = string.Empty;

    public string FocusedBefore { get; set; } = string.Empty;

    public string FocusedAfter { get; set; } = string.Empty;

    public string Anchor { get; set; } = string.Empty;

    public float? ActionPointX { get; set; }

    public float? ActionPointY { get; set; }

    public float? BoundsX { get; set; }

    public float? BoundsY { get; set; }

    public float? BoundsWidth { get; set; }

    public float? BoundsHeight { get; set; }

    public float? ViewportX { get; set; }

    public float? ViewportY { get; set; }

    public float? ViewportWidth { get; set; }

    public float? ViewportHeight { get; set; }

    public bool? Visible { get; set; }

    public bool? Enabled { get; set; }

    public bool? InViewport { get; set; }

    public bool? Interactive { get; set; }

    public List<object> AutomationEvents { get; } = new();

    public DateTime StartedUtc { get; set; }

    public DateTime CompletedUtc { get; set; }
}

public sealed class InkkOopsCommandException : InvalidOperationException
{
    public InkkOopsCommandException(InkkOopsFailureCategory category, string message)
        : base(message)
    {
        Category = category;
    }

    public InkkOopsFailureCategory Category { get; }
}
