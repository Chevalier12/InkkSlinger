using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class VisualTreeHelperOptimizationTests
{
    [Fact]
    public void HitTest_LargeItemsPresenter_CandidateHit_DoesNotUseFallbackProbes()
    {
        var (root, _, items) = CreateItemsPresenterHost(itemCount: 200, itemHeight: 20f);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 480, 5200, 16);

        var targetItem = items[100];
        var probe = GetCenter(targetItem.LayoutSlot);

        VisualTreeHelper.ResetInstrumentationForTests();
        var hit = VisualTreeHelper.HitTest(root, probe);
        var snapshot = VisualTreeHelper.GetInstrumentationSnapshotForTests();

        Assert.Same(targetItem, hit);
        Assert.Equal(0, snapshot.ItemsPresenterNeighborProbes);
        Assert.Equal(0, snapshot.ItemsPresenterFullFallbackScans);
    }

    [Fact]
    public void HitTest_ItemsPresenterNeighborProbe_HitsShiftedItemWithoutDuplicateFullFallback()
    {
        var (root, _, items) = CreateItemsPresenterHost(itemCount: 24, itemHeight: 20f);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 480, 900, 16);

        var shiftedItem = items[10];
        var blockedItem = items[11];
        shiftedItem.SetLayoutSlot(blockedItem.LayoutSlot);
        blockedItem.IsHitTestVisible = false;

        VisualTreeHelper.ResetInstrumentationForTests();
        var probe = GetCenter(blockedItem.LayoutSlot);
        var hit = VisualTreeHelper.HitTest(root, probe);
        var snapshot = VisualTreeHelper.GetInstrumentationSnapshotForTests();

        Assert.Same(shiftedItem, hit);
        Assert.True(snapshot.ItemsPresenterNeighborProbes > 0);
        Assert.Equal(0, snapshot.ItemsPresenterFullFallbackScans);
    }

    [Fact]
    public void HitTest_MonotonicPanelWithChildClips_StillUsesPanelFastPath()
    {
        var stackPanel = new StackPanel
        {
            Width = 240f,
            Height = 520f
        };

        for (var i = 0; i < 20; i++)
        {
            stackPanel.AddChild(new ClippedBorder
            {
                Width = 220f,
                Height = 20f,
                Margin = new Thickness(0f, 0f, 0f, 2f)
            });
        }

        var uiRoot = new UiRoot(stackPanel);
        RunLayout(uiRoot, 260, 560, 16);

        var target = Assert.IsType<ClippedBorder>(stackPanel.Children[12]);
        var probe = GetCenter(target.LayoutSlot);

        VisualTreeHelper.ResetInstrumentationForTests();
        var hit = VisualTreeHelper.HitTest(stackPanel, probe);
        var snapshot = VisualTreeHelper.GetInstrumentationSnapshotForTests();

        Assert.Same(target, hit);
        Assert.True(snapshot.MonotonicPanelFastPathCount > 0);
    }

    [Fact]
    public void HitTest_BuiltInCompositeTraversal_UsesIndexedChildrenWithoutEnumerableFallback()
    {
        var toolBar = new ToolBar
        {
            Width = 320f,
            Height = 240f
        };

        for (var i = 0; i < 6; i++)
        {
            toolBar.Items.Add(new Button
            {
                Width = 90f,
                Content = new Label
                {
                    Content = $"Item {i}"
                }
            });
        }

        var uiRoot = new UiRoot(toolBar);
        RunLayout(uiRoot, 320, 240, 16);

        Assert.True(toolBar.OverflowItemCountForTesting > 0);
        var overflowButton = toolBar.OverflowButtonForTesting;
        Panel.SetZIndex(overflowButton, 10);
        var probe = GetCenter(overflowButton.LayoutSlot);

        VisualTreeHelper.ResetInstrumentationForTests();
        var hit = VisualTreeHelper.HitTest(toolBar, probe);
        var snapshot = VisualTreeHelper.GetInstrumentationSnapshotForTests();

        Assert.Same(overflowButton, FindAncestor<Button>(hit));
        Assert.Equal(0, snapshot.LegacyEnumerableFallbacks);
    }

    private static (Panel Root, ItemsPresenter Presenter, List<Border> Items) CreateItemsPresenterHost(int itemCount, float itemHeight)
    {
        var owner = new ItemsControl();
        var items = new List<Border>(itemCount);
        for (var i = 0; i < itemCount; i++)
        {
            var item = new Border
            {
                Width = 280f,
                Height = itemHeight
            };
            owner.Items.Add(item);
            items.Add(item);
        }

        var presenter = new ItemsPresenter
        {
            Width = 320f,
            Height = MathF.Max(400f, itemCount * (itemHeight + 2f))
        };
        presenter.SetExplicitItemsOwner(owner);

        var root = new Panel
        {
            Width = 360f,
            Height = MathF.Max(420f, itemCount * (itemHeight + 2f))
        };
        root.AddChild(presenter);

        return (root, presenter, items);
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class ClippedBorder : Border
    {
        protected override bool TryGetClipRect(out LayoutRect clipRect)
        {
            clipRect = LayoutSlot;
            return clipRect.Width > 0f && clipRect.Height > 0f;
        }
    }

}
