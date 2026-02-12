using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class OverlayCompositionTests
{
    public OverlayCompositionTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Popup_DismissOnOutsideClick_Closes_WhenClickingOutside()
    {
        var host = new Panel { Width = 400f, Height = 300f };
        var outside = new TestHitElement { Width = 100f, Height = 50f };
        host.AddChild(outside);

        var popup = new Popup
        {
            Width = 180f,
            Height = 120f,
            Left = 50f,
            Top = 40f,
            DismissOnOutsideClick = true,
            CanClose = false
        };

        popup.Show(host);
        host.Measure(new Vector2(400f, 300f));
        host.Arrange(new LayoutRect(0f, 0f, 400f, 300f));

        outside.FireLeftDown(new Vector2(10f, 10f));

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Popup_PlacementModeBottom_UsesTargetAndClampsToHost()
    {
        var host = new Panel { Width = 300f, Height = 180f };
        var anchor = new Border { Width = 60f, Height = 30f, Margin = new Thickness(250f, 160f, 0f, 0f) };
        host.AddChild(anchor);

        var popup = new Popup
        {
            Width = 120f,
            Height = 60f,
            PlacementMode = PopupPlacementMode.Bottom,
            PlacementTarget = anchor,
            DismissOnOutsideClick = true,
            CanClose = false
        };

        popup.Show(host);
        host.Measure(new Vector2(300f, 180f));
        host.Arrange(new LayoutRect(0f, 0f, 300f, 180f));

        Assert.InRange(popup.Left, 0f, 180f);
        Assert.InRange(popup.Top, 0f, 120f);
        Assert.InRange(MathF.Abs(popup.Margin.Left - popup.Left), 0f, 0.01f);
        Assert.InRange(MathF.Abs(popup.Margin.Top - popup.Top), 0f, 0.01f);
    }

    [Fact]
    public void Popup_EscapeDismisses_FromHostPreviewEvenWhenFocusElsewhere()
    {
        var host = new Panel { Width = 320f, Height = 220f };
        var focusable = new TestFocusableElement { Width = 50f, Height = 30f };
        host.AddChild(focusable);

        var popup = new Popup
        {
            Width = 120f,
            Height = 80f,
            DismissOnOutsideClick = true,
            CanClose = false
        };

        popup.Show(host);
        Assert.True(FocusManager.SetFocusedElement(focusable));

        focusable.FireKeyDown(Keys.Escape);

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void ToolTip_ReusesPopupPrimitive_WithBottomPlacement_AndOutsideDismiss()
    {
        var host = new Panel { Width = 360f, Height = 220f };
        var target = new TestHitElement { Width = 80f, Height = 24f, Margin = new Thickness(40f, 60f, 0f, 0f) };
        var outside = new TestHitElement { Width = 40f, Height = 20f, Margin = new Thickness(0f, 0f, 0f, 0f) };
        host.AddChild(target);
        host.AddChild(outside);

        var tip = new ToolTip
        {
            Width = 110f,
            Height = 42f,
            Content = new Label { Text = "Tip" }
        };

        tip.ShowFor(host, target, horizontalOffset: 4f, verticalOffset: 8f);
        host.Measure(new Vector2(360f, 220f));
        host.Arrange(new LayoutRect(0f, 0f, 360f, 220f));

        Assert.True(tip.IsOpen);
        Assert.Equal(PopupPlacementMode.Bottom, tip.PlacementMode);

        outside.FireLeftDown(new Vector2(2f, 2f));
        Assert.False(tip.IsOpen);
    }

    [Fact]
    public void Popup_BottomPlacement_UsesHostLocalCoordinates_WhenHostIsOffset()
    {
        var root = new Panel { Width = 900f, Height = 600f };
        var host = new Canvas { Width = 500f, Height = 320f, Margin = new Thickness(180f, 140f, 0f, 0f) };
        root.AddChild(host);

        var anchor = new Border { Width = 80f, Height = 24f };
        Canvas.SetLeft(anchor, 210f);
        Canvas.SetTop(anchor, 70f);
        host.AddChild(anchor);

        var popup = new Popup
        {
            Width = 120f,
            Height = 80f,
            PlacementMode = PopupPlacementMode.Bottom,
            PlacementTarget = anchor,
            CanClose = false
        };

        popup.Show(host);
        root.Measure(new Vector2(900f, 600f));
        root.Arrange(new LayoutRect(0f, 0f, 900f, 600f));
        root.Measure(new Vector2(900f, 600f));
        root.Arrange(new LayoutRect(0f, 0f, 900f, 600f));

        Assert.True(MathF.Abs(popup.Left - 210f) <= 0.01f);
        Assert.True(MathF.Abs(popup.Top - 94f) <= 0.01f);
        Assert.True(MathF.Abs(Canvas.GetLeft(popup) - 210f) <= 0.01f);
        Assert.True(MathF.Abs(Canvas.GetTop(popup) - 94f) <= 0.01f);
        Assert.True(MathF.Abs(popup.Margin.Left) <= 0.01f);
        Assert.True(MathF.Abs(popup.Margin.Top) <= 0.01f);
    }

    private sealed class TestHitElement : Border
    {
        public void FireLeftDown(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class TestFocusableElement : Border
    {
        public TestFocusableElement()
        {
            Focusable = true;
        }

        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }
    }
}
