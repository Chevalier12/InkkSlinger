using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SubspaceViewport2DInputTests
{
    [Fact]
    public void CheckBoxInsideSubspaceViewport2D_ClickTogglesIsChecked()
    {
        var backup = CaptureApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var host = new Canvas
            {
                Width = 800f,
                Height = 600f
            };

            var renderSurface = new RenderSurface
            {
                Width = 560f,
                Height = 392f,
                Stretch = Stretch.Uniform
            };

            host.AddChild(renderSurface);
            Canvas.SetLeft(renderSurface, 24f);
            Canvas.SetTop(renderSurface, 24f);

            var checkBox = new CheckBox
            {
                Width = 172f,
                Height = 28f,
                Content = "Enable locality",
                IsChecked = true
            };

            var viewportRoot = new Grid();
            viewportRoot.AddChild(checkBox);

            renderSurface.SubspaceViewport2Ds.Add(new SubspaceViewport2D
            {
                X = 286f,
                Y = 52f,
                Width = 220f,
                Height = 188f,
                Content = viewportRoot
            });

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 800, 600);

            var clickPoint = GetCenter(renderSurface, checkBox);
            Assert.True(renderSurface.TryHitTestSubspaceViewport2Ds(clickPoint, out var viewportHit));
            Assert.True(HasAncestorOrSelf(viewportHit, checkBox));

            ClickAt(uiRoot, clickPoint);

            Assert.False(checkBox.IsChecked);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void ActionButtonInsideSubspaceViewport2D_HoverTriggersIsMouseOverAndAnimation()
    {
        var backup = CaptureApplicationResources();
        try
        {
            AnimationManager.Current.ResetForTests();
            AnimationValueSink.ResetTelemetryForTests();
            Freezable.ResetTelemetryForTests();
            Button.ResetTimingForTests();
            DropShadowEffect.ResetTimingForTests();
            TestApplicationResources.LoadDemoAppResources();

            var host = new Canvas
            {
                Width = 800f,
                Height = 600f
            };

            var renderSurface = new RenderSurface
            {
                Width = 560f,
                Height = 392f,
                Stretch = Stretch.Uniform
            };

            host.AddChild(renderSurface);
            Canvas.SetLeft(renderSurface, 24f);
            Canvas.SetTop(renderSurface, 24f);

            var actionButton = new Button
            {
                Content = "Action",
                Width = 96f,
                Margin = new Thickness(0f, 12f, 0f, 0f)
            };

            var viewportRoot = new Border
            {
                Padding = new Thickness(12f),
                Child = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    }
                }
            };

            var viewportGrid = Assert.IsType<Grid>(viewportRoot.Child);
            viewportGrid.AddChild(new TextBlock
            {
                 Text = "Local Viewport A",
                TextWrapping = TextWrapping.Wrap
            });
            var bodyText = new TextBlock
            {
                Text = "This subtree is rendered inside the RenderSurface and laid out in local viewport coordinates.",
                Margin = new Thickness(0f, 8f, 0f, 0f),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(bodyText, 1);
            viewportGrid.AddChild(bodyText);
            Grid.SetRow(actionButton, 2);
            viewportGrid.AddChild(actionButton);

            renderSurface.SubspaceViewport2Ds.Add(new SubspaceViewport2D
            {
                X = 24f,
                Y = 24f,
                Width = 236f,
                Height = 142f,
                Content = viewportRoot
            });

            var uiRoot = new UiRoot(host);
            var viewport = new Viewport(0, 0, 800, 600);
            AdvanceFrame(uiRoot, viewport, 16);
            AdvanceFrame(uiRoot, viewport, 32);

            var hoverPoint = GetCenter(renderSurface, actionButton);

            Assert.False(actionButton.IsMouseOver);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPoint, pointerMoved: true));
            AdvanceFrames(uiRoot, viewport, startMs: 48, frameCount: 8, frameStepMs: 16);

            Assert.True(actionButton.IsMouseOver);
            Assert.True(AnimationManager.Current.GetTelemetrySnapshotForTests().BeginStoryboardCallCount > 0 ||
                        AnimationManager.Current.GetTelemetrySnapshotForTests().ActiveStoryboardCount > 0);
            Assert.True(actionButton.RenderTransform is ScaleTransform scale && scale.ScaleX > 1f);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void ActionButtonInsideSubspaceViewport2D_FastPointerPassStillTriggersHoverAnimation()
    {
        var backup = CaptureApplicationResources();
        try
        {
            AnimationManager.Current.ResetForTests();
            AnimationValueSink.ResetTelemetryForTests();
            Freezable.ResetTelemetryForTests();
            Button.ResetTimingForTests();
            DropShadowEffect.ResetTimingForTests();
            TestApplicationResources.LoadDemoAppResources();

            var host = new Canvas
            {
                Width = 800f,
                Height = 600f
            };

            var renderSurface = new RenderSurface
            {
                Width = 560f,
                Height = 392f,
                Stretch = Stretch.Uniform
            };

            host.AddChild(renderSurface);
            Canvas.SetLeft(renderSurface, 24f);
            Canvas.SetTop(renderSurface, 24f);

            var actionButton = new Button
            {
                Content = "Action",
                Width = 96f,
                Margin = new Thickness(0f, 12f, 0f, 0f)
            };

            var viewportRoot = CreateActionButtonSubspaceViewport2D(actionButton);
            renderSurface.SubspaceViewport2Ds.Add(new SubspaceViewport2D
            {
                X = 24f,
                Y = 24f,
                Width = 236f,
                Height = 142f,
                Content = viewportRoot
            });

            var uiRoot = new UiRoot(host);
            var viewport = new Viewport(0, 0, 800, 600);
            AdvanceFrame(uiRoot, viewport, 16);
            AdvanceFrame(uiRoot, viewport, 32);

            var center = GetCenter(renderSurface, actionButton);
            var start = new Vector2(center.X - 120f, center.Y);
            var end = new Vector2(center.X + 120f, center.Y);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, end, pointerMoved: true));
            AdvanceFrames(uiRoot, viewport, startMs: 48, frameCount: 4, frameStepMs: 16);

            var animationSnapshot = AnimationManager.Current.GetTelemetrySnapshotForTests();
            Assert.True(animationSnapshot.BeginStoryboardCallCount > 0 || animationSnapshot.ActiveStoryboardCount > 0);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void ClickAt(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
    }

    private static Vector2 GetCenter(RenderSurface renderSurface, UIElement element)
    {
        return new Vector2(
            renderSurface.LayoutSlot.X + element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            renderSurface.LayoutSlot.Y + element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return CreatePointerDelta(pointer, pointer, pointerMoved, leftPressed, leftReleased);
    }

    private static InputDelta CreatePointerDelta(
        Vector2 previousPointer,
        Vector2 currentPointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previousPointer),
            Current = new InputSnapshot(default, default, currentPointer),
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static void AdvanceFrame(UiRoot uiRoot, Viewport viewport, double totalMilliseconds)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(totalMilliseconds), TimeSpan.FromMilliseconds(16)),
            viewport);
    }

    private static void AdvanceFrames(UiRoot uiRoot, Viewport viewport, double startMs, int frameCount, double frameStepMs)
    {
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            AdvanceFrame(uiRoot, viewport, startMs + (frameIndex * frameStepMs));
        }
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static Border CreateActionButtonSubspaceViewport2D(Button actionButton)
    {
        var viewportRoot = new Border
        {
            Padding = new Thickness(12f),
            Child = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                }
            }
        };

        var viewportGrid = Assert.IsType<Grid>(viewportRoot.Child);
        viewportGrid.AddChild(new TextBlock
        {
              Text = "Local Viewport A",
            TextWrapping = TextWrapping.Wrap
        });
        var bodyText = new TextBlock
        {
            Text = "This subtree is rendered inside the RenderSurface and laid out in local viewport coordinates.",
            Margin = new Thickness(0f, 8f, 0f, 0f),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(bodyText, 1);
        viewportGrid.AddChild(bodyText);
        Grid.SetRow(actionButton, 2);
        viewportGrid.AddChild(actionButton);
        return viewportRoot;
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private static bool HasAncestorOrSelf(UIElement? element, UIElement expectedAncestor)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, expectedAncestor))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}