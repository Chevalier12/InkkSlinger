using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal interface IWindowNativeAdapter
{
    event EventHandler<EventArgs> ClientSizeChanged;

    string Title { get; set; }

    bool AllowUserResizing { get; set; }

    bool IsBorderless { get; set; }

    Point Position { get; set; }

    Rectangle ClientBounds { get; }

    IntPtr Handle { get; }
}

internal interface IWindowGraphicsAdapter
{
    int PreferredBackBufferWidth { get; set; }

    int PreferredBackBufferHeight { get; set; }

    bool IsFullScreen { get; set; }

    void ApplyChanges();
}

internal sealed class GameWindowNativeAdapter : IWindowNativeAdapter
{
    private readonly GameWindow _window;

    public GameWindowNativeAdapter(GameWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public event EventHandler<EventArgs> ClientSizeChanged
    {
        add => _window.ClientSizeChanged += value;
        remove => _window.ClientSizeChanged -= value;
    }

    public string Title
    {
        get => _window.Title;
        set => _window.Title = value ?? string.Empty;
    }

    public bool AllowUserResizing
    {
        get => _window.AllowUserResizing;
        set => _window.AllowUserResizing = value;
    }

    public bool IsBorderless
    {
        get => _window.IsBorderless;
        set => _window.IsBorderless = value;
    }

    public Point Position
    {
        get => _window.Position;
        set => _window.Position = value;
    }

    public Rectangle ClientBounds => _window.ClientBounds;

    public IntPtr Handle => _window.Handle;
}

internal sealed class GraphicsDeviceManagerAdapter : IWindowGraphicsAdapter
{
    private readonly GraphicsDeviceManager _graphics;

    public GraphicsDeviceManagerAdapter(GraphicsDeviceManager graphics)
    {
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
    }

    public int PreferredBackBufferWidth
    {
        get => _graphics.PreferredBackBufferWidth;
        set => _graphics.PreferredBackBufferWidth = value;
    }

    public int PreferredBackBufferHeight
    {
        get => _graphics.PreferredBackBufferHeight;
        set => _graphics.PreferredBackBufferHeight = value;
    }

    public bool IsFullScreen
    {
        get => _graphics.IsFullScreen;
        set => _graphics.IsFullScreen = value;
    }

