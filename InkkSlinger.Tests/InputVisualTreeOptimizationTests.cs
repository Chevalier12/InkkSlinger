using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class InputVisualTreeOptimizationTests
{
    public InputVisualTreeOptimizationTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
    }

    [Fact]
    public void InputManagerValidity_NoPerFrameTreeScan_WhenFocusAndCaptureStable()
    {
        var root = new Panel
        {
            Width = 320f,
            Height = 240f
        };

        var focusable = new Button
        {
            Width = 100f,
            Height = 30f,
            Focusable = true
        };

        root.AddChild(focusable);
        root.Measure(new Vector2(320f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 240f));

        Assert.True(FocusManager.SetFocusedElement(focusable));
        Assert.True(focusable.CaptureMouse());

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        for (var i = 0; i < 24; i++)
        {
            InputManager.UpdateForTesting(root, gameTime, new Vector2(5f, 5f));
        }

        Assert.Equal(0, InputManager.GetTreeMembershipFallbackChecksForTests());
    }

    [Fact]
    public void InputManagerKeyboard_SustainedRepeat_AllocatesLessThanChangedKeyPath()
    {
        var root = new Panel
        {
            Width = 320f,
            Height = 240f
        };
        var focusTarget = new Button
        {
            Width = 100f,
            Height = 30f,
            Focusable = true
        };
        root.AddChild(focusTarget);
        root.Measure(new Vector2(320f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 240f));
        Assert.True(FocusManager.SetFocusedElement(focusTarget));

        var keyDownState = new KeyboardState(Keys.A);
        var initialTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var repeatTime = new GameTime(TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(16));

        InputManager.UpdateForTesting(root, initialTime, new Vector2(5f, 5f), keyboardState: keyDownState);
        for (var i = 0; i < 32; i++)
        {
            InputManager.UpdateForTesting(root, repeatTime, new Vector2(5f, 5f), keyboardState: keyDownState);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++)
        {
            InputManager.UpdateForTesting(root, repeatTime, new Vector2(5f, 5f), keyboardState: keyDownState);
        }

        var unchangedAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        var changingBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++)
        {
            var alternatingState = (i & 1) == 0
                ? new KeyboardState(Keys.A)
                : new KeyboardState(Keys.B);
            InputManager.UpdateForTesting(root, repeatTime, new Vector2(5f, 5f), keyboardState: alternatingState);
        }

        var changingAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - changingBefore;
        Assert.True(unchangedAllocatedBytes < changingAllocatedBytes);
    }

    [Fact]
    public void FocusManager_MoveFocus_StableTree_ReusesCachedCandidates()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 300f
        };
        root.AddChild(new Button { Width = 80f, Height = 28f, Focusable = true });
        root.AddChild(new Button { Width = 80f, Height = 28f, Focusable = true });
        root.AddChild(new Button { Width = 80f, Height = 28f, Focusable = true });
        root.Measure(new Vector2(400f, 300f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 300f));

        FocusManager.MoveFocus(root);
        var statsAfterFirst = FocusManager.GetTraversalCacheStatsForTests();
        FocusManager.MoveFocus(root);
        FocusManager.MoveFocus(root, backwards: true);
        var statsAfterRepeated = FocusManager.GetTraversalCacheStatsForTests();

        Assert.True(statsAfterFirst.Builds >= 1);
        Assert.True(statsAfterRepeated.Hits > statsAfterFirst.Hits);
    }

    [Fact]
    public void VisualTreeHelper_ItemsPresenter_UnevenHeights_UsesBoundedFallbackAndFindsTarget()
    {
        var root = new Panel
        {
            Width = 360f,
            Height = 320f
        };

        var listBox = new ListBox
        {
            Width = 300f,
            Height = 300f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var first = new ListBoxItem { Height = 200f, Content = new Label { Text = "A" } };
        var second = new ListBoxItem { Height = 10f, Content = new Label { Text = "B" } };
        var third = new ListBoxItem { Height = 10f, Content = new Label { Text = "C" } };
        var fourth = new ListBoxItem { Height = 10f, Content = new Label { Text = "D" } };
        var fifth = new ListBoxItem { Height = 10f, Content = new Label { Text = "E" } };
        listBox.Items.Add(first);
        listBox.Items.Add(second);
        listBox.Items.Add(third);
        listBox.Items.Add(fourth);
        listBox.Items.Add(fifth);

        root.AddChild(listBox);
        root.Measure(new Vector2(360f, 320f));
        root.Arrange(new LayoutRect(0f, 0f, 360f, 320f));

        var secondSlot = second.LayoutSlot;
        var probe = new Vector2(secondSlot.X + 2f, secondSlot.Y + MathF.Max(1f, secondSlot.Height * 0.5f));
        VisualTreeHelper.ResetInstrumentationForTests();
        var hit = VisualTreeHelper.HitTest(root, probe);
        var stats = VisualTreeHelper.GetItemsPresenterFallbackStatsForTests();

        Assert.NotNull(hit);
        Assert.True(IsDescendantOf(hit!, second));
        Assert.Equal(0, stats.FullFallbackScans);
    }

    private static bool IsDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
