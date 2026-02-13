using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public class ScrollViewerHoverPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ScrollViewerHoverPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
    }

    [Fact]
    public void InputManager_UnchangedPointer_DoesNotDispatchMouseMove()
    {
        var root = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var tracker = new TrackingElement(300f, 200f);
        root.AddChild(tracker);
        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        InputManager.ResetForTests();

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        var moveCountAfterFirst = tracker.MouseMoveCount;

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));

        Assert.Equal(moveCountAfterFirst, tracker.MouseMoveCount);
    }

    [Fact]
    public void InputManager_HoverTransition_RaisesEnterAndLeave_WhenPointerCrossesItems()
    {
        var root = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var left = new TrackingElement(180f, 200f);
        var right = new TrackingElement(180f, 200f);
        Canvas.SetLeft(right, 200f);
        root.AddChild(left);
        root.AddChild(right);
        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        InputManager.ResetForTests();

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        InputManager.UpdateForTesting(root, gameTime, new Vector2(220f, 20f));

        Assert.Equal(1, left.MouseEnterCount);
        Assert.Equal(1, left.MouseLeaveCount);
        Assert.Equal(1, right.MouseEnterCount);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void InputUpdate_Throughput_NoList_Vs_ListInScrollViewer_MedianRatio()
    {
        var noListRoot = BuildNoListScenario();
        var listRoot = BuildListScenario();
        var probe = new Vector2(24f, 24f);
        const int iterations = 40_000;
        const int runs = 5;

        var noListRuns = new double[runs];
        var listRuns = new double[runs];
        for (var i = 0; i < runs; i++)
        {
            noListRuns[i] = MeasureInputUpdate(noListRoot, probe, iterations);
            listRuns[i] = MeasureInputUpdate(listRoot, probe, iterations);
        }

        var noListMedian = Median(noListRuns);
        var listMedian = Median(listRuns);
        var ratio = noListMedian <= 0.0001d ? 0d : listMedian / noListMedian;
        var line =
            $"InputUpdate no-list-vs-list | Iterations={iterations} | Runs={runs} | NoListMs[{string.Join(", ", noListRuns.Select(v => v.ToString("0.###")))}] | ListMs[{string.Join(", ", listRuns.Select(v => v.ToString("0.###")))}] | MedianNoList={noListMedian:0.###} | MedianList={listMedian:0.###} | Ratio={ratio:0.###}x";
        _output.WriteLine(line);
        Console.WriteLine(line);

        Assert.True(noListMedian > 0d);
        Assert.True(listMedian > 0d);
        Assert.True(ratio <= 1.25d);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void InputUpdate_Throughput_MovingPointer_NoList_Vs_ListInScrollViewer_MedianRatio()
    {
        var result = MeasureMovingPointerComparison(iterations: 20_000, runs: 7);
        var ratio = result.NoListMedian <= 0.0001d ? 0d : result.ListMedian / result.NoListMedian;
        var line =
            $"InputUpdate moving-pointer no-list-vs-list | Iterations={result.Iterations} | Runs={result.Runs} | NoListMs[{string.Join(", ", result.NoListRuns.Select(v => v.ToString("0.###")))}] | ListMs[{string.Join(", ", result.ListRuns.Select(v => v.ToString("0.###")))}] | MedianNoList={result.NoListMedian:0.###} | MedianList={result.ListMedian:0.###} | Ratio={ratio:0.###}x | Reuse(NoList)={result.NoListReuseSuccesses}/{result.NoListReuseAttempts} | Reuse(List)={result.ListReuseSuccesses}/{result.ListReuseAttempts}";
        _output.WriteLine(line);
        Console.WriteLine(line);

        Assert.True(result.NoListMedian > 0d);
        Assert.True(result.ListMedian > 0d);
        Assert.True(ratio <= 1.5d);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void InputUpdate_Throughput_MovingPointer_NoList_Vs_ListInScrollViewer_Diagnostic()
    {
        var result = MeasureMovingPointerComparison(iterations: 20_000, runs: 7);
        var ratio = result.NoListMedian <= 0.0001d ? 0d : result.ListMedian / result.NoListMedian;
        var line =
            $"InputUpdate moving-pointer no-list-vs-list | Iterations={result.Iterations} | Runs={result.Runs} | NoListMs[{string.Join(", ", result.NoListRuns.Select(v => v.ToString("0.###")))}] | ListMs[{string.Join(", ", result.ListRuns.Select(v => v.ToString("0.###")))}] | MedianNoList={result.NoListMedian:0.###} | MedianList={result.ListMedian:0.###} | Ratio={ratio:0.###}x | Reuse(NoList)={result.NoListReuseSuccesses}/{result.NoListReuseAttempts} | Reuse(List)={result.ListReuseSuccesses}/{result.ListReuseAttempts}";
        _output.WriteLine(line);
        Console.WriteLine(line);

        Assert.True(result.NoListMedian > 0d);
        Assert.True(result.ListMedian > 0d);
    }

    [Fact]
    public void InputManager_MovingWithinSameListItem_DoesNotRaiseExtraEnterLeave()
    {
        var (root, first, _, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = new Vector2(p1.X + 1f, p1.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);
        InputManager.UpdateForTesting(root, gameTime, p1);

        Assert.Equal(1, first.MouseEnterCount);
        Assert.Equal(0, first.MouseLeaveCount);
    }

    [Fact]
    public void InputManager_MovingAcrossAdjacentListItems_RaisesSingleLeaveAndEnter()
    {
        var (root, first, second, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = ProbePoint(second, 6f, 6f);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);

        Assert.Equal(1, first.MouseEnterCount);
        Assert.Equal(1, first.MouseLeaveCount);
        Assert.Equal(1, second.MouseEnterCount);
        Assert.Equal(0, second.MouseLeaveCount);
    }

    [Fact]
    public void InputManager_MovingWithinSameListItem_StillDispatchesMouseMove()
    {
        var (root, first, _, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = new Vector2(p1.X + 1f, p1.Y);
        var p3 = new Vector2(p2.X + 1f, p2.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        var initialMoveCount = first.MouseMoveCount;

        InputManager.UpdateForTesting(root, gameTime, p2);
        InputManager.UpdateForTesting(root, gameTime, p3);

        Assert.Equal(initialMoveCount + 2, first.MouseMoveCount);
    }

    private (
        double[] NoListRuns,
        double[] ListRuns,
        double NoListMedian,
        double ListMedian,
        int Iterations,
        int Runs,
        int NoListReuseAttempts,
        int NoListReuseSuccesses,
        int ListReuseAttempts,
        int ListReuseSuccesses)
        MeasureMovingPointerComparison(int iterations, int runs)
    {
        var noListRoot = BuildNoListScenario();
        var listRoot = BuildListScenario();
        var noListPair = ResolveMovingProbePair(noListRoot, typeof(Label));
        var listPair = ResolveMovingProbePair(listRoot, typeof(ListBoxItem));

        var noListRuns = new double[runs];
        var listRuns = new double[runs];
        var noListReuseAttempts = 0;
        var noListReuseSuccesses = 0;
        var listReuseAttempts = 0;
        var listReuseSuccesses = 0;
        for (var i = 0; i < runs; i++)
        {
            var noListResult = MeasureInputUpdateMovingPointer(noListRoot, noListPair.P1, noListPair.P2, iterations);
            noListRuns[i] = noListResult.ElapsedMs;
            noListReuseAttempts += noListResult.ReuseAttempts;
            noListReuseSuccesses += noListResult.ReuseSuccesses;

            var listResult = MeasureInputUpdateMovingPointer(listRoot, listPair.P1, listPair.P2, iterations);
            listRuns[i] = listResult.ElapsedMs;
            listReuseAttempts += listResult.ReuseAttempts;
            listReuseSuccesses += listResult.ReuseSuccesses;
        }

        var noListMedian = Median(noListRuns);
        var listMedian = Median(listRuns);
        return (
            noListRuns,
            listRuns,
            noListMedian,
            listMedian,
            iterations,
            runs,
            noListReuseAttempts,
            noListReuseSuccesses,
            listReuseAttempts,
            listReuseSuccesses);
    }

    private static double MeasureInputUpdate(UIElement root, Vector2 point, int iterations)
    {
        InputManager.ResetForTests();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        for (var i = 0; i < 64; i++)
        {
            InputManager.UpdateForTesting(root, gameTime, point);
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            InputManager.UpdateForTesting(root, gameTime, point);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static (double ElapsedMs, int ReuseAttempts, int ReuseSuccesses) MeasureInputUpdateMovingPointer(
        UIElement root,
        Vector2 p1,
        Vector2 p2,
        int iterations)
    {
        InputManager.ResetForTests();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

        for (var i = 0; i < 64; i++)
        {
            InputManager.UpdateForTesting(root, gameTime, (i & 1) == 0 ? p1 : p2);
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            InputManager.UpdateForTesting(root, gameTime, (i & 1) == 0 ? p1 : p2);
        }

        stopwatch.Stop();
        var reuseStats = InputManager.GetHoverReuseStatsForTests();
        return (stopwatch.Elapsed.TotalMilliseconds, reuseStats.Attempts, reuseStats.Successes);
    }

    private static double Median(double[] values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        return ordered.Length % 2 == 0
            ? (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2d
            : ordered[ordered.Length / 2];
    }

    private static Panel BuildNoListScenario()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var viewer = new ScrollViewer
        {
            Width = 300f,
            Height = 180f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel();
        stack.AddChild(new Label { Text = "Label 1" });
        stack.AddChild(new Label { Text = "Label 2" });
        stack.AddChild(new Label { Text = "Label 3" });

        viewer.Content = stack;
        root.AddChild(viewer);

        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));
        return root;
    }

    private static (Vector2 P1, Vector2 P2) ResolveMovingProbePair(UIElement root, Type preferredType)
    {
        var target = FindFirstByType(root, preferredType) as FrameworkElement;
        if (target == null)
        {
            return (new Vector2(24f, 24f), new Vector2(25f, 24f));
        }

        var p1 = ProbePoint(target, 6f, 6f);
        var p2 = new Vector2(
            MathF.Min(target.LayoutSlot.X + target.LayoutSlot.Width - 1f, p1.X + 1f),
            p1.Y);
        if (MathF.Abs(p2.X - p1.X) < 0.5f)
        {
            p2 = new Vector2(p1.X, MathF.Min(target.LayoutSlot.Y + target.LayoutSlot.Height - 1f, p1.Y + 1f));
        }

        return (p1, p2);
    }

    private static UIElement? FindFirstByType(UIElement root, Type targetType)
    {
        if (targetType.IsInstanceOfType(root))
        {
            return root;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstByType(child, targetType);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Panel BuildListScenario()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var viewer = new ScrollViewer
        {
            Width = 300f,
            Height = 180f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var listBox = new ListBox
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        listBox.Items.Add(new ListBoxItem { Content = new Label { Text = "Label A" } });
        listBox.Items.Add(new ListBoxItem { Content = new Label { Text = "Label B" } });
        listBox.Items.Add(new ListBoxItem { Content = new Label { Text = "Label C" } });

        viewer.Content = listBox;
        root.AddChild(viewer);

        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));
        return root;
    }

    private static (Panel Root, TrackingListBoxItem First, TrackingListBoxItem Second, TrackingListBoxItem Third)
        BuildTrackingListScenario()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var viewer = new ScrollViewer
        {
            Width = 300f,
            Height = 180f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var listBox = new ListBox
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var first = new TrackingListBoxItem { Content = new Label { Text = "Label A" } };
        var second = new TrackingListBoxItem { Content = new Label { Text = "Label B" } };
        var third = new TrackingListBoxItem { Content = new Label { Text = "Label C" } };
        listBox.Items.Add(first);
        listBox.Items.Add(second);
        listBox.Items.Add(third);

        viewer.Content = listBox;
        root.AddChild(viewer);

        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));
        return (root, first, second, third);
    }

    private static Vector2 ProbePoint(FrameworkElement element, float dx, float dy)
    {
        var slot = element.LayoutSlot;
        var x = MathF.Max(slot.X + 1f, MathF.Min(slot.X + slot.Width - 1f, slot.X + dx));
        var y = MathF.Max(slot.Y + 1f, MathF.Min(slot.Y + slot.Height - 1f, slot.Y + dy));
        return new Vector2(x, y);
    }

    private sealed class TrackingElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public TrackingElement(float width, float height)
        {
            _size = new Vector2(width, height);
            Width = width;
            Height = height;
        }

        public int MouseMoveCount { get; private set; }

        public int MouseEnterCount { get; private set; }

        public int MouseLeaveCount { get; private set; }

        protected override void OnMouseMove(RoutedMouseEventArgs args)
        {
            base.OnMouseMove(args);
            MouseMoveCount++;
        }

        protected override void OnMouseEnter(RoutedMouseEventArgs args)
        {
            base.OnMouseEnter(args);
            MouseEnterCount++;
        }

        protected override void OnMouseLeave(RoutedMouseEventArgs args)
        {
            base.OnMouseLeave(args);
            MouseLeaveCount++;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }

    private sealed class TrackingListBoxItem : ListBoxItem
    {
        public int MouseMoveCount { get; private set; }

        public int MouseEnterCount { get; private set; }

        public int MouseLeaveCount { get; private set; }

        protected override void OnMouseMove(RoutedMouseEventArgs args)
        {
            base.OnMouseMove(args);
            MouseMoveCount++;
        }

        protected override void OnMouseEnter(RoutedMouseEventArgs args)
        {
            base.OnMouseEnter(args);
            MouseEnterCount++;
        }

        protected override void OnMouseLeave(RoutedMouseEventArgs args)
        {
            base.OnMouseLeave(args);
            MouseLeaveCount++;
        }
    }
}
