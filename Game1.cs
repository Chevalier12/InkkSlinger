using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Game1 : Game
{
    private static readonly bool EnableRuntimePerfCounters =
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_PERF_COUNTERS"), "0", StringComparison.Ordinal);
    private static readonly bool EnableExperimentalPartialRedraw =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_EXPERIMENTAL_PARTIAL_REDRAW"), "1", StringComparison.Ordinal);
    private const double PerfSampleIntervalSeconds = 1.0;

    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private readonly bool _isWindowDemo;
    private readonly bool _isPaintShellDemo;
    private readonly bool _isCommandingDemo;
    private readonly bool _isTwoScrollViewersDemo;
    private readonly bool _isSimpleScrollViewerDemo;
    private readonly bool _isSimpleStackPanelDemo;
    private readonly bool _isScrollViewerTextBoxDemo;
    private readonly bool _isListBoxDemo;
    private readonly bool _isItemsPresenterDemo;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D? _uiCompositeTarget;
    private Panel _root = null!;
    private UiRoot _uiRoot = null!;
    private MainMenuView? _mainMenuView;
    private WindowDemoView? _windowDemoView;
    private PaintShellView? _paintShellView;
    private CommandingMenuDemoView? _commandingMenuDemoView;
    private TwoScrollViewersView? _twoScrollViewersView;
    private SimpleScrollViewerView? _simpleScrollViewerView;
    private SimpleStackPanelView? _simpleStackPanelView;
    private ScrollViewerTextBoxView? _scrollViewerTextBoxView;
    private SimpleListBoxView? _simpleListBoxView;
    private SimpleItemsPresenterView? _simpleItemsPresenterView;
    private string _baseWindowTitle = "InkkSlinger";
    private string _perfTitleSuffix = string.Empty;
    private double _perfAccumulatedSeconds;
    private int _perfUpdateFrames;
    private int _perfDrawFrames;
    private int _perfLayoutCycles;
    private int _perfLayoutCalls;
    private int _perfLastNeighborProbeTotal;
    private int _perfLastFullFallbackTotal;
    private int _lastViewportWidth;
    private int _lastViewportHeight;
    private bool _hasViewportSnapshot;

    public Game1(
        bool isWindowDemo = false,
        bool isPaintShellDemo = false,
        bool isCommandingDemo = false,
        bool isTwoScrollViewersDemo = false,
        bool isSimpleScrollViewerDemo = false,
        bool isSimpleStackPanelDemo = false,
        bool isScrollViewerTextBoxDemo = false,
        bool isListBoxDemo = false,
        bool isItemsPresenterDemo = false)
    {
        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        _isWindowDemo = isWindowDemo;
        _isPaintShellDemo = isPaintShellDemo;
        _isCommandingDemo = isCommandingDemo;
        _isTwoScrollViewersDemo = isTwoScrollViewersDemo;
        _isSimpleScrollViewerDemo = isSimpleScrollViewerDemo;
        _isSimpleStackPanelDemo = isSimpleStackPanelDemo;
        _isScrollViewerTextBoxDemo = isScrollViewerTextBoxDemo;
        _isListBoxDemo = isListBoxDemo;
        _isItemsPresenterDemo = isItemsPresenterDemo;
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = true;
        _window.AllowUserResizing = true;
        _window.SetClientSize(
            _isWindowDemo ? 1100 : (_isPaintShellDemo ? 1580 : (_isCommandingDemo ? 1500 : (_isTwoScrollViewersDemo ? 1280 : (_isSimpleScrollViewerDemo ? 1024 : (_isSimpleStackPanelDemo ? 1024 : (_isScrollViewerTextBoxDemo ? 1024 : (_isListBoxDemo ? 1024 : (_isItemsPresenterDemo ? 1024 : 1720)))))))),
            _isWindowDemo ? 700 : (_isPaintShellDemo ? 940 : (_isCommandingDemo ? 900 : (_isTwoScrollViewersDemo ? 760 : (_isSimpleScrollViewerDemo ? 720 : (_isSimpleStackPanelDemo ? 720 : (_isScrollViewerTextBoxDemo ? 720 : (_isListBoxDemo ? 720 : (_isItemsPresenterDemo ? 720 : 1080)))))))));
        _window.Title = "InkkSlinger";
    }

    protected override void Initialize()
    {
        _root = new Panel
        {
            Background = new Color(18, 22, 30)
        };

        if (_isWindowDemo)
        {
            _windowDemoView = new WindowDemoView();
            _windowDemoView.CloseRequested += OnWindowDemoCloseRequested;
            _root.AddChild(_windowDemoView);
        }
        else if (_isPaintShellDemo)
        {
            _paintShellView = new PaintShellView();
            _root.AddChild(_paintShellView);
        }
        else if (_isCommandingDemo)
        {
            _commandingMenuDemoView = new CommandingMenuDemoView();
            _root.AddChild(_commandingMenuDemoView);
        }
        else if (_isTwoScrollViewersDemo)
        {
            _twoScrollViewersView = new TwoScrollViewersView();
            _root.AddChild(_twoScrollViewersView);
        }
        else if (_isSimpleScrollViewerDemo)
        {
            _simpleScrollViewerView = new SimpleScrollViewerView();
            _root.AddChild(_simpleScrollViewerView);
        }
        else if (_isSimpleStackPanelDemo)
        {
            _simpleStackPanelView = new SimpleStackPanelView();
            _root.AddChild(_simpleStackPanelView);
        }
        else if (_isScrollViewerTextBoxDemo)
        {
            _scrollViewerTextBoxView = new ScrollViewerTextBoxView();
            _root.AddChild(_scrollViewerTextBoxView);
        }
        else if (_isListBoxDemo)
        {
            _simpleListBoxView = new SimpleListBoxView();
            _root.AddChild(_simpleListBoxView);
        }
        else if (_isItemsPresenterDemo)
        {
            _simpleItemsPresenterView = new SimpleItemsPresenterView();
            _root.AddChild(_simpleItemsPresenterView);
        }
        else
        {
            _mainMenuView = new MainMenuView();
            _root.AddChild(_mainMenuView);
        }
        _window.ClientSizeChanged += OnClientSizeChanged;
        _window.NativeWindow.TextInput += OnTextInput;
        _uiRoot = new UiRoot(_root);
        // Backbuffer contents are not guaranteed to persist across presents on all platforms.
        // Default to a safe middle ground:
        // - allow idle frame skipping
        // - keep partial dirty redraw and subtree caches off unless explicitly requested.
        _uiRoot.UseRetainedRenderList = EnableExperimentalPartialRedraw;
        _uiRoot.UseDirtyRegionRendering = EnableExperimentalPartialRedraw;
        _uiRoot.UseConditionalDrawScheduling = true;
        _uiRoot.UseElementRenderCaches = EnableExperimentalPartialRedraw;
        RefreshWindowTitle();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        try
        {
            var font = Content.Load<SpriteFont>("UIFont");
            _mainMenuView?.SetFont(font);
            _windowDemoView?.SetFont(font);
            _paintShellView?.SetFont(font);
            _commandingMenuDemoView?.SetFont(font);
            _twoScrollViewersView?.SetFont(font);
            _simpleScrollViewerView?.SetFont(font);
            _simpleStackPanelView?.SetFont(font);
            _scrollViewerTextBoxView?.SetFont(font);
            _simpleListBoxView?.SetFont(font);
            _simpleItemsPresenterView?.SetFont(font);
        }
        catch
        {
            // Keep running without font asset so XAML pipeline can still be exercised.
        }
    }

    protected override void Update(GameTime gameTime)
    {
        EnsureBackBufferMatchesClientSize();
        var viewport = EnsureViewportMatchesBackBuffer();
        _lastViewportWidth = viewport.Width;
        _lastViewportHeight = viewport.Height;
        _hasViewportSnapshot = true;
        _uiRoot.Update(gameTime, viewport);
        _perfLayoutCycles++;
        _perfLayoutCalls += 2;
        _perfUpdateFrames++;
        UpdateRuntimePerfCounters(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = EnsureViewportMatchesBackBuffer();
        EnsureUiCompositeTarget(viewport);

        if (_uiRoot.ShouldDrawThisFrame(gameTime, viewport, GraphicsDevice))
        {
            _perfDrawFrames++;
            GraphicsDevice.SetRenderTarget(_uiCompositeTarget);
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _uiRoot.Draw(_spriteBatch, gameTime);
            GraphicsDevice.SetRenderTarget(null);
        }

        if (_uiCompositeTarget != null)
        {
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.Opaque,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone);
            _spriteBatch.Draw(
                _uiCompositeTarget,
                new Rectangle(0, 0, viewport.Width, viewport.Height),
                Color.White);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _window.ClientSizeChanged -= OnClientSizeChanged;
        _window.NativeWindow.TextInput -= OnTextInput;

        if (_windowDemoView != null)
        {
            _windowDemoView.CloseRequested -= OnWindowDemoCloseRequested;
        }

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = null;
        _uiRoot.Shutdown();
        _window.Dispose();
        base.OnExiting(sender, args);
    }

    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        _uiRoot.EnqueueTextInput(args.Character);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_uiRoot != null)
        {
            EnsureBackBufferMatchesClientSize();
        }

        RefreshWindowTitle();
    }

    private void RefreshWindowTitle()
    {
        var size = _window.ClientSize;

        if (_isWindowDemo)
        {
            _baseWindowTitle = $"InkkSlinger Window Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isPaintShellDemo)
        {
            _baseWindowTitle = $"InkkSlinger Paint Shell | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isCommandingDemo)
        {
            _baseWindowTitle = $"InkkSlinger Commanding Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isTwoScrollViewersDemo)
        {
            _baseWindowTitle = $"InkkSlinger Two ScrollViewers Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isSimpleScrollViewerDemo)
        {
            _baseWindowTitle = $"InkkSlinger ScrollViewer Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isSimpleStackPanelDemo)
        {
            _baseWindowTitle = $"InkkSlinger StackPanel Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isScrollViewerTextBoxDemo)
        {
            var backBufferSize = _window.BackBufferSize;
            var viewportText = _hasViewportSnapshot
                ? $"{_lastViewportWidth}x{_lastViewportHeight}"
                : "n/a";
            var textBoxDiagnostics = _scrollViewerTextBoxView?.GetDiagnostics() ?? "TB:n/a";
            _baseWindowTitle = $"InkkSlinger ScrollViewer + TextBox Demo | C:{size.X}x{size.Y} BB:{backBufferSize.X}x{backBufferSize.Y} VP:{viewportText} {textBoxDiagnostics}";
            ApplyWindowTitle();
            return;
        }

        if (_isListBoxDemo)
        {
            _baseWindowTitle = $"InkkSlinger ListBox Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isItemsPresenterDemo)
        {
            _baseWindowTitle = $"InkkSlinger ItemsPresenter Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        _baseWindowTitle = $"InkkSlinger | {size.X}x{size.Y}";
        ApplyWindowTitle();
    }

    private void OnWindowDemoCloseRequested(object? sender, EventArgs e)
    {
        Exit();
    }

    private void UpdateRuntimePerfCounters(GameTime gameTime)
    {
        if (!EnableRuntimePerfCounters)
        {
            return;
        }

        _perfAccumulatedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (_perfAccumulatedSeconds < PerfSampleIntervalSeconds)
        {
            return;
        }

        var stats = VisualTreeHelper.GetItemsPresenterFallbackStatsForTests();
        var neighborDelta = stats.NeighborProbes - _perfLastNeighborProbeTotal;
        var fullFallbackDelta = stats.FullFallbackScans - _perfLastFullFallbackTotal;
        var sampleSeconds = _perfAccumulatedSeconds <= 0d ? PerfSampleIntervalSeconds : _perfAccumulatedSeconds;
        var updatesPerSecond = _perfUpdateFrames / sampleSeconds;
        var drawsPerSecond = _perfDrawFrames / sampleSeconds;
        var layoutCyclesPerSecond = _perfLayoutCycles / sampleSeconds;
        var layoutCallsPerSecond = _perfLayoutCalls / sampleSeconds;
        var neighborPerSecond = neighborDelta / sampleSeconds;
        var fullFallbackPerSecond = fullFallbackDelta / sampleSeconds;
        var inputMetrics = _uiRoot.GetInputMetricsSnapshot();
        var hasInputActivity = inputMetrics.HitTestCount > 0 ||
                               inputMetrics.RoutedEventCount > 0 ||
                               inputMetrics.PointerEventCount > 0 ||
                               inputMetrics.KeyEventCount > 0 ||
                               inputMetrics.TextEventCount > 0;

        if (hasInputActivity)
        {
            Console.WriteLine(
                $"[Perf] U:{updatesPerSecond:0.0}/s D:{drawsPerSecond:0.0}/s " +
                $"LayoutCycles:{layoutCyclesPerSecond:0.0}/s LayoutCalls:{layoutCallsPerSecond:0.0}/s " +
                $"HitTestNeighbor:{neighborPerSecond:0.0}/s HitTestFullFallback:{fullFallbackPerSecond:0.0}/s " +
                $"InputMs:{inputMetrics.LastInputPhaseMilliseconds:0.###} " +
                $"DispatchMs:{inputMetrics.LastInputDispatchMilliseconds:0.###} " +
                $"VisualUpdateMs:{inputMetrics.LastVisualUpdateMilliseconds:0.###} " +
                $"Hit:{inputMetrics.HitTestCount} Route:{inputMetrics.RoutedEventCount} Ptr:{inputMetrics.PointerEventCount} Key:{inputMetrics.KeyEventCount} Txt:{inputMetrics.TextEventCount}");
        }

        _perfTitleSuffix =
            $" | U:{updatesPerSecond:0} D:{drawsPerSecond:0} L:{layoutCyclesPerSecond:0} " +
            $"HN:{neighborPerSecond:0} HF:{fullFallbackPerSecond:0} " +
            $"I:{inputMetrics.LastInputPhaseMilliseconds:0.##} " +
            $"ID:{inputMetrics.LastInputDispatchMilliseconds:0.##} " +
            $"IV:{inputMetrics.LastVisualUpdateMilliseconds:0.##} " +
            $"H:{inputMetrics.HitTestCount:0} R:{inputMetrics.RoutedEventCount:0}";
        ApplyWindowTitle();

        _perfLastNeighborProbeTotal = stats.NeighborProbes;
        _perfLastFullFallbackTotal = stats.FullFallbackScans;
        _perfAccumulatedSeconds = 0d;
        _perfUpdateFrames = 0;
        _perfDrawFrames = 0;
        _perfLayoutCycles = 0;
        _perfLayoutCalls = 0;
    }

    private void ApplyWindowTitle()
    {
        if (!EnableRuntimePerfCounters)
        {
            _window.Title = _baseWindowTitle;
            return;
        }

        _window.Title = $"{_baseWindowTitle}{_perfTitleSuffix}";
    }

    private void EnsureUiCompositeTarget(Viewport viewport)
    {
        if (_uiCompositeTarget != null &&
            !_uiCompositeTarget.IsDisposed &&
            _uiCompositeTarget.Width == viewport.Width &&
            _uiCompositeTarget.Height == viewport.Height)
        {
            return;
        }

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = new RenderTarget2D(
            GraphicsDevice,
            Math.Max(1, viewport.Width),
            Math.Max(1, viewport.Height),
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.DiscardContents);
    }

    private void EnsureBackBufferMatchesClientSize()
    {
        var clientSize = _window.ClientSize;
        if (clientSize.X <= 0 || clientSize.Y <= 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var backBufferSize = _window.BackBufferSize;
        var preferredMatches = clientSize.X == backBufferSize.X && clientSize.Y == backBufferSize.Y;
        var actualMatches = clientSize.X == viewport.Width && clientSize.Y == viewport.Height;
        if (preferredMatches && actualMatches)
        {
            return;
        }

        _window.SetClientSize(clientSize.X, clientSize.Y, applyChanges: true);
    }

    private Viewport EnsureViewportMatchesBackBuffer()
    {
        var presentation = GraphicsDevice.PresentationParameters;
        var targetWidth = Math.Max(1, presentation.BackBufferWidth);
        var targetHeight = Math.Max(1, presentation.BackBufferHeight);
        var viewport = GraphicsDevice.Viewport;

        if (viewport.X != 0 ||
            viewport.Y != 0 ||
            viewport.Width != targetWidth ||
            viewport.Height != targetHeight)
        {
            viewport = new Viewport(0, 0, targetWidth, targetHeight);
            GraphicsDevice.Viewport = viewport;
        }

        return viewport;
    }
}
