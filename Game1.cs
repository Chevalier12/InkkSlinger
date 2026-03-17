using System;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Game1 : Game
{
    private static readonly bool EnableExperimentalPartialRedraw = true;
    private const int IdleThrottleSleepMilliseconds = 8;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D? _uiCompositeTarget;
    private Panel _root = null!;
    private UiRoot _uiRoot = null!;
    private ControlsCatalogView? _catalogView;
    private WindowThemeBinding? _windowThemeBinding;
    private bool _shouldDrawUiThisFrame = true;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = true;
        _window.AllowUserResizing = true;
        _window.SetClientSize(1280, 820);
        _window.Title = "InkkSlinger Controls Catalog";
    }

    protected override void Initialize()
    {
        var appMarkupPath = Path.Combine(AppContext.BaseDirectory, "App.xml");
        if (File.Exists(appMarkupPath))
        {
            XamlLoader.LoadApplicationResourcesFromFile(appMarkupPath, clearExisting: true);
        }

        _root = new Panel();
        _windowThemeBinding = new WindowThemeBinding(_window, _root);

        _catalogView = new ControlsCatalogView();
        _root.AddChild(_catalogView);

        _window.ClientSizeChanged += OnClientSizeChanged;
        _window.NativeWindow.TextInput += OnTextInput;

        _uiRoot = new UiRoot(_root)
        {
            UseRetainedRenderList = EnableExperimentalPartialRedraw,
            UseDirtyRegionRendering = EnableExperimentalPartialRedraw,
            UseConditionalDrawScheduling = true,
            UseSoftwareCursor = false
        };

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        try
        {
            UiTextRenderer.SetDefaultTypography("Segoe UI", 12f, "Normal", "Normal");
            UiTextRenderer.PrewarmDefaultGlyphs(GraphicsDevice);
        }
        catch
        {
            // Keep running without font asset so the control catalog remains usable.
        }
    }

    protected override void Update(GameTime gameTime)
    {
        EnsureBackBufferMatchesClientSize();
        var viewport = EnsureViewportMatchesBackBuffer();
        _uiRoot.Update(gameTime, viewport);
        _shouldDrawUiThisFrame = _uiRoot.ShouldDrawThisFrame(gameTime, viewport, GraphicsDevice);
        if (!_shouldDrawUiThisFrame)
        {
            SuppressDraw();
            var idleThrottleDelayMilliseconds = GetIdleThrottleDelayMilliseconds(IsActive, _shouldDrawUiThisFrame);
            if (idleThrottleDelayMilliseconds > 0)
            {
                Thread.Sleep(idleThrottleDelayMilliseconds);
            }
        }

        base.Update(gameTime);
    }

    internal static int GetIdleThrottleDelayMilliseconds(bool isActive, bool shouldDrawUiThisFrame)
    {
        return isActive && !shouldDrawUiThisFrame
            ? IdleThrottleSleepMilliseconds
            : 0;
    }

    internal static bool ShouldDrawUiOnCurrentFrame(bool scheduledDraw, bool targetRecreated)
    {
        return scheduledDraw || targetRecreated;
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = EnsureViewportMatchesBackBuffer();
        var targetRecreated = EnsureUiCompositeTarget(viewport);
        var shouldDrawUiThisFrame = ShouldDrawUiOnCurrentFrame(_shouldDrawUiThisFrame, targetRecreated);
        if (EnableExperimentalPartialRedraw && targetRecreated)
        {
            _uiRoot.ForceFullRedrawForSurfaceReset();
        }

        if (shouldDrawUiThisFrame)
        {
            GraphicsDevice.SetRenderTarget(_uiCompositeTarget);
            if (!EnableExperimentalPartialRedraw)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
            }

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
        _windowThemeBinding?.Dispose();
        _windowThemeBinding = null;

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
    }

    private bool EnsureUiCompositeTarget(Viewport viewport)
    {
        if (_uiCompositeTarget != null &&
            !_uiCompositeTarget.IsDisposed &&
            _uiCompositeTarget.Width == viewport.Width &&
            _uiCompositeTarget.Height == viewport.Height)
        {
            return false;
        }

        _uiCompositeTarget?.Dispose();
        var usage = EnableExperimentalPartialRedraw
            ? RenderTargetUsage.PreserveContents
            : RenderTargetUsage.DiscardContents;
        _uiCompositeTarget = new RenderTarget2D(
            GraphicsDevice,
            Math.Max(1, viewport.Width),
            Math.Max(1, viewport.Height),
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            usage);
        return true;
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
