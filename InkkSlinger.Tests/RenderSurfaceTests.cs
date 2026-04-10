using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RenderSurfaceTests
{
        [Fact]
        public void SubspaceViewport2Ds_EnableManagedMode()
        {
                var renderSurface = new RenderSurface();
                renderSurface.SubspaceViewport2Ds.Add(new SubspaceViewport2D
                {
                        X = 12f,
                        Y = 18f,
                        Width = 120f,
                        Height = 64f,
                        Content = new Grid()
                });

                renderSurface.Arrange(new LayoutRect(0f, 0f, 240f, 160f));

                Assert.True(renderSurface.IsManagedModeActiveForTests);
                Assert.NotNull(renderSurface.GetDisplayedSurfaceForTests());
                Assert.Equal(240, renderSurface.GetDisplayedSurfaceForTests()!.PixelWidth);
                Assert.Equal(160, renderSurface.GetDisplayedSurfaceForTests()!.PixelHeight);
        }

        [Fact]
        public void LoadFromXaml_CreatesRenderSurfaceSubspaceViewport2Ds()
        {
                const string xaml = """
<UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <RenderSurface x:Name="Surface">
        <RenderSurface.SubspaceViewport2Ds>
            <SubspaceViewport2D X="16" Y="24" Width="140" Height="80">
                <Grid x:Name="InnerRoot">
                    <Button x:Name="InnerButton" Content="Inside" />
                </Grid>
            </SubspaceViewport2D>
        </RenderSurface.SubspaceViewport2Ds>
    </RenderSurface>
</UserControl>
""";

                var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
                var surface = Assert.IsType<RenderSurface>(root.FindName("Surface"));
                    var viewport = Assert.Single(surface.SubspaceViewport2Ds);
                    var innerRoot = Assert.IsType<Grid>(viewport.Content);
                var innerButton = Assert.IsType<Button>(innerRoot.FindName("InnerButton"));

                    Assert.Equal(16f, viewport.X);
                    Assert.Equal(24f, viewport.Y);
                    Assert.Equal(140f, viewport.Width);
                    Assert.Equal(80f, viewport.Height);
                Assert.Equal("Inside", Assert.IsType<string>(innerButton.Content));
        }

    [Fact]
    public void Measure_WithPixelSurface_UsesSurfaceSize()
    {
        var renderSurface = new RenderSurface();

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(64f, renderSurface.DesiredSize.X);
        Assert.Equal(32f, renderSurface.DesiredSize.Y);
    }

    [Fact]
    public void Present_SameSurfaceReference_InvalidatesRenderWithoutMeasure()
    {
        var renderSurface = new RenderSurface();
        var surface = ImageSource.FromPixels(64, 32);

        renderSurface.Present(surface);
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 64f, 32f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.Present(surface);

        Assert.False(renderSurface.NeedsMeasure);
        Assert.False(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void RefreshSurface_WithExistingSurface_InvalidatesRenderWithoutMeasure()
    {
        var renderSurface = new RenderSurface();

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 64f, 32f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.RefreshSurface();

        Assert.False(renderSurface.NeedsMeasure);
        Assert.False(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void ManagedMode_WinsOverManualSurfaceWhileActive()
    {
        var renderSurface = new RenderSurface();
        renderSurface.Present(ImageSource.FromPixels(64, 32));

        renderSurface.DrawSurface += OnDrawSurface;
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 200f, 100f));

        var displayedSurface = renderSurface.GetDisplayedSurfaceForTests();

        Assert.NotNull(displayedSurface);
        Assert.NotSame(renderSurface.Surface, displayedSurface);
        Assert.Equal(200, displayedSurface!.PixelWidth);
        Assert.Equal(100, displayedSurface.PixelHeight);

        renderSurface.DrawSurface -= OnDrawSurface;
        return;

        static void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _ = spriteBatch;
            _ = bounds;
        }
    }

    [Fact]
    public void ManualSurface_ResumesWhenManagedModeBecomesInactive()
    {
        var renderSurface = new RenderSurface();
        var manualSurface = ImageSource.FromPixels(96, 48);
        renderSurface.Present(manualSurface);
        renderSurface.DrawSurface += OnDrawSurface;
        renderSurface.Arrange(new LayoutRect(0f, 0f, 180f, 90f));

        renderSurface.DrawSurface -= OnDrawSurface;

        Assert.Same(manualSurface, renderSurface.GetDisplayedSurfaceForTests());
        return;

        static void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _ = spriteBatch;
            _ = bounds;
        }
    }

    [Fact]
    public void DrawSurface_SubscriptionTransitions_InvalidateMeasureAndRender()
    {
        var renderSurface = new RenderSurface();
        RenderSurfaceDrawEventHandler handler = static (spriteBatch, bounds) =>
        {
            _ = spriteBatch;
            _ = bounds;
        };

        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 100f, 50f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.DrawSurface += handler;

        Assert.True(renderSurface.NeedsMeasure);
        Assert.True(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);

        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 100f, 50f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.DrawSurface -= handler;

        Assert.True(renderSurface.NeedsMeasure);
        Assert.True(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void ManagedMode_MeasuresAsZeroIntrinsicSize()
    {
        var renderSurface = new RenderSurface();
        renderSurface.DrawSurface += OnDrawSurface;

        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(Vector2.Zero, renderSurface.DesiredSize);

        renderSurface.DrawSurface -= OnDrawSurface;
        return;

        static void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _ = spriteBatch;
            _ = bounds;
        }
    }

    [Fact]
    public void RefreshSurface_InManagedMode_SchedulesDrawWithoutManualSurface()
    {
        var root = new Panel();
        var renderSurface = new RenderSurface();
        renderSurface.DrawSurface += OnDrawSurface;
        root.AddChild(renderSurface);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.ApplyRenderInvalidationCleanupForTests();
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.RefreshSurface();
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);

        renderSurface.DrawSurface -= OnDrawSurface;
        return;

        static void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _ = spriteBatch;
            _ = bounds;
        }
    }

    [Fact]
    public void ManagedMode_DoesNotRedrawWithoutInvalidation()
    {
        var backend = new FakeRenderSurfaceManagedBackend();
        var previousBackend = RenderSurface.ManagedBackend;
        RenderSurface.ManagedBackend = backend;

        try
        {
            var renderSurface = new RenderSurface();
            var callbackCount = 0;
            renderSurface.DrawSurface += (_, _) => callbackCount++;
            renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            renderSurface.Arrange(new LayoutRect(0f, 0f, 160f, 90f));

            var graphicsDevice = CreateFakeGraphicsDevice();

            Assert.True(renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice));
            Assert.False(renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice));
            Assert.Equal(1, callbackCount);
            Assert.Equal(1, backend.RenderCallCount);

            renderSurface.InvalidateVisual();
            Assert.True(renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice));
            Assert.Equal(2, callbackCount);
            Assert.Equal(2, backend.RenderCallCount);
        }
        finally
        {
            RenderSurface.ManagedBackend = previousBackend;
        }
    }

    [Fact]
    public void SubspaceViewport2DChildRenderInvalidation_SchedulesManagedRedraw()
    {
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
            Content = "Enable locality",
            IsChecked = true
        };

        var viewportStack = new StackPanel();
        viewportStack.AddChild(new TextBlock { Text = "Diagnostics Pod" });
        viewportStack.AddChild(new ProgressBar
        {
            Minimum = 0f,
            Maximum = 100f,
            Value = 68f,
            Width = 172f,
            Height = 16f,
            Margin = new Thickness(0f, 10f, 0f, 0f)
        });
        viewportStack.AddChild(checkBox);
        viewportStack.AddChild(new TextBlock
        {
            Text = "Buttons, text, panels, and other framework visuals can be composed here as part of the local world.",
            Width = 180f,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 12f, 0f, 0f)
        });

        renderSurface.SubspaceViewport2Ds.Add(new SubspaceViewport2D
        {
            X = 286f,
            Y = 52f,
            Width = 220f,
            Height = 188f,
            Content = new Border
            {
                Padding = new Thickness(12f),
                Child = viewportStack
            }
        });

        var uiRoot = new UiRoot(host);
        var viewport = new Viewport(0, 0, 800, 600);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        renderSurface.ClearManagedSurfaceDirtyForTests();
        renderSurface.ClearRenderInvalidationShallow();

        checkBox.IsChecked = false;

        Assert.False(renderSurface.IsManagedSurfaceDirtyForTests());

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);

        Assert.True(renderSurface.IsManagedSurfaceDirtyForTests());
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void ManagedMode_SizeChange_RecreatesManagedResources()
    {
        var backend = new FakeRenderSurfaceManagedBackend();
        var previousBackend = RenderSurface.ManagedBackend;
        RenderSurface.ManagedBackend = backend;

        try
        {
            var renderSurface = new RenderSurface();
            renderSurface.DrawSurface += static (_, _) => { };
            renderSurface.Arrange(new LayoutRect(0f, 0f, 160f, 90f));

            var graphicsDevice = CreateFakeGraphicsDevice();
            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice);

            renderSurface.Arrange(new LayoutRect(0f, 0f, 200f, 120f));
            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice);

            Assert.Equal(2, backend.CreateCallCount);
            Assert.Equal(new Point(200, 120), backend.LastCreatedSize);
        }
        finally
        {
            RenderSurface.ManagedBackend = previousBackend;
        }
    }

    [Fact]
    public void ManagedMode_GraphicsDeviceChange_RecreatesManagedResources()
    {
        var backend = new FakeRenderSurfaceManagedBackend();
        var previousBackend = RenderSurface.ManagedBackend;
        RenderSurface.ManagedBackend = backend;

        try
        {
            var renderSurface = new RenderSurface();
            renderSurface.DrawSurface += static (_, _) => { };
            renderSurface.Arrange(new LayoutRect(0f, 0f, 140f, 80f));

            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(CreateFakeGraphicsDevice());
            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(CreateFakeGraphicsDevice());

            Assert.Equal(2, backend.CreateCallCount);
        }
        finally
        {
            RenderSurface.ManagedBackend = previousBackend;
        }
    }

    [Fact]
    public void DrawSurface_CallbackExecution_ClearsBeforeEachRedraw()
    {
        var backend = new FakeRenderSurfaceManagedBackend();
        var previousBackend = RenderSurface.ManagedBackend;
        RenderSurface.ManagedBackend = backend;

        try
        {
            var renderSurface = new RenderSurface();
            var callbackCount = 0;
            renderSurface.DrawSurface += (_, _) => callbackCount++;
            renderSurface.Arrange(new LayoutRect(0f, 0f, 180f, 120f));

            var graphicsDevice = CreateFakeGraphicsDevice();
            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice);
            renderSurface.InvalidateVisual();
            _ = renderSurface.EnsureManagedSurfaceRenderedForTests(graphicsDevice);

            Assert.Equal(2, callbackCount);
            Assert.Equal(2, backend.ClearCallCount);
            Assert.Equal(new Rectangle(0, 0, 180, 120), backend.LastDrawBounds);
        }
        finally
        {
            RenderSurface.ManagedBackend = previousBackend;
        }
    }

    [Fact]
    public void UiDrawing_StateSnapshot_RestoresClipAndTransformStacksAfterIsolation()
    {
        var graphicsDevice = CreateFakeGraphicsDevice();
        UiDrawing.ConfigureDrawingStateForTests(
            graphicsDevice,
            new[]
            {
                new Rectangle(0, 0, 24, 24),
                new Rectangle(4, 4, 16, 16)
            },
            new[]
            {
                Matrix.CreateTranslation(6f, 2f, 0f),
                Matrix.CreateScale(1.25f, 1.25f, 1f)
            });

        var snapshot = UiDrawing.CaptureDrawingStateForTests(graphicsDevice);

        UiDrawing.ClearDrawingStateForTests(graphicsDevice);
        var cleared = UiDrawing.GetDrawingStateInfoForTests(graphicsDevice);
        Assert.Equal(0, cleared.ClipCount);
        Assert.Equal(0, cleared.TransformCount);

        UiDrawing.RestoreDrawingStateForTests(graphicsDevice, snapshot);
        var restored = UiDrawing.GetDrawingStateInfoForTests(graphicsDevice);
        Assert.Equal(2, restored.ClipCount);
        Assert.Equal(2, restored.TransformCount);

        UiDrawing.ReleaseDeviceResourcesForTests(graphicsDevice);
    }

    [Fact]
    public void UiDrawing_PushLocalState_AppliesAxisAlignedScaleAndTranslationToClipBounds()
    {
        var graphicsDevice = CreateFakeGraphicsDevice();

        try
        {
            UiDrawing.ConfigureDrawingStateForTests(
                graphicsDevice,
                new[]
                {
                    new Rectangle(0, 0, 500, 500)
                },
                Array.Empty<Matrix>());

            var appliedClip = UiDrawing.PushLocalStateForTests(
                graphicsDevice,
                hasTransform: true,
                Matrix.CreateScale(1.5f, 2f, 1f) * Matrix.CreateTranslation(30f, 15f, 0f),
                hasClip: true,
                new LayoutRect(10f, 20f, 100f, 60f));

            Assert.Equal(new Rectangle(45, 55, 150, 120), appliedClip);

            var restoredClip = UiDrawing.PopLocalStateForTests(
                graphicsDevice,
                hasTransform: true,
                hasClip: true);

            Assert.Equal(new Rectangle(0, 0, 500, 500), restoredClip);
        }
        finally
        {
            UiDrawing.ReleaseDeviceResourcesForTests(graphicsDevice);
        }
    }

    [Fact]
    public void UiDrawing_TryGetAxisAligned2DTransformInfo_ReturnsScaleTranslationAndPixelBounds()
    {
        var graphicsDevice = CreateFakeGraphicsDevice();

        try
        {
            UiDrawing.ConfigureDrawingStateForTests(
                graphicsDevice,
                new[]
                {
                    new Rectangle(0, 0, 500, 500)
                },
                new[]
                {
                    Matrix.CreateScale(1.5f, 2f, 1f) * Matrix.CreateTranslation(30f, 15f, 0f)
                });

            var success = UiDrawing.TryGetAxisAligned2DTransformInfo(graphicsDevice, out var scaleX, out var scaleY, out var offsetX, out var offsetY);

            Assert.True(success);
            Assert.Equal(1.5f, scaleX);
            Assert.Equal(2f, scaleY);
            Assert.Equal(30f, offsetX);
            Assert.Equal(15f, offsetY);
            Assert.Equal(new Rectangle(45, 55, 150, 120), UiDrawing.TransformRectToPixelBounds(graphicsDevice, new LayoutRect(10f, 20f, 100f, 60f)));
        }
        finally
        {
            UiDrawing.ReleaseDeviceResourcesForTests(graphicsDevice);
        }
    }

    [Fact]
    public void OverrideOnlyManagedSurface_UsesManagedModeWithoutSubscribers()
    {
        var renderSurface = new OverridingRenderSurface();
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 150f, 90f));

        Assert.True(renderSurface.IsManagedModeActiveForTests);
        Assert.NotNull(renderSurface.GetDisplayedSurfaceForTests());
        Assert.Equal(150, renderSurface.GetDisplayedSurfaceForTests()!.PixelWidth);
        Assert.Equal(90, renderSurface.GetDisplayedSurfaceForTests()!.PixelHeight);
    }

    [Fact]
    public void ApplyRenderInvalidationCleanup_WhenNoDirtyRootsRemain_ClearsStaleRenderFlags()
    {
        var root = new Panel();
        var renderSurface = new RenderSurface();
        root.AddChild(renderSurface);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        uiRoot.ApplyRenderInvalidationCleanupForTests();

        Assert.False(renderSurface.NeedsRender);
    }

    [Fact]
    public void RefreshSurface_AfterCleanupResetsVisualDirtyFlag_SchedulesDraw()
    {
        var root = new Panel();
        var renderSurface = new RenderSurface();
        root.AddChild(renderSurface);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.ApplyRenderInvalidationCleanupForTests();

        renderSurface.RefreshSurface();
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    private static GraphicsDevice CreateFakeGraphicsDevice()
    {
        return (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));
    }

    private sealed class OverridingRenderSurface : RenderSurface
    {
        protected override void OnDrawSurface(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _ = spriteBatch;
            _ = bounds;
        }
    }

    private sealed class FakeRenderSurfaceManagedBackend : IRenderSurfaceManagedBackend
    {
        public int CreateCallCount { get; private set; }

        public int RenderCallCount { get; private set; }

        public int ClearCallCount { get; private set; }

        public Point LastCreatedSize { get; private set; }

        public Rectangle LastDrawBounds { get; private set; }

        public IRenderSurfaceManagedSession Create(GraphicsDevice graphicsDevice, int pixelWidth, int pixelHeight)
        {
            CreateCallCount++;
            LastCreatedSize = new Point(pixelWidth, pixelHeight);
            return new FakeSession(this, graphicsDevice, pixelWidth, pixelHeight);
        }

        private sealed class FakeSession : IRenderSurfaceManagedSession
        {
            private readonly FakeRenderSurfaceManagedBackend _owner;

            public FakeSession(FakeRenderSurfaceManagedBackend owner, GraphicsDevice graphicsDevice, int pixelWidth, int pixelHeight)
            {
                _owner = owner;
                GraphicsDevice = graphicsDevice;
                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                Surface = ImageSource.FromPixels(pixelWidth, pixelHeight);
            }

            public GraphicsDevice GraphicsDevice { get; }

            public int PixelWidth { get; }

            public int PixelHeight { get; }

            public bool IsDisposed { get; private set; }

            public ImageSource Surface { get; }

            public void Render(SpriteBatch? uiSpriteBatch, Color clearColor, Action<SpriteBatch, Rectangle> drawCallback)
            {
                _ = uiSpriteBatch;
                _ = clearColor;
                _owner.ClearCallCount++;
                _owner.RenderCallCount++;
                _owner.LastDrawBounds = new Rectangle(0, 0, PixelWidth, PixelHeight);
                drawCallback(null!, _owner.LastDrawBounds);
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
