using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger.Designer;

public sealed class DesignerProjectExplorerHoverRuntimeScenario : IInkkOopsScriptDefinition
{
    private static readonly float[] HoverOffsets = [14f, 38f, 62f, 86f, 110f, 134f, 158f, 182f];

    public const string ScriptName = "designer-project-explorer-hover-recent-project";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));
        var hoverMotion = InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f);

        var builder = new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .CaptureFrame("start-page")
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-hover")
            .CaptureFrame("workspace-before-hover");

        ApplyHoverSweep(builder, hoverMotion);

        return builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-hover")
            .CaptureFrame("workspace-after-hover")
            .Build();
    }

    internal static InkkOopsScriptBuilder ApplyHoverSweep(InkkOopsScriptBuilder builder, InkkOopsPointerMotion hoverMotion)
    {
        foreach (var offsetY in HoverOffsets)
        {
            builder.MovePointerTo("ProjectExplorerTree", InkkOopsPointerAnchor.OffsetBy(80f, offsetY), hoverMotion);
        }

        return builder;
    }
}

public sealed class DesignerProjectExplorerRepeatedHoverRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-hover-repeated";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));
        var hoverMotion = InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f);
        var builder = new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-repeated-hover")
            .CaptureFrame("workspace-before-repeated-hover");

        for (var sweep = 1; sweep <= 6; sweep++)
        {
            DesignerProjectExplorerHoverRuntimeScenario.ApplyHoverSweep(builder, hoverMotion);
            builder.DumpTelemetry($"after-repeated-hover-sweep-{sweep}");
            builder.WaitFrames(1);
        }

        return builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-repeated-hover-idle")
            .CaptureFrame("workspace-after-repeated-hover")
            .Build();
    }
}

public sealed class DesignerProjectExplorerScrolledRepeatedHoverRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-hover-scrolled-repeated";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));
        var hoverMotion = InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f);
        var wheelMotion = InkkOopsPointerMotion.Default;
        var builder = new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-scroll")
            .CaptureFrame("workspace-before-scroll")
            .Wheel("ProjectExplorerTree", -720, InkkOopsPointerAnchor.Center, wheelMotion)
            .WaitFrames(2)
            .Wheel("ProjectExplorerTree", -720, InkkOopsPointerAnchor.Center, wheelMotion)
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-scroll-before-hover")
            .CaptureFrame("workspace-after-scroll-before-hover");

        for (var sweep = 1; sweep <= 6; sweep++)
        {
            DesignerProjectExplorerHoverRuntimeScenario.ApplyHoverSweep(builder, hoverMotion);
            builder.DumpTelemetry($"after-scrolled-repeated-hover-sweep-{sweep}");
            builder.WaitFrames(1);
        }

        return builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-scrolled-repeated-hover-idle")
            .CaptureFrame("workspace-after-scrolled-repeated-hover")
            .Build();
    }
}

public sealed class DesignerProjectExplorerPrepareCommitScrolledHoverRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-hover-prepare-commit-msg-top";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));
        var hoverMotion = InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f);
        var builder = new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-prepare-commit-msg-scroll")
            .Add(new DesignerProjectExplorerScrollItemToTopCommand(
                "ProjectExplorerTree",
                "prepare-commit-msg.sample",
                "prepare-commit-msg-at-top-before-hover"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-prepare-commit-msg-scroll")
            .CaptureFrame("workspace-prepare-commit-msg-at-top-before-hover");

        for (var sweep = 1; sweep <= 6; sweep++)
        {
            DesignerProjectExplorerHoverRuntimeScenario.ApplyHoverSweep(builder, hoverMotion);
            builder.DumpTelemetry($"after-prepare-commit-msg-hover-sweep-{sweep}");
            builder.WaitFrames(1);
        }

        return builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-prepare-commit-msg-hover-idle")
            .CaptureFrame("workspace-after-prepare-commit-msg-hover")
            .Build();
    }
}

