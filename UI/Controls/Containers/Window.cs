using System;
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
}
