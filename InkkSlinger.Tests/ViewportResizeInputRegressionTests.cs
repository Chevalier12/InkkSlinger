using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ViewportResizeInputRegressionTests
{
    [Fact]
    public void StationaryClick_AfterViewportResize_ShouldTargetElementUnderCurrentPointer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftClicks = 0;
        var rightClicks = 0;

        var leftButton = new Button { Content = "Left" };
        leftButton.Click += (_, _) => leftClicks++;
        Grid.SetColumn(leftButton, 0);
        root.AddChild(leftButton);

        var rightButton = new Button { Content = "Right" };
        rightButton.Click += (_, _) => rightClicks++;
        Grid.SetColumn(rightButton, 1);
        root.AddChild(rightButton);

        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, width: 200, height: 120, elapsedMs: 16);
        var pointer = new Vector2(150f, 60f); // Right column at 200px width.
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        RunLayout(uiRoot, width: 400, height: 120, elapsedMs: 32);
        // Same pointer is now in the left column at 400px width.
        ClickWithoutPointerMove(uiRoot, pointer);

        Assert.True(
            leftClicks == 1 && rightClicks == 0,
            $"Expected stationary click after resize to invoke left button only. leftClicks={leftClicks}, rightClicks={rightClicks}");
    }

    [Fact]
    public void StationaryClick_AfterLayoutMutationWithoutViewportResize_ShouldTargetElementUnderCurrentPointer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });

        var leftClicks = 0;
        var rightClicks = 0;

        var leftButton = new Button { Content = "Left" };
        leftButton.Click += (_, _) => leftClicks++;
        Grid.SetColumn(leftButton, 0);
        root.AddChild(leftButton);

        var rightButton = new Button { Content = "Right" };
        rightButton.Click += (_, _) => rightClicks++;
        Grid.SetColumn(rightButton, 1);
        root.AddChild(rightButton);

        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, width: 200, height: 120, elapsedMs: 16);
        var pointer = new Vector2(150f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        root.ColumnDefinitions[0].Width = new GridLength(160f);
        root.ColumnDefinitions[1].Width = new GridLength(40f);
        RunLayout(uiRoot, width: 200, height: 120, elapsedMs: 32);
        ClickWithoutPointerMove(uiRoot, pointer);

        Assert.True(
            leftClicks == 1 && rightClicks == 0,
            $"Expected stationary click after layout mutation to invoke left button only. leftClicks={leftClicks}, rightClicks={rightClicks}");
    }

    [Fact]
    public void StationaryClick_AfterViewportResize_ShouldSelectListBoxItemUnderCurrentPointer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftList = new ListBox();
        leftList.Items.Add("Left-Item");
        Grid.SetColumn(leftList, 0);
        root.AddChild(leftList);

        var rightList = new ListBox();
        rightList.Items.Add("Right-Item");
        Grid.SetColumn(rightList, 1);
        root.AddChild(rightList);

        var uiRoot = new UiRoot(root);

        var pointer = FindSharedListItemPointer(root, leftList, rightList, uiRoot);

        RunLayout(uiRoot, width: 200, height: 180, elapsedMs: 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        RunLayout(uiRoot, width: 400, height: 180, elapsedMs: 32);
        ClickWithoutPointerMove(uiRoot, pointer);

        Assert.True(
            leftList.SelectedIndex == 0 && rightList.SelectedIndex == -1,
            $"Expected stationary click after resize to select left list item only. leftSelected={leftList.SelectedIndex}, rightSelected={rightList.SelectedIndex}");
    }

    [Fact]
    public void LowCoverageDirtyInvalidation_AfterViewportResize_SuppressesPartialDirtyRedrawDuringSettleWindow()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 800f, 600f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(20f, 20f, 40f, 40f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, width: 800, height: 600, elapsedMs: 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();

        RunLayout(uiRoot, width: 500, height: 400, elapsedMs: 32);
        Assert.True(uiRoot.GetFullRedrawSettleFramesRemainingForTests() > 0);

        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();
        child.RenderTransform = new TranslateTransform { X = 16f };
        uiRoot.SynchronizeRetainedRenderListForTests();
        Assert.NotEmpty(uiRoot.GetDirtyRegionsSnapshotForTests());

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    private static void ClickWithoutPointerMove(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static Vector2 FindSharedListItemPointer(Grid root, ListBox leftList, ListBox rightList, UiRoot uiRoot)
    {
        RunLayout(uiRoot, width: 200, height: 180, elapsedMs: 8);
        var rightHitPoints = new List<Vector2>();
        for (var y = 4f; y < 176f; y += 2f)
        {
            for (var x = 102f; x < 198f; x += 2f)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                var hitList = FindAncestor<ListBox>(FindAncestor<ListBoxItem>(hit));
                if (ReferenceEquals(hitList, rightList))
                {
                    rightHitPoints.Add(point);
                }
            }
        }

        RunLayout(uiRoot, width: 400, height: 180, elapsedMs: 8);
        foreach (var point in rightHitPoints)
        {
            var hit = VisualTreeHelper.HitTest(root, point);
            var hitList = FindAncestor<ListBox>(FindAncestor<ListBoxItem>(hit));
            if (ReferenceEquals(hitList, leftList))
            {
                return point;
            }
        }

        throw new InvalidOperationException("Could not find a stationary pointer position that maps from right list item (200px) to left list item (400px).");
    }

    private static TElement? FindAncestor<TElement>(UIElement? element)
        where TElement : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }
}
