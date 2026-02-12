using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class AdornerInfrastructureTests
{
    public AdornerInfrastructureTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Decorator_HostsSingleChild_AndArrangesToFinalRect()
    {
        var decorator = new Decorator
        {
            Width = 300f,
            Height = 200f,
            Child = new Border { Width = 80f, Height = 40f }
        };

        decorator.Measure(new Vector2(300f, 200f));
        decorator.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        var child = Assert.IsType<Border>(decorator.Child);
        Assert.Equal(80f, child.LayoutSlot.Width, 3);
        Assert.Equal(40f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void AdornerLayer_AttachDetach_AndGetAdorners_Works()
    {
        var decorator = new AdornerDecorator
        {
            Width = 400f,
            Height = 300f,
            Child = new Canvas { Width = 400f, Height = 300f }
        };

        var canvas = Assert.IsType<Canvas>(decorator.Child);
        var target = new Border { Width = 100f, Height = 60f };
        canvas.AddChild(target);
        Canvas.SetLeft(target, 40f);
        Canvas.SetTop(target, 30f);

        decorator.Measure(new Vector2(400f, 300f));
        decorator.Arrange(new LayoutRect(0f, 0f, 400f, 300f));

        var adorner = new SelectionRectangleAdorner(target);
        Assert.True(AdornerLayer.Attach(target, adorner));
        Assert.Single(decorator.AdornerLayer.GetAdorners(target));

        Assert.True(AdornerLayer.Detach(target, adorner));
        Assert.Empty(decorator.AdornerLayer.GetAdorners(target));
    }

    [Fact]
    public void SelectionAdorner_TracksAdornedElementBounds_OnLayoutChanges()
    {
        var decorator = new AdornerDecorator
        {
            Width = 500f,
            Height = 300f,
            Child = new Canvas { Width = 500f, Height = 300f }
        };

        var canvas = Assert.IsType<Canvas>(decorator.Child);
        var target = new Border { Width = 120f, Height = 80f };
        canvas.AddChild(target);
        Canvas.SetLeft(target, 50f);
        Canvas.SetTop(target, 40f);

        var adorner = new SelectionRectangleAdorner(target);
        Assert.True(AdornerLayer.Attach(target, adorner));

        decorator.Measure(new Vector2(500f, 300f));
        decorator.Arrange(new LayoutRect(0f, 0f, 500f, 300f));
        var initial = adorner.LastAdornerRectForTesting;

        Canvas.SetLeft(target, 180f);
        Canvas.SetTop(target, 120f);
        decorator.Measure(new Vector2(500f, 300f));
        decorator.Arrange(new LayoutRect(0f, 0f, 500f, 300f));
        var moved = adorner.LastAdornerRectForTesting;

        Assert.NotEqual(initial.X, moved.X);
        Assert.NotEqual(initial.Y, moved.Y);
        Assert.Equal(target.LayoutSlot.X, moved.X, 3);
        Assert.Equal(target.LayoutSlot.Y, moved.Y, 3);
    }

    [Fact]
    public void ResizeHandlesAdorner_ExposesInteractiveHandles_AndDragEvents()
    {
        var target = new Border { Width = 80f, Height = 40f };
        target.Measure(new Vector2(80f, 40f));
        target.Arrange(new LayoutRect(20f, 15f, 80f, 40f));

        var adorner = new ResizeHandlesAdorner(target);
        Assert.True(adorner.IsHitTestVisible);
        Assert.Equal(4, adorner.HandlesForTesting.Count);

        ResizeHandleDragEventArgs? observed = null;
        adorner.HandleDragDelta += (_, args) => observed = args;
        adorner.RaiseHandleDragForTesting(ResizeHandlePosition.BottomRight, 5f, -2f);

        Assert.NotNull(observed);
        Assert.Equal(ResizeHandlePosition.BottomRight, observed!.Handle);
        Assert.Equal(5f, observed.HorizontalChange, 3);
        Assert.Equal(-2f, observed.VerticalChange, 3);
    }

    [Fact]
    public void XamlLoader_ParsesDecorator_WithSingleChild()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Decorator x:Name="Host">
                                <Border Width="120" Height="40" />
                              </Decorator>
                            </UserControl>
                            """;

        var codeBehind = new DecoratorCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.Host);
        Assert.NotNull(codeBehind.Host!.Child);
        Assert.IsType<Border>(codeBehind.Host.Child);
    }

    [Fact]
    public void AdornerLayer_HitTesting_PassesThrough_WhenNoInteractiveHandleIsHit()
    {
        var decorator = new AdornerDecorator
        {
            Width = 420f,
            Height = 260f,
            Child = new Canvas { Width = 420f, Height = 260f }
        };

        var canvas = Assert.IsType<Canvas>(decorator.Child);
        var target = new Border { Width = 100f, Height = 70f };
        canvas.AddChild(target);
        Canvas.SetLeft(target, 120f);
        Canvas.SetTop(target, 80f);

        var handles = new ResizeHandlesAdorner(target);
        Assert.True(AdornerLayer.Attach(target, handles));

        decorator.Measure(new Vector2(420f, 260f));
        decorator.Arrange(new LayoutRect(0f, 0f, 420f, 260f));

        var interiorPoint = new Vector2(target.LayoutSlot.X + 20f, target.LayoutSlot.Y + 20f);
        var hit = VisualTreeHelper.HitTest(decorator, interiorPoint);

        Assert.NotNull(hit);
        Assert.NotEqual(typeof(AdornerLayer), hit!.GetType());
        Assert.NotEqual(typeof(ResizeHandlesAdorner), hit.GetType());
    }

    private sealed class DecoratorCodeBehind
    {
        public Decorator? Host { get; set; }
    }
}