public sealed class DesignerProjectExplorerPrePushClickRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-pre-push-click";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));

        return new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-pre-push-click-scroll")
            .Add(new DesignerProjectExplorerScrollItemToTopCommand(
                "ProjectExplorerTree",
                "pre-push.sample",
                "pre-push-at-top-before-click"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-pre-push-click-scroll")
            .Add(new DesignerProjectExplorerClickItemAndAssertSelectedCommand(
                "ProjectExplorerTree",
                "pre-push.sample",
                "after-pre-push-click"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-pre-push-click-idle")
            .CaptureFrame("workspace-after-pre-push-click")
            .Build();
    }
}

public sealed class DesignerProjectExplorerClaudeCollapseRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-claude-collapse";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));

        return new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .Add(new DesignerProjectExplorerClickItemAndAssertSelectedCommand(
                "ProjectExplorerTree",
                "InkkSlinger",
                "after-project-root-click"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("before-claude-collapse")
            .CaptureFrame("workspace-before-claude-collapse")
            .Add(new DesignerProjectExplorerCollapseItemAndAssertNoStaleDescendantsCommand(
                "ProjectExplorerTree",
                ".claude",
                "after-claude-collapse"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-claude-collapse-idle")
            .CaptureFrame("workspace-after-claude-collapse")
            .Build();
    }
}

public sealed class DesignerProjectExplorerInfoClickRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-info-click";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        return new InkkOopsScriptBuilder(Name)
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .Add(new DesignerProjectExplorerScrollItemToTopCommand(
                "ProjectExplorerTree",
                "info",
                "info-at-top-before-click"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .Add(new DesignerProjectExplorerClickItemAndAssertSelectedCommand(
                "ProjectExplorerTree",
                "info",
                "after-info-click"))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .CaptureFrame("workspace-after-info-click")
            .Build();
    }
}

internal sealed class DesignerProjectExplorerClickItemAndAssertSelectedCommand : IInkkOopsCommand
{
    public DesignerProjectExplorerClickItemAndAssertSelectedCommand(string treeViewName, string itemText, string artifactName)
    {
        TreeViewName = string.IsNullOrWhiteSpace(treeViewName)
            ? throw new ArgumentException("TreeView name is required.", nameof(treeViewName))
            : treeViewName;
        ItemText = string.IsNullOrWhiteSpace(itemText)
            ? throw new ArgumentException("Item text is required.", nameof(itemText))
            : itemText;
        ArtifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public string TreeViewName { get; }

    public string ItemText { get; }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"ClickTreeViewItemAndAssertSelected(Name('{TreeViewName}'), item: {ItemText}, artifact: {ArtifactName})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = await session.QueryOnUiThreadAsync(() => ResolveClickPoint(session), cancellationToken).ConfigureAwait(false);
        await session.MovePointerAsync(point, InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f), cancellationToken).ConfigureAwait(false);
        var afterMove = await session.QueryOnUiThreadAsync(() => CapturePointerEvidence(session), cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        var afterPress = await session.QueryOnUiThreadAsync(() => CapturePointerEvidence(session), cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        var afterRelease = await session.QueryOnUiThreadAsync(() => CapturePointerEvidence(session), cancellationToken).ConfigureAwait(false);
        await session.WaitFramesAsync(2, cancellationToken).ConfigureAwait(false);
        await session.ExecuteOnUiThreadAsync(() => WriteSelectionEvidenceAndAssert(session, point, afterMove, afterPress, afterRelease), cancellationToken).ConfigureAwait(false);
    }

    private System.Numerics.Vector2 ResolveClickPoint(InkkOopsSession session)
    {
        var treeView = ResolveTreeView(session);
        var targetItem = ResolveItem(treeView);
        if (!targetItem.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unrealized,
                $"TreeViewItem '{ItemText}' does not expose render bounds before click.");
        }

        return new System.Numerics.Vector2(
            bounds.X + MathF.Min(80f, MathF.Max(1f, bounds.Width - 1f)),
            bounds.Y + MathF.Min(8f, MathF.Max(1f, bounds.Height - 1f)));
    }

    private ClickPointerEvidence CapturePointerEvidence(InkkOopsSession session)
    {
        return new ClickPointerEvidence(
            session.UiRoot.LastPointerResolvePathForDiagnostics,
            session.UiRoot.LastClickDownResolvePathForDiagnostics,
            session.UiRoot.LastClickUpResolvePathForDiagnostics,
            DescribeTreeItem(session.UiRoot.GetLastClickDownTargetForDiagnostics()),
            DescribeTreeItem(session.UiRoot.GetLastClickUpTargetForDiagnostics()));
    }

    private void WriteSelectionEvidenceAndAssert(
        InkkOopsSession session,
        System.Numerics.Vector2 point,
        ClickPointerEvidence afterMove,
        ClickPointerEvidence afterPress,
        ClickPointerEvidence afterRelease)
    {
        var treeView = ResolveTreeView(session);
        var targetItem = ResolveItem(treeView);
        var selected = treeView.SelectedItem;
        var hitAtClick = VisualTreeHelper.HitTest(treeView, new Microsoft.Xna.Framework.Vector2(point.X, point.Y));
        var selectedHeader = selected?.Header ?? "<null>";
        var selectedNormalized = selected == null ? "<null>" : NormalizeHeader(selected.Header);
        var matched = ReferenceEquals(selected, targetItem);

        var builder = new StringBuilder();
        builder.AppendLine($"artifact_name={ArtifactName}");
        builder.AppendLine($"tree_view={TreeViewName}");
        builder.AppendLine($"target_item={ItemText}");
        builder.AppendLine(FormattableString.Invariant($"click_point=({point.X:0.###},{point.Y:0.###})"));
        builder.AppendLine($"selected_header={selectedHeader}");
        builder.AppendLine($"selected_normalized={selectedNormalized}");
        builder.AppendLine($"selected_matches_target={matched}");
        builder.AppendLine($"hit_at_click={DescribeTreeItem(hitAtClick)}");
        builder.AppendLine($"after_move_path={afterMove.ResolvePath}");
        builder.AppendLine($"after_move_down_path={afterMove.ClickDownResolvePath}");
        builder.AppendLine($"after_move_up_path={afterMove.ClickUpResolvePath}");
        builder.AppendLine($"after_move_down={afterMove.ClickDownTarget}");
        builder.AppendLine($"after_move_up={afterMove.ClickUpTarget}");
        builder.AppendLine($"after_press_path={afterPress.ResolvePath}");
        builder.AppendLine($"after_press_down_path={afterPress.ClickDownResolvePath}");
        builder.AppendLine($"after_press_up_path={afterPress.ClickUpResolvePath}");
        builder.AppendLine($"after_press_down={afterPress.ClickDownTarget}");
        builder.AppendLine($"after_press_up={afterPress.ClickUpTarget}");
        builder.AppendLine($"after_release_path={afterRelease.ResolvePath}");
        builder.AppendLine($"after_release_down_path={afterRelease.ClickDownResolvePath}");
        builder.AppendLine($"after_release_up_path={afterRelease.ClickUpResolvePath}");
        builder.AppendLine($"after_release_down={afterRelease.ClickDownTarget}");
        builder.AppendLine($"after_release_up={afterRelease.ClickUpTarget}");
        builder.AppendLine($"last_pointer_resolve_path={session.UiRoot.LastPointerResolvePathForDiagnostics}");
        builder.AppendLine($"last_click_down={DescribeTreeItem(session.UiRoot.GetLastClickDownTargetForDiagnostics())}");
        builder.AppendLine($"last_click_up={DescribeTreeItem(session.UiRoot.GetLastClickUpTargetForDiagnostics())}");
        if (targetItem.TryGetRenderBoundsInRootSpace(out var targetBounds))
        {
            builder.AppendLine(FormattableString.Invariant($"target_y={targetBounds.Y:0.###}"));
            builder.AppendLine(FormattableString.Invariant($"target_height={targetBounds.Height:0.###}"));
        }

        var fileName = ArtifactName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? ArtifactName
            : ArtifactName + ".txt";
        session.Artifacts.BufferTextArtifact(fileName, builder.ToString());

        if (!matched)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.NotInteractive,
                $"Expected TreeViewItem '{ItemText}' to be selected after click, but selected '{selectedNormalized}'.");
        }
    }

    private TreeView ResolveTreeView(InkkOopsSession session)
    {
        var target = new InkkOopsTargetReference(TreeViewName);
        return session.ResolveRequiredTarget(target) as TreeView
            ?? throw new InkkOopsCommandException(
                InkkOopsFailureCategory.SemanticProviderMissing,
                $"Target '{TreeViewName}' is not a TreeView.");
    }

    private TreeViewItem ResolveItem(TreeView treeView)
    {
        foreach (var item in EnumerateTreeItems(treeView))
        {
            if (string.Equals(NormalizeHeader(item.Header), ItemText, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Could not find TreeViewItem '{ItemText}' under '{TreeViewName}'.");
    }

    private static string NormalizeHeader(string? header)
    {
        var text = (header ?? string.Empty).Trim();
        return text.StartsWith("[+] ", StringComparison.Ordinal)
            ? text[4..]
            : text;
    }

    private static string DescribeTreeItem(UIElement? element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TreeViewItem item)
            {
                return NormalizeHeader(item.Header);
            }
        }

        return element?.GetType().Name ?? "<null>";
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(UIElement root)
    {
        foreach (var element in EnumerateVisuals(root))
        {
            if (element is TreeViewItem item)
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<UIElement> EnumerateVisuals(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct ClickPointerEvidence(
        string ResolvePath,
        string ClickDownResolvePath,
        string ClickUpResolvePath,
        string ClickDownTarget,
        string ClickUpTarget);
}

internal sealed class DesignerProjectExplorerCollapseItemAndAssertNoStaleDescendantsCommand : IInkkOopsCommand
{
    public DesignerProjectExplorerCollapseItemAndAssertNoStaleDescendantsCommand(string treeViewName, string itemText, string artifactName)
    {
        TreeViewName = string.IsNullOrWhiteSpace(treeViewName)
            ? throw new ArgumentException("TreeView name is required.", nameof(treeViewName))
            : treeViewName;
        ItemText = string.IsNullOrWhiteSpace(itemText)
            ? throw new ArgumentException("Item text is required.", nameof(itemText))
            : itemText;
        ArtifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public string TreeViewName { get; }

    public string ItemText { get; }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"CollapseTreeViewItemAndAssertNoStaleDescendants(Name('{TreeViewName}'), item: {ItemText}, artifact: {ArtifactName})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = await session.QueryOnUiThreadAsync(() => ResolveExpanderPoint(session), cancellationToken).ConfigureAwait(false);
        await session.MovePointerAsync(point, InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f), cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        await session.WaitFramesAsync(4, cancellationToken).ConfigureAwait(false);
        await session.ExecuteOnUiThreadAsync(() => WriteCollapseEvidenceAndAssert(session, point), cancellationToken).ConfigureAwait(false);
    }

    private System.Numerics.Vector2 ResolveExpanderPoint(InkkOopsSession session)
    {
        var targetItem = ResolveItem(ResolveTreeView(session));
        if (!targetItem.IsExpanded)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.NotInteractive,
                $"TreeViewItem '{ItemText}' is already collapsed before the requested collapse click.");
        }

        if (!targetItem.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unrealized,
                $"TreeViewItem '{ItemText}' does not expose render bounds before collapse.");
        }

        return new System.Numerics.Vector2(
            bounds.X + (targetItem.VirtualizedTreeDepth * targetItem.Indent) + targetItem.Padding.Left + 7f,
            bounds.Y + MathF.Min(11f, MathF.Max(1f, bounds.Height / 2f)));
    }

    private void WriteCollapseEvidenceAndAssert(InkkOopsSession session, System.Numerics.Vector2 clickPoint)
    {
        var treeView = ResolveTreeView(session);
        var targetItem = ResolveItem(treeView);
        var targetNode = targetItem.HierarchicalDataItem as DesignerProjectNode
            ?? throw new InkkOopsCommandException(
                InkkOopsFailureCategory.SemanticProviderMissing,
            $"TreeViewItem '{ItemText}' does not expose a DesignerProjectNode hierarchical data item.");
        var staleDescendants = EnumerateTreeItems(treeView)
            .Select(item => CreateNodeSnapshot(item, targetNode.FullPath))
            .Where(snapshot => snapshot.IsDescendantOfTarget)
            .ToArray();
        var allRows = EnumerateTreeItems(treeView)
            .Select(item => CreateNodeSnapshot(item, targetNode.FullPath))
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"artifact_name={ArtifactName}");
        builder.AppendLine($"tree_view={TreeViewName}");
        builder.AppendLine($"target_item={ItemText}");
        builder.AppendLine($"target_header={targetItem.Header}");
        builder.AppendLine($"target_full_path={targetNode.FullPath}");
        builder.AppendLine($"target_is_expanded={targetItem.IsExpanded}");
        builder.AppendLine(FormattableString.Invariant($"click_point=({clickPoint.X:0.###},{clickPoint.Y:0.###})"));
        builder.AppendLine($"stale_descendant_count={staleDescendants.Length}");
        builder.AppendLine("realized_tree_items:");
        for (var i = 0; i < allRows.Length; i++)
        {
            var row = allRows[i];
            builder.AppendLine(FormattableString.Invariant(
                $"{i}: header={row.Header} normalized={row.NormalizedHeader} path={row.FullPath} is_descendant_of_target={row.IsDescendantOfTarget} y={row.Y:0.###} height={row.Height:0.###}"));
        }

        var fileName = ArtifactName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? ArtifactName
            : ArtifactName + ".txt";
        session.Artifacts.BufferTextArtifact(fileName, builder.ToString());

        if (targetItem.IsExpanded)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.NotInteractive,
                $"Expected TreeViewItem '{ItemText}' to be collapsed after the expander click.");
        }

        if (staleDescendants.Length > 0)
        {
            var staleSummary = string.Join(
                ", ",
                staleDescendants.Select(snapshot => $"{snapshot.NormalizedHeader}@{snapshot.Y:0.###}"));
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unrealized,
                $"Collapsed TreeViewItem '{ItemText}' left realized descendant rows in Project Explorer: {staleSummary}.");
        }
    }

    private TreeView ResolveTreeView(InkkOopsSession session)
    {
        var target = new InkkOopsTargetReference(TreeViewName);
        return session.ResolveRequiredTarget(target) as TreeView
            ?? throw new InkkOopsCommandException(
                InkkOopsFailureCategory.SemanticProviderMissing,
                $"Target '{TreeViewName}' is not a TreeView.");
    }

    private TreeViewItem ResolveItem(TreeView treeView)
    {
        foreach (var item in EnumerateTreeItems(treeView))
        {
            if (string.Equals(NormalizeHeader(item.Header), ItemText, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Could not find TreeViewItem '{ItemText}' under '{TreeViewName}'.");
    }

    private static NodeSnapshot CreateNodeSnapshot(TreeViewItem item, string targetPath)
    {
        var fullPath = item.HierarchicalDataItem is DesignerProjectNode node ? node.FullPath : string.Empty;
        item.TryGetRenderBoundsInRootSpace(out var bounds);
        return new NodeSnapshot(
            item.Header,
            NormalizeHeader(item.Header),
            fullPath,
            IsDescendantPath(fullPath, targetPath),
            bounds.Y,
            bounds.Height);
    }

    private static bool IsDescendantPath(string path, string parentPath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parentPath))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        var normalizedParent = parentPath.Replace('\\', '/').TrimEnd('/');
        return normalizedPath.Length > normalizedParent.Length &&
               normalizedPath.StartsWith(normalizedParent + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHeader(string? header)
    {
        var text = (header ?? string.Empty).Trim();
        return text.StartsWith("[+] ", StringComparison.Ordinal)
            ? text[4..]
            : text;
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(UIElement root)
    {
        foreach (var element in EnumerateVisuals(root))
        {
            if (element is TreeViewItem item)
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<UIElement> EnumerateVisuals(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct NodeSnapshot(
        string Header,
        string NormalizedHeader,
        string FullPath,
        bool IsDescendantOfTarget,
        float Y,
        float Height);
}

internal sealed class DesignerProjectExplorerScrollItemToTopCommand : IInkkOopsCommand
{
    private const float NearTopTolerance = 6f;

    public DesignerProjectExplorerScrollItemToTopCommand(string treeViewName, string itemText, string artifactName)
    {
        TreeViewName = string.IsNullOrWhiteSpace(treeViewName)
            ? throw new ArgumentException("TreeView name is required.", nameof(treeViewName))
            : treeViewName;
        ItemText = string.IsNullOrWhiteSpace(itemText)
            ? throw new ArgumentException("Item text is required.", nameof(itemText))
            : itemText;
        ArtifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public string TreeViewName { get; }

    public string ItemText { get; }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"ScrollTreeViewItemToTop(Name('{TreeViewName}'), item: {ItemText}, artifact: {ArtifactName})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ExecuteOnUiThreadAsync(() => ScrollToItemTop(session), cancellationToken).ConfigureAwait(false);
        await session.WaitFramesAsync(6, cancellationToken).ConfigureAwait(false);
        await session.ExecuteOnUiThreadAsync(() => WriteViewportEvidence(session), cancellationToken).ConfigureAwait(false);
    }

    private void ScrollToItemTop(InkkOopsSession session)
    {
        var treeView = ResolveTreeView(session);
        var scrollViewer = ResolveScrollViewer(treeView);
        var targetItem = ResolveItem(treeView);

        if (!targetItem.TryGetRenderBoundsInRootSpace(out var targetBounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unrealized,
                $"TreeViewItem '{ItemText}' does not expose render bounds before scrolling.");
        }

        var viewportBounds = GetViewportBounds(scrollViewer);
        var nextVerticalOffset = scrollViewer.VerticalOffset + targetBounds.Y - viewportBounds.Y;
        scrollViewer.ScrollToVerticalOffset(nextVerticalOffset);
    }

    private void WriteViewportEvidence(InkkOopsSession session)
    {
        var treeView = ResolveTreeView(session);
        var scrollViewer = ResolveScrollViewer(treeView);
        var targetItem = ResolveItem(treeView);
        var viewportBounds = GetViewportBounds(scrollViewer);

        if (!targetItem.TryGetRenderBoundsInRootSpace(out var targetBounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unrealized,
                $"TreeViewItem '{ItemText}' does not expose render bounds after scrolling.");
        }

        var targetTopDelta = targetBounds.Y - viewportBounds.Y;
        var isNearTop = MathF.Abs(targetTopDelta) <= NearTopTolerance;
        if (!isNearTop)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Offscreen,
                $"Expected TreeViewItem '{ItemText}' near the top of the Project Explorer viewport. topDelta={targetTopDelta:0.###}, tolerance={NearTopTolerance:0.###}.");
        }

        var visibleRows = EnumerateTreeItems(treeView)
            .Select(item => CreateVisibleRowSnapshot(item, viewportBounds))
            .Where(snapshot => snapshot != null)
            .Cast<TreeViewRowSnapshot>()
            .OrderBy(snapshot => snapshot.Top)
            .ThenBy(snapshot => snapshot.Header, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"artifact_name={ArtifactName}");
        builder.AppendLine($"tree_view={TreeViewName}");
        builder.AppendLine($"target_item={ItemText}");
        builder.AppendLine($"target_header={targetItem.Header}");
        builder.AppendLine(FormattableString.Invariant($"scroll_viewer_vertical_offset={scrollViewer.VerticalOffset:0.###}"));
        builder.AppendLine(FormattableString.Invariant($"viewport_y={viewportBounds.Y:0.###}"));
        builder.AppendLine(FormattableString.Invariant($"viewport_height={viewportBounds.Height:0.###}"));
        builder.AppendLine(FormattableString.Invariant($"target_y={targetBounds.Y:0.###}"));
        builder.AppendLine(FormattableString.Invariant($"target_height={targetBounds.Height:0.###}"));
        builder.AppendLine(FormattableString.Invariant($"target_top_delta={targetTopDelta:0.###}"));
        builder.AppendLine($"target_is_near_top={isNearTop}");
        builder.AppendLine("visible_tree_items:");
        for (var i = 0; i < visibleRows.Length; i++)
        {
            var row = visibleRows[i];
            builder.AppendLine(FormattableString.Invariant(
                $"{i}: header={row.Header} normalized={row.NormalizedHeader} y={row.Top:0.###} height={row.Height:0.###} top_delta={row.Top - viewportBounds.Y:0.###}"));
        }

        var fileName = ArtifactName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? ArtifactName
            : ArtifactName + ".txt";
        session.Artifacts.BufferTextArtifact(fileName, builder.ToString());
    }

    private TreeView ResolveTreeView(InkkOopsSession session)
    {
        var target = new InkkOopsTargetReference(TreeViewName);
        return session.ResolveRequiredTarget(target) as TreeView
            ?? throw new InkkOopsCommandException(
                InkkOopsFailureCategory.SemanticProviderMissing,
                $"Target '{TreeViewName}' is not a TreeView.");
    }

    private TreeViewItem ResolveItem(TreeView treeView)
    {
        foreach (var item in EnumerateTreeItems(treeView))
        {
            if (string.Equals(NormalizeHeader(item.Header), ItemText, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Could not find TreeViewItem '{ItemText}' under '{TreeViewName}'.");
    }

    private static ScrollViewer ResolveScrollViewer(TreeView treeView)
    {
        foreach (var element in EnumerateVisuals(treeView))
        {
            if (element is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unrealized,
            "Project Explorer TreeView does not expose its ScrollViewer.");
    }

    private static LayoutRect GetViewportBounds(ScrollViewer scrollViewer)
    {
        return scrollViewer.TryGetContentViewportClipRect(out var viewportBounds)
            ? viewportBounds
            : scrollViewer.LayoutSlot;
    }

    private static TreeViewRowSnapshot? CreateVisibleRowSnapshot(TreeViewItem item, LayoutRect viewportBounds)
    {
        if (!item.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            return null;
        }

        var rowHeight = MathF.Min(bounds.Height, 24f);
        var rowBottom = bounds.Y + rowHeight;
        var viewportBottom = viewportBounds.Y + viewportBounds.Height;
        if (rowBottom < viewportBounds.Y - 0.5f || bounds.Y > viewportBottom + 0.5f)
        {
            return null;
        }

        return new TreeViewRowSnapshot(
            item.Header,
            NormalizeHeader(item.Header),
            bounds.Y,
            rowHeight);
    }

    private static string NormalizeHeader(string? header)
    {
        var text = (header ?? string.Empty).Trim();
        return text.StartsWith("[+] ", StringComparison.Ordinal)
            ? text[4..]
            : text;
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(UIElement root)
    {
        foreach (var element in EnumerateVisuals(root))
        {
            if (element is TreeViewItem item)
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<UIElement> EnumerateVisuals(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record TreeViewRowSnapshot(string Header, string NormalizedHeader, float Top, float Height);
}

public sealed class DesignerProjectExplorerHoverAction14TelemetryRuntimeScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-project-explorer-hover-action14-telemetry";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));
        var hoverMotion = InkkOopsPointerMotion.WithTravelFrames(3, stepDistance: 12f);

        var builder = new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable);

        ApplyHoverSweep(builder, hoverMotion, stopBeforeAction14: true);

        return builder
            .DumpTelemetry("before-action14")
            .MovePointerTo("ProjectExplorerTree", InkkOopsPointerAnchor.OffsetBy(80f, 158f), hoverMotion)
            .DumpTelemetry("after-action14-immediate")
            .WaitFrames(1)
            .DumpTelemetry("after-action14-one-frame")
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-action14-idle")
            .Build();
    }

    private static InkkOopsScriptBuilder ApplyHoverSweep(InkkOopsScriptBuilder builder, InkkOopsPointerMotion hoverMotion, bool stopBeforeAction14)
    {
        foreach (var offsetY in stopBeforeAction14 ? new[] { 14f, 38f, 62f, 86f, 110f, 134f } : new[] { 14f, 38f, 62f, 86f, 110f, 134f, 158f, 182f })
        {
            builder.MovePointerTo("ProjectExplorerTree", InkkOopsPointerAnchor.OffsetBy(80f, offsetY), hoverMotion);
        }

        return builder;
    }
}
