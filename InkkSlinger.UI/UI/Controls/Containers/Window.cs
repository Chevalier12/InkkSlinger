using System;
using System.Collections.Generic;
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

    void EnsureBorderlessChromeStyle(bool isBorderless);

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
        set
        {
            _window.Title = value ?? string.Empty;
            EnsureBorderlessChromeStyle(_window.IsBorderless);
        }
    }

    public bool AllowUserResizing
    {
        get => _window.AllowUserResizing;
        set => _window.AllowUserResizing = value;
    }

    public bool IsBorderless
    {
        get => _window.IsBorderless;
        set
        {
            _window.IsBorderless = value;
            EnsureBorderlessChromeStyle(value);
        }
    }

    public void EnsureBorderlessChromeStyle(bool isBorderless)
    {
        if (!OperatingSystem.IsWindows() || _window.Handle == IntPtr.Zero || !isBorderless)
        {
            return;
        }

        Window.EnsureSdlWindowBorderless(_window.Handle);
        var hwnd = Window.ResolveWin32WindowHandle(_window.Handle);
        Window.EnsureWin32BorderlessChrome(hwnd);
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
    private const int SwMinimize = 6;
    private const int WmNclButtonDown = 0x00A1;
    private const int HtCaption = 2;

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
    private bool _requestedIsBorderless;
    private Point _preMaximizePosition;
    private Point _preMaximizeSize;
    private bool _isMaximized;
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
        _requestedIsBorderless = _nativeWindow.IsBorderless;
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
        set
        {
            _nativeWindow.Title = value ?? string.Empty;
            EnsureNativeChromeState();
        }
    }

    public bool AllowUserResizing
    {
        get => _nativeWindow.AllowUserResizing;
        set => _nativeWindow.AllowUserResizing = value;
    }

    public bool IsBorderless
    {
        get => _requestedIsBorderless;
        set
        {
            _requestedIsBorderless = value;
            _nativeWindow.IsBorderless = value;
            EnsureNativeChromeState();
        }
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

    public int Width
    {
        get => BackBufferSize.X;
        set => SetClientSize(value, Height);
    }

    public int Height
    {
        get => BackBufferSize.Y;
        set => SetClientSize(Width, value);
    }

    public bool IsFullScreen
    {
        get => _graphics.IsFullScreen;
        set => SetFullScreen(value);
    }

    public bool IsMaximized => _isMaximized;

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
            EnsureNativeChromeState();
        }
    }

    public void SetFullScreen(bool isFullScreen, bool applyChanges = true)
    {
        _graphics.IsFullScreen = isFullScreen;
        if (applyChanges)
        {
            _graphics.ApplyChanges();
            EnsureNativeChromeState();
        }
    }

    public void ToggleFullScreen(bool applyChanges = true)
    {
        _graphics.IsFullScreen = !_graphics.IsFullScreen;
        if (applyChanges)
        {
            _graphics.ApplyChanges();
            EnsureNativeChromeState();
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

        if (_isMaximized)
        {
            Restore();
            return;
        }

        _preMaximizePosition = Position;
        _preMaximizeSize = ClientSize;

        var clientSize = ClientSize;
        var centerPoint = new NativeMethods.POINT
        {
            X = Position.X + Math.Max(1, clientSize.X / 2),
            Y = Position.Y + Math.Max(1, clientSize.Y / 2)
        };

        var monitor = NativeMethods.MonitorFromPoint(centerPoint, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            _preMaximizePosition = default;
            _preMaximizeSize = default;
            throw new InvalidOperationException("Unable to resolve the target monitor for window maximization.");
        }

        var monitorInfo = NativeMethods.MONITORINFO.Create();
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            _preMaximizePosition = default;
            _preMaximizeSize = default;
            throw new InvalidOperationException("Unable to read monitor bounds for window maximization.");
        }

        var workArea = monitorInfo.rcWork;
        var width = workArea.Right - workArea.Left;
        var height = workArea.Bottom - workArea.Top;
        if (width <= 0 || height <= 0)
        {
            _preMaximizePosition = default;
            _preMaximizeSize = default;
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
            _preMaximizePosition = default;
            _preMaximizeSize = default;
            throw new InvalidOperationException("Native window maximization produced an invalid client size.");
        }

        Position = new Point(workArea.Left, workArea.Top);
        SetClientSize(clientWidth, clientHeight, applyChanges: true);
        Position = new Point(workArea.Left, workArea.Top);
        _isMaximized = true;
    }

    public void Restore()
    {
        if (!_isMaximized)
        {
            return;
        }

        Position = _preMaximizePosition;
        SetClientSize(_preMaximizeSize.X, _preMaximizeSize.Y, applyChanges: true);
        _isMaximized = false;
        _preMaximizePosition = default;
        _preMaximizeSize = default;
    }

    public void Minimize()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Window minimization is currently only implemented on Windows.");
        }

        var hwnd = ResolveWin32WindowHandle(Handle);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Native window handle is unavailable for minimization.");
        }

        NativeMethods.ShowWindow(hwnd, SwMinimize);
    }

    public void BeginDragMove()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Window drag move is currently only implemented on Windows.");
        }

        var hwnd = ResolveWin32WindowHandle(Handle);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Native window handle is unavailable for drag move.");
        }

        NativeMethods.ReleaseCapture();
        NativeMethods.GetCursorPos(out var cursorPosition);
        NativeMethods.SendMessage(hwnd, WmNclButtonDown, new IntPtr(HtCaption), PackLParam(cursorPosition.X, cursorPosition.Y));
    }

    private static IntPtr PackLParam(int lowWord, int highWord)
    {
        return new IntPtr((highWord << 16) | (lowWord & 0xFFFF));
    }

    public void ApplyChanges()
    {
        _graphics.ApplyChanges();
        EnsureNativeChromeState();
    }

    public void EnsureNativeChromeState()
    {
        if (_nativeWindow.IsBorderless != _requestedIsBorderless)
        {
            _nativeWindow.IsBorderless = _requestedIsBorderless;
        }

        _nativeWindow.EnsureBorderlessChromeStyle(_requestedIsBorderless);
    }

    internal static void EnsureWin32BorderlessChrome(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlStyle);
        if (style == IntPtr.Zero)
        {
            return;
        }

        var styleValue = style.ToInt64();
        var borderlessStyleValue = styleValue & ~NativeMethods.BorderlessChromeStyleMask;
        var exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        var exStyleValue = exStyle.ToInt64();
        var borderlessExStyleValue = exStyleValue & ~NativeMethods.BorderlessChromeExStyleMask;
        if (borderlessStyleValue == styleValue && borderlessExStyleValue == exStyleValue)
        {
            NativeMethods.EnsureBorderlessWindowSubclass(handle);
            return;
        }

        if (borderlessStyleValue != styleValue)
        {
            _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlStyle, new IntPtr(borderlessStyleValue));
        }

        if (borderlessExStyleValue != exStyleValue)
        {
            _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(borderlessExStyleValue));
        }

        _ = NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove |
            NativeMethods.SwpNoSize |
            NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged);
            NativeMethods.EnsureBorderlessWindowSubclass(handle);
    }

    internal static IntPtr ResolveWin32WindowHandle(IntPtr nativeHandle)
    {
        if (!OperatingSystem.IsWindows() || nativeHandle == IntPtr.Zero)
        {
            return nativeHandle;
        }

        if (NativeMethods.TryGetSdlWindowWin32Handle(nativeHandle, out var hwnd) && hwnd != IntPtr.Zero)
        {
            return hwnd;
        }

        if (NativeMethods.TryFindCurrentProcessSdlWindow(out hwnd) && hwnd != IntPtr.Zero)
        {
            return hwnd;
        }

        return nativeHandle;
    }

    internal static void EnsureSdlWindowBorderless(IntPtr nativeHandle)
    {
        if (!OperatingSystem.IsWindows() || nativeHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.TrySetSdlWindowBordered(nativeHandle, bordered: false);
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
        private static readonly object BorderlessSubclassLock = new();
        private static readonly Dictionary<IntPtr, BorderlessSubclassState> BorderlessSubclasses = new();

        public const int GwlStyle = -16;
        public const int GwlExStyle = -20;
        public const int GwlWndProc = -4;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpFrameChanged = 0x0020;
        public const long BorderlessChromeStyleMask = 0x00C00000L | 0x00040000L;
        public const long BorderlessChromeExStyleMask = 0x00000001L | 0x00000200L | 0x00010000L;
        public const int WmNcCalcSize = 0x0083;
        public const int WmNcActivate = 0x0086;
        public const int WmNcPaint = 0x0085;
        public const int WmActivateApp = 0x001C;
        public const int WmWindowPosChanged = 0x0047;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetVersion")]
        private static extern void SdlGetVersion(out SDL_VERSION version);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowWMInfo")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SdlGetWindowWMInfo(IntPtr window, ref SDL_SYS_WM_INFO info);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_SetWindowBordered")]
        private static extern void SdlSetWindowBordered(IntPtr window, int bordered);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr64(hWnd, nIndex);
            }

            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }

            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public static void EnsureBorderlessWindowSubclass(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            lock (BorderlessSubclassLock)
            {
                if (BorderlessSubclasses.ContainsKey(hwnd))
                {
                    return;
                }

                WndProc wndProc = BorderlessWindowProc;
                var oldWndProc = SetWindowLongPtr(hwnd, GwlWndProc, Marshal.GetFunctionPointerForDelegate(wndProc));
                if (oldWndProc == IntPtr.Zero)
                {
                    return;
                }

                BorderlessSubclasses[hwnd] = new BorderlessSubclassState(oldWndProc, wndProc);
            }
        }

        private static IntPtr BorderlessWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg is WmNcCalcSize or WmNcActivate or WmNcPaint or WmActivateApp or WmWindowPosChanged)
            {
                ApplyBorderlessChromeStyle(hwnd);
            }

            return msg switch
            {
                WmNcCalcSize => IntPtr.Zero,
                WmNcActivate => new IntPtr(1),
                WmNcPaint => IntPtr.Zero,
                _ => CallPreviousWindowProc(hwnd, msg, wParam, lParam)
            };
        }

        private static void ApplyBorderlessChromeStyle(IntPtr hwnd)
        {
            var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
            var borderlessStyle = style & ~BorderlessChromeStyleMask;
            if (borderlessStyle != style)
            {
                _ = SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(borderlessStyle));
            }

            var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            var borderlessExStyle = exStyle & ~BorderlessChromeExStyleMask;
            if (borderlessExStyle != exStyle)
            {
                _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(borderlessExStyle));
            }
        }

        private static IntPtr CallPreviousWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            lock (BorderlessSubclassLock)
            {
                if (BorderlessSubclasses.TryGetValue(hwnd, out var state))
                {
                    return CallWindowProc(state.OldWndProc, hwnd, msg, wParam, lParam);
                }
            }

            return IntPtr.Zero;
        }

        public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed record BorderlessSubclassState(IntPtr OldWndProc, WndProc WndProc);

        public static bool TryGetSdlWindowWin32Handle(IntPtr sdlWindow, out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            if (sdlWindow == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                SdlGetVersion(out var version);
                var info = new SDL_SYS_WM_INFO
                {
                    Version = version
                };
                if (!SdlGetWindowWMInfo(sdlWindow, ref info) || info.Subsystem != SDL_SYS_WM_TYPE.Windows)
                {
                    return false;
                }

                hwnd = info.Window;
                return hwnd != IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        public static void TrySetSdlWindowBordered(IntPtr sdlWindow, bool bordered)
        {
            if (sdlWindow == IntPtr.Zero)
            {
                return;
            }

            try
            {
                SdlSetWindowBordered(sdlWindow, bordered ? 1 : 0);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        public static bool TryFindCurrentProcessSdlWindow(out IntPtr hwnd)
        {
            var finder = new CurrentProcessWindowFinder(GetCurrentProcessId());
            EnumWindows(finder.Visit, IntPtr.Zero);
            hwnd = finder.BestHandle;
            return hwnd != IntPtr.Zero;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private sealed class CurrentProcessWindowFinder
        {
            private readonly uint _processId;
            private int _bestArea;

            public CurrentProcessWindowFinder(uint processId)
            {
                _processId = processId;
            }

            public IntPtr BestHandle { get; private set; }

            public bool Visit(IntPtr hwnd, IntPtr lParam)
            {
                _ = lParam;
                if (hwnd == IntPtr.Zero || GetWindowThreadProcessId(hwnd, out var processId) == 0 || processId != _processId)
                {
                    return true;
                }

                if (!GetWindowRect(hwnd, out var rect))
                {
                    return true;
                }

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0)
                {
                    return true;
                }

                var className = GetWindowClassName(hwnd);
                var area = width * height;
                if (string.Equals(className, "SDL_app", StringComparison.Ordinal) || area > _bestArea)
                {
                    BestHandle = hwnd;
                    _bestArea = area;
                }

                return !string.Equals(className, "SDL_app", StringComparison.Ordinal);
            }

            private static string GetWindowClassName(IntPtr hwnd)
            {
                var buffer = new char[256];
                var length = GetClassName(hwnd, buffer, buffer.Length);
                return length <= 0 ? string.Empty : new string(buffer, 0, length);
            }
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_VERSION
        {
            public byte Major;
            public byte Minor;
            public byte Patch;
        }

        public enum SDL_SYS_WM_TYPE
        {
            Unknown,
            Windows,
            X11,
            Directfb,
            Cocoa,
            UiKit,
            Wayland,
            Mir,
            WinRt,
            Android,
            Vivante,
            Os2,
            Haiku,
            KmsDrm,
            RiscOs
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_SYS_WM_INFO
        {
            public SDL_VERSION Version;
            public SDL_SYS_WM_TYPE Subsystem;
            public IntPtr Window;
            public IntPtr Hdc;
            public IntPtr HInstance;
        }
    }
}
