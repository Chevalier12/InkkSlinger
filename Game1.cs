using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Game1 : Game
{
    private static readonly bool EnableExperimentalPartialRedraw =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_EXPERIMENTAL_PARTIAL_REDRAW"), "1", StringComparison.Ordinal);

    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private readonly bool _isWindowDemo;
    private readonly bool _isPaintShellDemo;
    private readonly bool _isDarkDashboardDemo;
    private readonly bool _isCommandingDemo;
    private readonly bool _isThreeScrollViewersDemo;
    private readonly bool _isTwoScrollViewersDemo;
    private readonly bool _isSimpleScrollViewerDemo;
    private readonly bool _isSimpleStackPanelDemo;
    private readonly bool _isScrollViewerTextBoxDemo;
    private readonly bool _isListBoxDemo;
    private readonly bool _isItemsPresenterDemo;
    private readonly bool _isVirtualizedStackPanelDemo;
    private readonly bool _isScrollViewerEdgeCasesDemo;
    private readonly bool _isPasswordBoxDemo;
    private readonly bool _isMenuParityDemo;
    private readonly bool _isBindingParityGap5Demo;
    private readonly bool _isRichTextBoxDemo;
    private readonly bool _isRichTextDiagnosticsLabDemo;
    private readonly bool _isWindowPopupParityLabDemo;
    private readonly bool _isContextMenuParityLabDemo;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D? _uiCompositeTarget;
    private Panel _root = null!;
    private UiRoot _uiRoot = null!;
    private MainMenuView? _mainMenuView;
    private WindowDemoView? _windowDemoView;
    private PaintShellView? _paintShellView;
    private DarkDashboardView? _darkDashboardView;
    private CommandingMenuDemoView? _commandingMenuDemoView;
    private ThreeScrollViewersView? _threeScrollViewersView;
    private TwoScrollViewersView? _twoScrollViewersView;
    private SimpleScrollViewerView? _simpleScrollViewerView;
    private SimpleStackPanelView? _simpleStackPanelView;
    private ScrollViewerTextBoxView? _scrollViewerTextBoxView;
    private SimpleListBoxView? _simpleListBoxView;
    private SimpleItemsPresenterView? _simpleItemsPresenterView;
    private VirtualizedStackPanelView? _virtualizedStackPanelView;
    private ScrollViewerEdgeCasesView? _scrollViewerEdgeCasesView;
    private PasswordBoxDemoView? _passwordBoxDemoView;
    private MenuParityLabView? _menuParityLabView;
    private BindingParityGap5DemoView? _bindingParityGap5DemoView;
    private RichTextBoxDemoView? _richTextBoxDemoView;
    private RichTextDiagnosticsLabView? _richTextDiagnosticsLabView;
    private WindowPopupParityLabView? _windowPopupParityLabView;
    private ContextMenuParityLabView? _contextMenuParityLabView;
    private string _baseWindowTitle = "InkkSlinger";
    private int _lastViewportWidth;
    private int _lastViewportHeight;
    private bool _hasViewportSnapshot;

    public Game1(
        bool isWindowDemo = false,
        bool isPaintShellDemo = false,
        bool isDarkDashboardDemo = false,
        bool isCommandingDemo = false,
        bool isThreeScrollViewersDemo = false,
        bool isTwoScrollViewersDemo = false,
        bool isSimpleScrollViewerDemo = false,
        bool isSimpleStackPanelDemo = false,
        bool isScrollViewerTextBoxDemo = false,
        bool isListBoxDemo = false,
        bool isItemsPresenterDemo = false,
        bool isVirtualizedStackPanelDemo = false,
        bool isScrollViewerEdgeCasesDemo = false,
        bool isPasswordBoxDemo = false,
        bool isMenuParityDemo = false,
        bool isBindingParityGap5Demo = false,
        bool isRichTextBoxDemo = false,
        bool isRichTextDiagnosticsLabDemo = false,
        bool isWindowPopupParityLabDemo = false,
        bool isContextMenuParityLabDemo = false)
    {
        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        _isWindowDemo = isWindowDemo;
        _isPaintShellDemo = isPaintShellDemo;
        _isDarkDashboardDemo = isDarkDashboardDemo;
        _isCommandingDemo = isCommandingDemo;
        _isThreeScrollViewersDemo = isThreeScrollViewersDemo;
        _isTwoScrollViewersDemo = isTwoScrollViewersDemo;
        _isSimpleScrollViewerDemo = isSimpleScrollViewerDemo;
        _isSimpleStackPanelDemo = isSimpleStackPanelDemo;
        _isScrollViewerTextBoxDemo = isScrollViewerTextBoxDemo;
        _isListBoxDemo = isListBoxDemo;
        _isItemsPresenterDemo = isItemsPresenterDemo;
        _isVirtualizedStackPanelDemo = isVirtualizedStackPanelDemo;
        _isScrollViewerEdgeCasesDemo = isScrollViewerEdgeCasesDemo;
        _isPasswordBoxDemo = isPasswordBoxDemo;
        _isMenuParityDemo = isMenuParityDemo;
        _isBindingParityGap5Demo = isBindingParityGap5Demo;
        _isRichTextBoxDemo = isRichTextBoxDemo;
        _isRichTextDiagnosticsLabDemo = isRichTextDiagnosticsLabDemo;
        _isWindowPopupParityLabDemo = isWindowPopupParityLabDemo;
        _isContextMenuParityLabDemo = isContextMenuParityLabDemo;
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = !_isWindowPopupParityLabDemo;
        _window.AllowUserResizing = true;
        _window.SetClientSize(GetInitialWindowWidth(), GetInitialWindowHeight());
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
        else if (_isDarkDashboardDemo)
        {
            _darkDashboardView = new DarkDashboardView();
            _root.AddChild(_darkDashboardView);
        }
        else if (_isCommandingDemo)
        {
            _commandingMenuDemoView = new CommandingMenuDemoView();
            _root.AddChild(_commandingMenuDemoView);
        }
        else if (_isThreeScrollViewersDemo)
        {
            _threeScrollViewersView = new ThreeScrollViewersView();
            _root.AddChild(_threeScrollViewersView);
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
        else if (_isVirtualizedStackPanelDemo)
        {
            _virtualizedStackPanelView = new VirtualizedStackPanelView();
            _root.AddChild(_virtualizedStackPanelView);
        }
        else if (_isScrollViewerEdgeCasesDemo)
        {
            _scrollViewerEdgeCasesView = new ScrollViewerEdgeCasesView();
            _root.AddChild(_scrollViewerEdgeCasesView);
        }
        else if (_isPasswordBoxDemo)
        {
            _passwordBoxDemoView = new PasswordBoxDemoView();
            _root.AddChild(_passwordBoxDemoView);
        }
        else if (_isMenuParityDemo)
        {
            _menuParityLabView = new MenuParityLabView();
            _root.AddChild(_menuParityLabView);
        }
        else if (_isBindingParityGap5Demo)
        {
            _bindingParityGap5DemoView = new BindingParityGap5DemoView();
            _root.AddChild(_bindingParityGap5DemoView);
        }
        else if (_isRichTextBoxDemo)
        {
            _richTextBoxDemoView = new RichTextBoxDemoView();
            _root.AddChild(_richTextBoxDemoView);
        }
        else if (_isRichTextDiagnosticsLabDemo)
        {
            _richTextDiagnosticsLabView = new RichTextDiagnosticsLabView();
            _root.AddChild(_richTextDiagnosticsLabView);
        }
        else if (_isWindowPopupParityLabDemo)
        {
            _windowPopupParityLabView = new WindowPopupParityLabView
            {
                ToggleFullscreenRequested = () => _window.ToggleFullScreen(),
                ResizeTo1024Requested = () => _window.SetClientSize(1024, 720, applyChanges: true),
                ResizeTo1280Requested = () => _window.SetClientSize(1280, 900, applyChanges: true),
                WindowSnapshotProvider = () =>
                {
                    var client = _window.ClientSize;
                    var backBuffer = _window.BackBufferSize;
                    return $"Window: C:{client.X}x{client.Y} BB:{backBuffer.X}x{backBuffer.Y} FullScreen={_window.IsFullScreen}";
                }
            };
            _windowPopupParityLabView.RefreshWindowStatus();
            _root.AddChild(_windowPopupParityLabView);
        }
        else if (_isContextMenuParityLabDemo)
        {
            _contextMenuParityLabView = new ContextMenuParityLabView();
            _root.AddChild(_contextMenuParityLabView);
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
        _uiRoot.UseSoftwareCursor = _isWindowPopupParityLabDemo;
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
            _darkDashboardView?.SetFont(font);
            _commandingMenuDemoView?.SetFont(font);
            _threeScrollViewersView?.SetFont(font);
            _twoScrollViewersView?.SetFont(font);
            _simpleScrollViewerView?.SetFont(font);
            _simpleStackPanelView?.SetFont(font);
            _scrollViewerTextBoxView?.SetFont(font);
            _simpleListBoxView?.SetFont(font);
            _simpleItemsPresenterView?.SetFont(font);
            _virtualizedStackPanelView?.SetFont(font);
            _scrollViewerEdgeCasesView?.SetFont(font);
            _passwordBoxDemoView?.SetFont(font);
            _menuParityLabView?.SetFont(font);
            _bindingParityGap5DemoView?.SetFont(font);
            _richTextBoxDemoView?.SetFont(font);
            _richTextDiagnosticsLabView?.SetFont(font);
            _windowPopupParityLabView?.SetFont(font);
            _contextMenuParityLabView?.SetFont(font);
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
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = EnsureViewportMatchesBackBuffer();
        EnsureUiCompositeTarget(viewport);

        if (_uiRoot.ShouldDrawThisFrame(gameTime, viewport, GraphicsDevice))
        {
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
            _windowPopupParityLabView?.RefreshWindowStatus();
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

        if (_isDarkDashboardDemo)
        {
            _baseWindowTitle = $"InkkSlinger Dark Dashboard | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isCommandingDemo)
        {
            _baseWindowTitle = $"InkkSlinger Commanding Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isThreeScrollViewersDemo)
        {
            _baseWindowTitle = $"InkkSlinger Three ScrollViewers Demo | {size.X}x{size.Y}";
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

        if (_isVirtualizedStackPanelDemo)
        {
            _baseWindowTitle = $"InkkSlinger VirtualizedStackPanel Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isScrollViewerEdgeCasesDemo)
        {
            _baseWindowTitle = $"InkkSlinger ScrollViewer Edge Cases | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isPasswordBoxDemo)
        {
            _baseWindowTitle = $"InkkSlinger PasswordBox Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isMenuParityDemo)
        {
            _baseWindowTitle = $"InkkSlinger Menu Parity Lab | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isBindingParityGap5Demo)
        {
            _baseWindowTitle = $"InkkSlinger Binding Parity Gap #5 Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isRichTextBoxDemo)
        {
            _baseWindowTitle = $"InkkSlinger RichTextBox Demo | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isRichTextDiagnosticsLabDemo)
        {
            _baseWindowTitle = $"InkkSlinger RichText Diagnostics Lab | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isWindowPopupParityLabDemo)
        {
            _baseWindowTitle = $"InkkSlinger Window/Popup Parity Lab | {size.X}x{size.Y}";
            ApplyWindowTitle();
            return;
        }

        if (_isContextMenuParityLabDemo)
        {
            _baseWindowTitle = $"InkkSlinger ContextMenu Parity Lab | {size.X}x{size.Y}";
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

    private void ApplyWindowTitle()
    {
        _window.Title = _baseWindowTitle;
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

    private int GetInitialWindowWidth()
    {
        if (_isWindowDemo)
        {
            return 1100;
        }

        if (_isPaintShellDemo)
        {
            return 1580;
        }

        if (_isDarkDashboardDemo)
        {
            return 1580;
        }

        if (_isCommandingDemo)
        {
            return 1500;
        }

        if (_isThreeScrollViewersDemo)
        {
            return 1440;
        }

        if (_isTwoScrollViewersDemo)
        {
            return 1280;
        }

        if (_isSimpleScrollViewerDemo ||
            _isSimpleStackPanelDemo ||
            _isScrollViewerTextBoxDemo ||
            _isListBoxDemo ||
            _isItemsPresenterDemo ||
            _isVirtualizedStackPanelDemo ||
            _isScrollViewerEdgeCasesDemo ||
            _isPasswordBoxDemo ||
            _isMenuParityDemo ||
            _isBindingParityGap5Demo ||
            _isRichTextBoxDemo ||
            _isRichTextDiagnosticsLabDemo ||
            _isWindowPopupParityLabDemo ||
            _isContextMenuParityLabDemo)
        {
            return 1024;
        }

        return 1720;
    }

    private int GetInitialWindowHeight()
    {
        if (_isWindowDemo)
        {
            return 700;
        }

        if (_isPaintShellDemo)
        {
            return 940;
        }

        if (_isDarkDashboardDemo)
        {
            return 940;
        }

        if (_isCommandingDemo)
        {
            return 900;
        }

        if (_isThreeScrollViewersDemo)
        {
            return 820;
        }

        if (_isTwoScrollViewersDemo)
        {
            return 760;
        }

        if (_isSimpleScrollViewerDemo ||
            _isSimpleStackPanelDemo ||
            _isScrollViewerTextBoxDemo ||
            _isListBoxDemo ||
            _isItemsPresenterDemo ||
            _isVirtualizedStackPanelDemo ||
            _isScrollViewerEdgeCasesDemo ||
            _isPasswordBoxDemo ||
            _isMenuParityDemo ||
            _isBindingParityGap5Demo ||
            _isRichTextBoxDemo ||
            _isRichTextDiagnosticsLabDemo ||
            _isWindowPopupParityLabDemo ||
            _isContextMenuParityLabDemo)
        {
            return 720;
        }

        return 1080;
    }
}
