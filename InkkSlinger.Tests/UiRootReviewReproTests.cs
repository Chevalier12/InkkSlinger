using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UiRootReviewReproTests
{
    [Fact]
    public void RetainedRendering_ReenabledAfterDirtyWorkWasSkipped_ForcesFullRebuild()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.UseRetainedRenderList = false;
        child.InvalidateVisual();

        uiRoot.UseRetainedRenderList = true;

        Assert.True(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.True(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void CapturedButtonRemovedBeforePointerUp_DoesNotInvokeDetachedButton()
    {
        var root = new Panel();
        var button = new Button
        {
            Width = 80f,
            Height = 32f,
            Content = "Remove"
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        root.AddChild(button);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        var pointer = new Vector2(20f, 16f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        Assert.True(root.RemoveChild(button));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));

        Assert.Equal(0, clicks);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void FocusedTextBoxRemovedBeforeTextInput_DoesNotReceiveText()
    {
        var root = new Panel();
        var textBox = new TextBox
        {
            Width = 120f,
            Height = 32f
        };
        root.AddChild(textBox);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        var pointer = new Vector2(20f, 16f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
        Assert.True(textBox.IsFocused);

        Assert.True(root.RemoveChild(textBox));
        uiRoot.RunInputDeltaForTests(CreateTextInputDelta('x'));

        Assert.Equal(string.Empty, textBox.Text);
        Assert.False(textBox.IsFocused);
        Assert.Null(FocusManager.GetFocusedElement());
    }

    [Fact]
    public void DirectDescendantArrangeInvalidation_RunsLayoutWhenRootFlagsAreClean()
    {
        var root = new Panel();
        var child = new Border
        {
            Width = 80f,
            Height = 32f
        };
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        uiRoot.CompleteDrawStateForTests();

        child.InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
        RunLayout(uiRoot, elapsedMs: 32);

        Assert.True(uiRoot.LayoutPasses > 0);
        Assert.False(child.NeedsArrange);
    }

    [Fact]
    public void DescendantArrangeInvalidation_WithStableParentRects_DoesNotArrangeUnrelatedSiblings()
    {
        var root = new Panel();
        var branch = new Panel();
        var leaf = new Border
        {
            Width = 40f,
            Height = 20f
        };
        var unrelatedSibling = new ArrangeCountingPanel
        {
            Width = 40f,
            Height = 20f
        };

        branch.AddChild(leaf);
        root.AddChild(branch);
        root.AddChild(unrelatedSibling);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        uiRoot.CompleteDrawStateForTests();
        var siblingArrangeCount = unrelatedSibling.ArrangeOverrideCallCount;

        leaf.InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
        RunLayout(uiRoot, elapsedMs: 32);

        Assert.False(leaf.NeedsArrange);
        Assert.Equal(siblingArrangeCount, unrelatedSibling.ArrangeOverrideCallCount);
    }

    [Fact]
    public void DescendantArrangeInvalidation_DoesNotTelemetryTraverseUnrelatedCleanSubtrees()
    {
        var root = new Panel();
        var branch = new Panel();
        var leaf = new Border
        {
            Width = 40f,
            Height = 20f
        };
        var unrelatedSibling = new VisualChildrenCountingPanel
        {
            Width = 40f,
            Height = 20f
        };

        for (var i = 0; i < 200; i++)
        {
            unrelatedSibling.AddChild(new Border
            {
                Width = 1f,
                Height = 1f
            });
        }

        branch.AddChild(leaf);
        root.AddChild(branch);
        root.AddChild(unrelatedSibling);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        uiRoot.CompleteDrawStateForTests();
        unrelatedSibling.GetVisualChildrenCallCount = 0;

        leaf.InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
        RunLayout(uiRoot, elapsedMs: 32);

        Assert.False(leaf.NeedsArrange);
        Assert.True(
            unrelatedSibling.GetVisualChildrenCallCount <= 1,
            $"A clean unrelated subtree may be touched by normal parent arrange, but UiRoot must not repeatedly walk it for layout telemetry or invalid-layout scans. " +
            $"getVisualChildrenCallCount={unrelatedSibling.GetVisualChildrenCallCount}.");
    }

    [Fact]
    public void CachedDescendantMeasureReuse_ClearsInvalidationAndDoesNotArrangeUnrelatedSiblings()
    {
        var root = new Panel();
        var branch = new Panel();
        var leaf = new Border
        {
            Width = 40f,
            Height = 20f
        };
        var unrelatedSibling = new ArrangeCountingPanel
        {
            Width = 40f,
            Height = 20f
        };

        branch.AddChild(leaf);
        root.AddChild(branch);
        root.AddChild(unrelatedSibling);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        uiRoot.CompleteDrawStateForTests();
        var siblingArrangeCount = unrelatedSibling.ArrangeOverrideCallCount;

        leaf.InvalidateMeasure();
        RunLayout(uiRoot, elapsedMs: 32);

        Assert.False(leaf.NeedsMeasure);
        Assert.False(leaf.NeedsArrange);
        Assert.Equal(siblingArrangeCount, unrelatedSibling.ArrangeOverrideCallCount);
    }

    private static void RunLayout(UiRoot uiRoot, int elapsedMs = 16)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, 240, 120));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreateTextInputDelta(char character)
    {
        return new InputDelta
        {
            Previous = default,
            Current = default,
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char> { character },
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private sealed class ArrangeCountingPanel : Panel
    {
        public int ArrangeOverrideCallCount { get; private set; }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCallCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class VisualChildrenCountingPanel : Panel
    {
        public int GetVisualChildrenCallCount { get; set; }

        public override IEnumerable<UIElement> GetVisualChildren()
        {
            GetVisualChildrenCallCount++;
            return base.GetVisualChildren();
        }
    }
}
