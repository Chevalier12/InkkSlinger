using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class Window : IDisposable
{
    private readonly Game _game;
    private readonly GraphicsDeviceManager _graphics;
    private bool _disposed;

    public Window(Game game, GraphicsDeviceManager graphics)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));

        NativeWindow.ClientSizeChanged += OnClientSizeChanged;
        NativeWindow.TextInput += OnTextInput;
    }

    public event EventHandler? ClientSizeChanged;

    public event EventHandler<TextInputEventArgs>? TextInput;

    public GameWindow NativeWindow => _game.Window;

    public string Title
    {
        get => NativeWindow.Title;
        set => NativeWindow.Title = value ?? string.Empty;
    }

    public bool AllowUserResizing
    {
        get => NativeWindow.AllowUserResizing;
        set => NativeWindow.AllowUserResizing = value;
    }

    public bool IsBorderless
    {
        get => NativeWindow.IsBorderless;
        set => NativeWindow.IsBorderless = value;
    }

    public Point Position
    {
        get => NativeWindow.Position;
        set => NativeWindow.Position = value;
    }

    public Rectangle ClientBounds => NativeWindow.ClientBounds;

    public Point ClientSize => new(ClientBounds.Width, ClientBounds.Height);

    public Point BackBufferSize =>
        new(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

    public bool IsFullScreen => _graphics.IsFullScreen;

    public bool IsMouseVisible
    {
        get => _game.IsMouseVisible;
        set => _game.IsMouseVisible = value;
    }

    public IntPtr Handle => NativeWindow.Handle;

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
        NativeWindow.ClientSizeChanged -= OnClientSizeChanged;
        NativeWindow.TextInput -= OnTextInput;
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        ClientSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        TextInput?.Invoke(this, e);
    }
}
