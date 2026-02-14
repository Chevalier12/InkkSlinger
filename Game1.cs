using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private readonly bool _isWindowDemo;
    private readonly bool _isPaintShellDemo;
    private readonly bool _isCommandingDemo;
    private readonly bool _isTwoScrollViewersDemo;
    private SpriteBatch _spriteBatch = null!;
    private UiRoot _uiRoot = null!;
    private MainMenuView? _mainMenuView;
    private WindowDemoView? _windowDemoView;
    private PaintShellView? _paintShellView;
    private CommandingMenuDemoView? _commandingMenuDemoView;
    private TwoScrollViewersView? _twoScrollViewersView;
    private KeyboardState _previousKeyboardState;
    private long _frameUpdateCounter;
    private long _frameDrawCounter;
    private int _lastPrintedHitchCount;

    public Game1(
        bool isWindowDemo = false,
        bool isPaintShellDemo = false,
        bool isCommandingDemo = false,
        bool isTwoScrollViewersDemo = false)
    {
        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        _isWindowDemo = isWindowDemo;
        _isPaintShellDemo = isPaintShellDemo;
        _isCommandingDemo = isCommandingDemo;
        _isTwoScrollViewersDemo = isTwoScrollViewersDemo;
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = true;
        _window.AllowUserResizing = true;
        _window.SetClientSize(
            _isWindowDemo ? 1100 : (_isPaintShellDemo ? 1580 : (_isCommandingDemo ? 1500 : (_isTwoScrollViewersDemo ? 1280 : 1720))),
            _isWindowDemo ? 700 : (_isPaintShellDemo ? 940 : (_isCommandingDemo ? 900 : (_isTwoScrollViewersDemo ? 760 : 1080))));
        _window.Title = "InkkSlinger";
    }

    protected override void Initialize()
    {
        var root = new Panel
        {
            Focusable = true,
            Background = new Color(18, 22, 30)
        };

        if (_isWindowDemo)
        {
            _windowDemoView = new WindowDemoView();
            _windowDemoView.CloseRequested += OnWindowDemoCloseRequested;
            root.AddChild(_windowDemoView);
        }
        else if (_isPaintShellDemo)
        {
            _paintShellView = new PaintShellView();
            root.AddChild(_paintShellView);
        }
        else if (_isCommandingDemo)
        {
            _commandingMenuDemoView = new CommandingMenuDemoView();
            root.AddChild(_commandingMenuDemoView);
        }
        else if (_isTwoScrollViewersDemo)
        {
            _twoScrollViewersView = new TwoScrollViewersView();
            root.AddChild(_twoScrollViewersView);
        }
        else
        {
            _mainMenuView = new MainMenuView();
            root.AddChild(_mainMenuView);
        }

        _uiRoot = new UiRoot(root);
        FrameLoopDiagnostics.Reset();
        _window.TextInput += OnTextInput;
        _window.ClientSizeChanged += OnClientSizeChanged;
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
        }
        catch
        {
            // Keep running without font asset so XAML pipeline can still be exercised.
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var updateStartTicks = Stopwatch.GetTimestamp();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        var preUiStartTicks = Stopwatch.GetTimestamp();
        var keyboardState = Keyboard.GetState();
        if (!_isWindowDemo)
        {
            HandleWindowDemoInput(keyboardState);
        }

        _previousKeyboardState = keyboardState;
        var preUiTicks = Stopwatch.GetTimestamp() - preUiStartTicks;

        var uiRootStartTicks = Stopwatch.GetTimestamp();
        _uiRoot.Update(gameTime, new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        var uiRootTicks = Stopwatch.GetTimestamp() - uiRootStartTicks;

        var baseStartTicks = Stopwatch.GetTimestamp();
        base.Update(gameTime);
        var baseTicks = Stopwatch.GetTimestamp() - baseStartTicks;

        var totalTicks = Stopwatch.GetTimestamp() - updateStartTicks;
        _frameUpdateCounter++;
        FrameLoopDiagnostics.RecordUpdate(
            _frameUpdateCounter,
            gameTime.TotalGameTime,
            TicksToMilliseconds(totalTicks),
            TicksToMilliseconds(preUiTicks),
            TicksToMilliseconds(uiRootTicks),
            TicksToMilliseconds(baseTicks),
            _uiRoot.LastUpdateTiming);

        // Lightweight hitch logging for diagnosing scroll stalls.
        // Only poll the snapshot when we detect a large frame time, to avoid overhead in normal runs.
        if (TicksToMilliseconds(totalTicks) >= 200d || _uiRoot.LastUpdateTiming.TotalMilliseconds >= 200d)
        {
            var snapshot = FrameLoopDiagnostics.GetSnapshot();
            if (snapshot.HitchCount > _lastPrintedHitchCount)
            {
                _lastPrintedHitchCount = snapshot.HitchCount;
                Console.WriteLine(snapshot.LastHitch);
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        var drawStartTicks = Stopwatch.GetTimestamp();

        var clearStartTicks = Stopwatch.GetTimestamp();
        GraphicsDevice.Clear(Color.CornflowerBlue);
        var clearTicks = Stopwatch.GetTimestamp() - clearStartTicks;

        const long beginTicks = 0L;

        var uiDrawStartTicks = Stopwatch.GetTimestamp();
        _uiRoot.Draw(_spriteBatch);
        var uiDrawTicks = Stopwatch.GetTimestamp() - uiDrawStartTicks;

        const long endTicks = 0L;

        var baseStartTicks = Stopwatch.GetTimestamp();
        base.Draw(gameTime);
        var baseTicks = Stopwatch.GetTimestamp() - baseStartTicks;

        var totalTicks = Stopwatch.GetTimestamp() - drawStartTicks;
        _frameDrawCounter++;
        FrameLoopDiagnostics.RecordDraw(
            _frameDrawCounter,
            gameTime.TotalGameTime,
            TicksToMilliseconds(totalTicks),
            TicksToMilliseconds(clearTicks),
            TicksToMilliseconds(beginTicks),
            TicksToMilliseconds(uiDrawTicks),
            TicksToMilliseconds(endTicks),
            TicksToMilliseconds(baseTicks),
            _uiRoot.LastDrawTiming);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _window.TextInput -= OnTextInput;
        _window.ClientSizeChanged -= OnClientSizeChanged;

        if (_windowDemoView != null)
        {
            _windowDemoView.CloseRequested -= OnWindowDemoCloseRequested;
        }

        _window.Dispose();
        _uiRoot.Shutdown();
        base.OnExiting(sender, args);
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        InputManager.ProcessTextInput(e.Character);
    }

    private void HandleWindowDemoInput(KeyboardState keyboardState)
    {
        if (IsNewKeyPress(keyboardState, Keys.F11))
        {
            _window.ToggleFullScreen();
            RefreshWindowTitle();
        }

        if (IsNewKeyPress(keyboardState, Keys.F10))
        {
            _window.IsBorderless = !_window.IsBorderless;
            RefreshWindowTitle();
        }

        if (IsNewKeyPress(keyboardState, Keys.F9))
        {
            _window.CenterOnPrimaryDisplay();
            RefreshWindowTitle();
        }

        if (IsNewKeyPress(keyboardState, Keys.OemPlus) || IsNewKeyPress(keyboardState, Keys.Add))
        {
            ResizeWindowBy(120, 70);
        }

        if (IsNewKeyPress(keyboardState, Keys.OemMinus) || IsNewKeyPress(keyboardState, Keys.Subtract))
        {
            ResizeWindowBy(-120, -70);
        }
    }

    private void ResizeWindowBy(int widthDelta, int heightDelta)
    {
        if (_window.IsFullScreen)
        {
            return;
        }

        var current = _window.ClientSize;
        var newWidth = Math.Max(640, current.X + widthDelta);
        var newHeight = Math.Max(360, current.Y + heightDelta);
        _window.SetClientSize(newWidth, newHeight);
        RefreshWindowTitle();
    }

    private bool IsNewKeyPress(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        RefreshWindowTitle();
    }

    private void RefreshWindowTitle()
    {
        var size = _window.ClientSize;

        if (_isWindowDemo)
        {
            _window.Title = $"InkkSlinger Window Demo | {size.X}x{size.Y}";
            return;
        }

        if (_isPaintShellDemo)
        {
            _window.Title = $"InkkSlinger Paint Shell | {size.X}x{size.Y}";
            return;
        }

        if (_isCommandingDemo)
        {
            _window.Title = $"InkkSlinger Commanding Demo | {size.X}x{size.Y}";
            return;
        }

        if (_isTwoScrollViewersDemo)
        {
            _window.Title = $"InkkSlinger Two ScrollViewers Demo | {size.X}x{size.Y}";
            return;
        }

        var mode = _window.IsFullScreen ? "Fullscreen" : "Windowed";
        var border = _window.IsBorderless ? "Borderless" : "Framed";
        _window.Title =
            $"InkkSlinger | {size.X}x{size.Y} | {mode} | {border} | F11 Fullscreen, F10 Borderless, F9 Center, +/- Resize";
    }

    private void OnWindowDemoCloseRequested(object? sender, EventArgs e)
    {
        Exit();
    }

    private static double TicksToMilliseconds(long ticks)
    {
        if (ticks <= 0)
        {
            return 0d;
        }

        return ticks * 1000d / Stopwatch.Frequency;
    }
}
