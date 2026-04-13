using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class InkkSlingerGameHost : Game
{
    private const int IdleThrottleSleepMilliseconds = 8;
    private const double FpsWindowTitleRefreshIntervalSeconds = 0.1d;

    private readonly GraphicsDeviceManager _graphics;
    private readonly InkkSlinger.Window _window;
    private readonly InkkSlingerOptions _options;
    private readonly InkkOopsRuntimeOptions _inkkOopsOptions;
    private readonly Func<UIElement> _rootContentFactory;
    private InkkOopsHostConfiguration _inkkOopsHostConfiguration = null!;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D? _uiCompositeTarget;
    private UIElement? _rootContent;
    private Panel _root = null!;
    private UiRoot _uiRoot = null!;
    private WindowThemeBinding? _windowThemeBinding;
    private bool _shouldDrawUiThisFrame = true;
    private int _appFrameCount;
    private long _appFpsWindowStartTimestamp;
    private string _displayedAppFps = "0.0";
    private InkkOopsGameHost? _inkkOopsHost;
    private InkkOopsRuntimeService? _inkkOopsRuntimeService;

    public InkkSlingerGameHost(Func<UIElement> rootContentFactory, InkkSlingerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootContentFactory);

        _rootContentFactory = rootContentFactory;
        _options = options ?? new InkkSlingerOptions();
        _inkkOopsOptions = _options.InkkOopsRuntimeOptions ?? new InkkOopsRuntimeOptions();

        _graphics = new GraphicsDeviceManager(this);
        _window = new InkkSlinger.Window(this, _graphics);
        Content.RootDirectory = "Content";
        _window.IsMouseVisible = _options.IsMouseVisible;
        _window.AllowUserResizing = _options.AllowUserResizing;
        _window.SetClientSize(_options.InitialWindowWidth, _options.InitialWindowHeight);
        _window.Title = _options.WindowTitle;
        UiApplication.Current.AttachMainWindow(
            _window,
            _options,
            () =>
            {
                if (_uiRoot != null)
                {
                    _uiRoot.EnqueueDeferredOperation(Exit);
                    return;
                }

                Exit();
            });
    }

    protected internal string DisplayedFps => _displayedAppFps;

    protected override void Initialize()
    {
        LoadApplicationResourcesIfPresent();

        var rootContent = CreateRootContent();
        _rootContent = rootContent;
        _inkkOopsHostConfiguration = CreateInkkOopsHostConfiguration(rootContent.GetType().Assembly);

        _root = new Panel();
        _windowThemeBinding = new WindowThemeBinding(_window, _root);
        _root.AddChild(rootContent);

        _window.ClientSizeChanged += OnClientSizeChanged;
        _window.NativeWindow.TextInput += OnTextInput;

        _uiRoot = CreateUiRoot(_root);
        InitializeInkkOopsRuntime();

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
            // Keep running even when the default font assets are unavailable.
        }
    }

    protected override void Update(GameTime gameTime)
    {
        EnsureBackBufferMatchesClientSize();
        var viewport = EnsureViewportMatchesBackBuffer();

        _uiRoot.Update(gameTime, viewport);
        _inkkOopsRuntimeService?.Update();

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

        if (UiApplication.Current.FpsEnabled)
        {
            UpdateDisplayedFpsFromAppCadence();
            UpdateWindowTitleWithDiagnostics();
        }
        else
        {
            var baseTitle = ExtractBaseWindowTitle(_window.Title);
            if (!string.Equals(baseTitle, _window.Title, StringComparison.Ordinal))
            {
                _window.Title = baseTitle;
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

        if (targetRecreated)
        {
            _uiRoot.ForceFullRedrawForSurfaceReset();
        }

        if (shouldDrawUiThisFrame)
        {
            if (targetRecreated && !_shouldDrawUiThisFrame)
            {
                _uiRoot.RecordForcedDrawForSurfaceReset();
            }

            GraphicsDevice.SetRenderTarget(_uiCompositeTarget);
            _uiRoot.Draw(_spriteBatch, gameTime);
            GraphicsDevice.SetRenderTarget(null);
            _inkkOopsRuntimeService?.AfterDraw();
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

    internal static bool TryComputeDisplayedFps(
        int accumulatedFrameCount,
        double accumulatedElapsedSeconds,
        out string displayedFps)
    {
        if (accumulatedElapsedSeconds < FpsWindowTitleRefreshIntervalSeconds)
        {
            displayedFps = string.Empty;
            return false;
        }

        var fps = accumulatedElapsedSeconds <= 0d
            ? 0d
            : accumulatedFrameCount / accumulatedElapsedSeconds;
        displayedFps = $"{fps:0.0}";
        return true;
    }

    internal static string BuildWindowTitle(string baseTitle, string displayedFps, string hoveredElement)
    {
        return $"{baseTitle} | App FPS: {displayedFps} | Hovered: {hoveredElement}";
    }

    internal static string ExtractBaseWindowTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        const string appFpsMarker = " | App FPS: ";
        const string legacyFpsMarker = " | FPS: ";
        var markerIndex = title.IndexOf(appFpsMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            markerIndex = title.IndexOf(legacyFpsMarker, StringComparison.Ordinal);
        }

        return markerIndex < 0
            ? title
            : title[..markerIndex];
    }

    internal static string ExtractDisplayedFpsFromWindowTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "0.0";
        }

        const string appFpsMarker = " | App FPS: ";
        const string legacyFpsMarker = " | FPS: ";
        const string hoveredMarker = " | Hovered: ";
        var fpsMarker = appFpsMarker;
        var fpsStart = title.IndexOf(fpsMarker, StringComparison.Ordinal);
        if (fpsStart < 0)
        {
            fpsMarker = legacyFpsMarker;
            fpsStart = title.IndexOf(fpsMarker, StringComparison.Ordinal);
        }

        if (fpsStart < 0)
        {
            return "0.0";
        }

        fpsStart += fpsMarker.Length;
        var fpsEnd = title.IndexOf(hoveredMarker, fpsStart, StringComparison.Ordinal);
        if (fpsEnd < 0)
        {
            fpsEnd = title.Length;
        }

        var displayedFps = title.Substring(fpsStart, fpsEnd - fpsStart).Trim();
        return string.IsNullOrWhiteSpace(displayedFps) ? "0.0" : displayedFps;
    }

    internal static string DescribeElementForWindowTitle(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        return element is FrameworkElement { Name.Length: > 0 } frameworkElement
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (_rootContent != null && ShouldCancelExitRequest(_rootContent))
        {
            args.Cancel = true;
            return;
        }

        _window.ClientSizeChanged -= OnClientSizeChanged;
        _window.NativeWindow.TextInput -= OnTextInput;
        _windowThemeBinding?.Dispose();
        _windowThemeBinding = null;
        _inkkOopsRuntimeService?.Dispose();
        _inkkOopsRuntimeService = null;
        _inkkOopsHost = null;

        _uiCompositeTarget?.Dispose();
        _uiCompositeTarget = null;
        UiDrawing.ReleaseDeviceResources(GraphicsDevice);
        _uiRoot.Shutdown();
        UiApplication.Current.DetachMainWindow(_window);
        _window.Dispose();
        base.OnExiting(sender, args);
    }

    internal static bool ShouldCancelExitRequest(UIElement rootContent)
    {
        ArgumentNullException.ThrowIfNull(rootContent);
        return rootContent is IAppExitRequestHandler exitRequestHandler &&
               !exitRequestHandler.TryRequestAppExit();
    }

    private UIElement CreateRootContent()
    {
        return _rootContentFactory() ?? throw new InvalidOperationException("The root content factory returned null.");
    }

    private InkkOopsHostConfiguration CreateInkkOopsHostConfiguration(Assembly rootContentAssembly)
    {
        var applicationAssembly = _options.ApplicationAssembly ?? rootContentAssembly;
        return InkkOopsHostConfiguration.CreateDefault(
            applicationAssembly,
            _inkkOopsOptions.AdditionalScriptAssemblyPaths);
    }

    private void LoadApplicationResourcesIfPresent()
    {
        if (!_options.LoadApplicationResources)
        {
            return;
        }

        var configuredPath = _options.ApplicationResourcesPath;
        var appMarkupPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "App.xml")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);

        if (File.Exists(appMarkupPath))
        {
            XamlLoader.LoadApplicationResourcesFromFile(appMarkupPath, clearExisting: true);
        }
    }

    private UiRoot CreateUiRoot(Panel root)
    {
        return new UiRoot(root)
        {
            UseRetainedRenderList = !_inkkOopsOptions.DisableRetainedRenderList,
            UseDirtyRegionRendering = !_inkkOopsOptions.DisableDirtyRegionRendering,
            UseConditionalDrawScheduling = true,
            UseSoftwareCursor = false
        };
    }

    private void InitializeInkkOopsRuntime()
    {
        _inkkOopsHost = new InkkOopsGameHost(
            _uiRoot,
            _window,
            EnsureViewportMatchesBackBuffer,
            () => _uiCompositeTarget,
            () => _displayedAppFps,
            ResolveArtifactRoot(),
            _inkkOopsHostConfiguration);

        _inkkOopsRuntimeService = new InkkOopsRuntimeService(
            _inkkOopsOptions,
            _inkkOopsHostConfiguration,
            _inkkOopsHost,
            requestAppExit: result =>
            {
                Environment.ExitCode = InkkOopsExitCodes.FromStatus(result.Status);
                _uiRoot.EnqueueDeferredOperation(Exit);
            });
    }

    private string ResolveArtifactRoot()
    {
        return string.IsNullOrWhiteSpace(_inkkOopsOptions.ArtifactRoot)
            ? _inkkOopsHostConfiguration.DefaultArtifactRoot
            : _inkkOopsOptions.ArtifactRoot;
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
        _uiCompositeTarget = new RenderTarget2D(
            GraphicsDevice,
            Math.Max(1, viewport.Width),
            Math.Max(1, viewport.Height),
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);
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

    private void UpdateWindowTitleWithDiagnostics()
    {
        var hoveredElement = DescribeElementForWindowTitle(_uiRoot.GetHoveredElementForDiagnostics());
        _window.Title = BuildWindowTitle(ExtractBaseWindowTitle(_window.Title), _displayedAppFps, hoveredElement);
    }

    private void UpdateDisplayedFpsFromAppCadence()
    {
        var now = Stopwatch.GetTimestamp();
        if (_appFpsWindowStartTimestamp == 0)
        {
            _appFpsWindowStartTimestamp = now;
        }

        _appFrameCount++;
        var elapsedSeconds = (double)(now - _appFpsWindowStartTimestamp) / Stopwatch.Frequency;
        if (TryComputeDisplayedFps(_appFrameCount, elapsedSeconds, out var displayedFps))
        {
            _displayedAppFps = displayedFps;
            _appFrameCount = 0;
            _appFpsWindowStartTimestamp = now;
        }
    }
}