    public void ApplyChanges()
    {
        _graphics.ApplyChanges();
    }
}

public sealed class Window : DependencyObject, IDisposable
{
    private const int MonitorDefaultToNearest = 2;
    private const int SmCxSizeFrame = 32;
    private const int SmCySizeFrame = 33;
    private const int SmCyCaption = 4;
    private const int SmCxPaddedBorder = 92;

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Window),
            new FrameworkPropertyMetadata(Color.Transparent));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Window),
            new FrameworkPropertyMetadata(Color.White));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(Window),
            new FrameworkPropertyMetadata(FontFamily.Empty));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(float),
            typeof(Window),
            new FrameworkPropertyMetadata(12f));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(string),
            typeof(Window),
            new FrameworkPropertyMetadata("Normal"));

    public static readonly DependencyProperty FontStyleProperty =
        DependencyProperty.Register(
            nameof(FontStyle),
            typeof(string),
            typeof(Window),
            new FrameworkPropertyMetadata("Normal"));

    public static readonly DependencyProperty StyleProperty =
        DependencyProperty.Register(
            nameof(Style),
            typeof(Style),
            typeof(Window),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Window window)
                    {
                        window.OnStyleChanged(args.OldValue as Style, args.NewValue as Style);
                    }
                }));

    private readonly Game? _game;
    private readonly IWindowNativeAdapter _nativeWindow;
    private readonly IWindowGraphicsAdapter _graphics;
    private Style? _activeImplicitStyle;
    private bool _isApplyingImplicitStyle;
    private bool _disposed;

    public Window(Game game, GraphicsDeviceManager graphics)
        : this(
            game ?? throw new ArgumentNullException(nameof(game)),
            new GameWindowNativeAdapter(game.Window),
            new GraphicsDeviceManagerAdapter(graphics ?? throw new ArgumentNullException(nameof(graphics))))
    {
    }

    internal Window(IWindowNativeAdapter nativeWindow, IWindowGraphicsAdapter graphics)
        : this(
            game: null,
            nativeWindow ?? throw new ArgumentNullException(nameof(nativeWindow)),
            graphics ?? throw new ArgumentNullException(nameof(graphics)))
    {
    }

    private Window(Game? game, IWindowNativeAdapter nativeWindow, IWindowGraphicsAdapter graphics)
    {
        _game = game;
        _nativeWindow = nativeWindow;
        _graphics = graphics;
        _nativeWindow.ClientSizeChanged += OnClientSizeChanged;
        UiApplication.Current.Resources.Changed += OnApplicationResourcesChanged;
        UpdateImplicitStyle();
    }

    public event EventHandler? ClientSizeChanged;

    public GameWindow NativeWindow
    {
        get
        {
            if (_game == null)
            {
                throw new InvalidOperationException("NativeWindow is unavailable for test-backed window instances.");
            }

            return _game.Window;
        }
    }

    public string Title
    {
        get => _nativeWindow.Title;
        set => _nativeWindow.Title = value ?? string.Empty;
    }

    public bool AllowUserResizing
    {
        get => _nativeWindow.AllowUserResizing;
        set => _nativeWindow.AllowUserResizing = value;
    }

    public bool IsBorderless
    {
        get => _nativeWindow.IsBorderless;
        set => _nativeWindow.IsBorderless = value;
    }

    public Point Position
    {
        get => _nativeWindow.Position;
        set => _nativeWindow.Position = value;
    }

    public int Left
    {
        get => Position.X;
        set => Position = new Point(value, Position.Y);
    }

    public int Top
    {
        get => Position.Y;
        set => Position = new Point(Position.X, value);
    }

    public Rectangle ClientBounds => _nativeWindow.ClientBounds;

    public Point ClientSize => new(ClientBounds.Width, ClientBounds.Height);

    public Point BackBufferSize => new(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

    public bool IsFullScreen => _graphics.IsFullScreen;

    public bool IsMouseVisible
    {
        get => _game?.IsMouseVisible ?? false;
        set
        {
            if (_game != null)
            {
                _game.IsMouseVisible = value;
            }
        }
    }

    public IntPtr Handle => _nativeWindow.Handle;

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty) ?? FontFamily.Empty;
        set => SetValue(FontFamilyProperty, value);
    }

    public float FontSize
    {
        get => GetValue<float>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public string FontWeight
    {
        get => GetValue<string>(FontWeightProperty) ?? "Normal";
        set => SetValue(FontWeightProperty, value);
    }

    public string FontStyle
    {
        get => GetValue<string>(FontStyleProperty) ?? "Normal";
        set => SetValue(FontStyleProperty, value);
    }

    public Style? Style
    {
        get => GetValue<Style>(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    public void SetClientSize(int width, int height, bool applyChanges = true)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;

        if (applyChanges)
        {
            _graphics.ApplyChanges();
        }
    }

    public void SetFullScreen(bool isFullScreen, bool applyChanges = true)
    {
        _graphics.IsFullScreen = isFullScreen;
        if (applyChanges)
        {
            _graphics.ApplyChanges();
        }
    }

    public void ToggleFullScreen(bool applyChanges = true)
    {
        _graphics.IsFullScreen = !_graphics.IsFullScreen;
        if (applyChanges)
        {
            _graphics.ApplyChanges();
        }
    }

    public void CenterOnPrimaryDisplay()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var size = ClientSize;
        var x = Math.Max(0, (display.Width - size.X) / 2);
        var y = Math.Max(0, (display.Height - size.Y) / 2);
        Position = new Point(x, y);
    }

    public void Maximize()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Window maximization is currently only implemented on Windows.");
        }

        var clientSize = ClientSize;
        var centerPoint = new NativeMethods.POINT
        {
            X = Position.X + Math.Max(1, clientSize.X / 2),
            Y = Position.Y + Math.Max(1, clientSize.Y / 2)
        };

        var monitor = NativeMethods.MonitorFromPoint(centerPoint, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to resolve the target monitor for window maximization.");
        }

        var monitorInfo = NativeMethods.MONITORINFO.Create();
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new InvalidOperationException("Unable to read monitor bounds for window maximization.");
        }

        var workArea = monitorInfo.rcWork;
        var width = workArea.Right - workArea.Left;
        var height = workArea.Bottom - workArea.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Monitor work area reported an invalid size for window maximization.");
        }

        var horizontalChrome = 0;
        var verticalChrome = 0;
        if (!IsBorderless)
        {
            var frameWidth = NativeMethods.GetSystemMetrics(SmCxSizeFrame);
            var frameHeight = NativeMethods.GetSystemMetrics(SmCySizeFrame);
            var captionHeight = NativeMethods.GetSystemMetrics(SmCyCaption);
            var paddedBorder = NativeMethods.GetSystemMetrics(SmCxPaddedBorder);
            horizontalChrome = (frameWidth * 2) + (paddedBorder * 2);
            verticalChrome = (frameHeight * 2) + (paddedBorder * 2) + captionHeight;
        }

        var clientWidth = Math.Max(1, width - horizontalChrome);
        var clientHeight = Math.Max(1, height - verticalChrome);
        if (clientWidth <= 0 || clientHeight <= 0)
        {
            throw new InvalidOperationException("Native window maximization produced an invalid client size.");
        }

        Position = new Point(workArea.Left, workArea.Top);
        SetClientSize(clientWidth, clientHeight, applyChanges: true);
        Position = new Point(workArea.Left, workArea.Top);
    }

    public void ApplyChanges()
    {
        _graphics.ApplyChanges();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _nativeWindow.ClientSizeChanged -= OnClientSizeChanged;
        UiApplication.Current.Resources.Changed -= OnApplicationResourcesChanged;
        if (Style is Style style)
        {
            style.Detach(this);
        }
    }

    public void SetPosition(int x, int y)
    {
        Position = new Point(x, y);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        ClientSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplicationResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateImplicitStyle();
    }

    private void OnStyleChanged(Style? oldStyle, Style? newStyle)
    {
        if (!_isApplyingImplicitStyle)
        {
            _activeImplicitStyle = null;
        }

        oldStyle?.Detach(this);
        newStyle?.Apply(this);

        if (_isApplyingImplicitStyle)
        {
            _activeImplicitStyle = newStyle;
        }
    }

    private void UpdateImplicitStyle()
    {
        if (!ShouldApplyImplicitStyle())
        {
            return;
        }

        if (UiApplication.Current.Resources.TryGetValue(typeof(Window), out var resource) &&
            resource is Style style)
        {
            if (!ReferenceEquals(Style, style))
            {
                _isApplyingImplicitStyle = true;
                try
                {
                    Style = style;
                }
                finally
                {
                    _isApplyingImplicitStyle = false;
                }
            }

            _activeImplicitStyle = style;
            return;
        }

        if (ImplicitStylePolicy.CanClearImplicit(Style, _activeImplicitStyle))
        {
            _isApplyingImplicitStyle = true;
            try
            {
                Style = null;
            }
            finally
            {
                _isApplyingImplicitStyle = false;
            }
        }

        _activeImplicitStyle = null;
    }

    private bool ShouldApplyImplicitStyle()
    {
        return ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;

            public static MONITORINFO Create()
            {
                return new MONITORINFO
                {
                    cbSize = Marshal.SizeOf<MONITORINFO>()
                };
            }
        }
    }
}
