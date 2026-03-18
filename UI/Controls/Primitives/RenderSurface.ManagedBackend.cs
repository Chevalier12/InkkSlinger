using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal interface IRenderSurfaceManagedBackend
{
    IRenderSurfaceManagedSession Create(GraphicsDevice graphicsDevice, int pixelWidth, int pixelHeight);
}

internal interface IRenderSurfaceManagedSession : IDisposable
{
    GraphicsDevice GraphicsDevice { get; }

    int PixelWidth { get; }

    int PixelHeight { get; }

    bool IsDisposed { get; }

    ImageSource Surface { get; }

    void Render(SpriteBatch? uiSpriteBatch, Color clearColor, Action<SpriteBatch, Rectangle> drawCallback);
}

internal sealed class DefaultRenderSurfaceManagedBackend : IRenderSurfaceManagedBackend
{
    private static readonly RasterizerState OffscreenRasterizerState = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true
    };

    public IRenderSurfaceManagedSession Create(GraphicsDevice graphicsDevice, int pixelWidth, int pixelHeight)
    {
        return new DefaultRenderSurfaceManagedSession(graphicsDevice, pixelWidth, pixelHeight);
    }

    private sealed class DefaultRenderSurfaceManagedSession : IRenderSurfaceManagedSession
    {
        private readonly RenderTarget2D _renderTarget;
        private readonly SpriteBatch _spriteBatch;

        public DefaultRenderSurfaceManagedSession(GraphicsDevice graphicsDevice, int pixelWidth, int pixelHeight)
        {
            GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            PixelWidth = pixelWidth > 0
                ? pixelWidth
                : throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            PixelHeight = pixelHeight > 0
                ? pixelHeight
                : throw new ArgumentOutOfRangeException(nameof(pixelHeight));

            _renderTarget = new RenderTarget2D(
                graphicsDevice,
                pixelWidth,
                pixelHeight,
                mipMap: false,
                preferredFormat: SurfaceFormat.Color,
                preferredDepthFormat: DepthFormat.None);
            _spriteBatch = new SpriteBatch(graphicsDevice);
            Surface = ImageSource.FromTexture(_renderTarget);
        }

        public GraphicsDevice GraphicsDevice { get; }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public bool IsDisposed { get; private set; }

        public ImageSource Surface { get; }

        public void Render(SpriteBatch? uiSpriteBatch, Color clearColor, Action<SpriteBatch, Rectangle> drawCallback)
        {
            ArgumentNullException.ThrowIfNull(uiSpriteBatch);
            ArgumentNullException.ThrowIfNull(drawCallback);

            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DefaultRenderSurfaceManagedSession));
            }

            if (!ReferenceEquals(uiSpriteBatch.GraphicsDevice, GraphicsDevice))
            {
                throw new InvalidOperationException("The UI SpriteBatch uses a different GraphicsDevice than the managed surface session.");
            }

            var suspendedState = UiDrawing.SuspendActiveBatch(uiSpriteBatch);
            try
            {
                GraphicsDevice.SetRenderTarget(_renderTarget);
                UiDrawing.ResetState(GraphicsDevice);
                GraphicsDevice.Clear(clearColor);

                _spriteBatch.Begin(
                    sortMode: SpriteSortMode.Deferred,
                    blendState: BlendState.AlphaBlend,
                    samplerState: SamplerState.LinearClamp,
                    depthStencilState: DepthStencilState.None,
                    rasterizerState: OffscreenRasterizerState);
                UiDrawing.SetActiveBatchState(
                    GraphicsDevice,
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    OffscreenRasterizerState);
                try
                {
                    drawCallback(_spriteBatch, new Rectangle(0, 0, PixelWidth, PixelHeight));
                }
                finally
                {
                    UiDrawing.ClearActiveBatchState(GraphicsDevice);
                    _spriteBatch.End();
                }
            }
            finally
            {
                UiDrawing.ResumeSuspendedBatch(uiSpriteBatch, suspendedState);
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _spriteBatch.Dispose();
            _renderTarget.Dispose();
        }
    }
}
