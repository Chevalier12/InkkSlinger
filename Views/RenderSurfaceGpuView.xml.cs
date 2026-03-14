using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class RenderSurfaceGpuView : UserControl, IUiRootUpdateParticipant
{
    private readonly SnakeGameViewModel _viewModel;

    // Background & grid
    private readonly Color _backgroundColor = new(8, 12, 21);
    private readonly Color _gridDotColor = new(18, 30, 45);

    // Snake palette
    private readonly Color _snakeHeadColor = new(130, 255, 120);
    private readonly Color _snakeHeadGlow = new(100, 230, 90);
    private readonly Color _snakeTailColor = new(30, 120, 70);

    // Food palette
    private readonly Color _foodCoreColor = new(255, 90, 80);
    private readonly Color _foodGlowColor = new(255, 60, 50);

    // Vignette
    private readonly Color _vignetteColor = new(4, 6, 12);

    // Eyes
    private readonly Color _eyeColor = new(20, 30, 20);

    // Border
    private readonly Color _borderColor = new(25, 55, 80);

    private readonly RenderSurface? _gameSurface;
    private Texture2D? _pixel;
    private Texture2D? _circleTexture;
    private Texture2D? _radialGlowTexture;
    private Texture2D? _roundedRect1Texture;
    private Texture2D? _roundedRect2Texture;
    private Texture2D? _vignetteTexture;
    private int _roundedRectSize;
    private bool _needsRedraw = true;
    private double _totalElapsedSeconds;

    public RenderSurfaceGpuView()
    {
        _viewModel = new SnakeGameViewModel();
        InitializeComponent();
        DataContext = _viewModel;

        _gameSurface = this.FindName("GameSurface") as RenderSurface;
        if (_gameSurface != null)
        {
            _gameSurface.DrawSurface += OnDrawGameSurface;
        }

        AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnKeyDown, handledEventsToo: true);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += OnUnloaded;
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => _viewModel.IsRunning || _needsRedraw;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        _totalElapsedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        _viewModel.Advance(gameTime.ElapsedGameTime);

        if (_viewModel.IsRunning)
        {
            _needsRedraw = true;
        }

        if (_needsRedraw)
        {
            _gameSurface?.InvalidateVisual();
        }
    }

    private void OnDrawGameSurface(SpriteBatch spriteBatch, Rectangle bounds)
    {
        EnsureTextures(spriteBatch.GraphicsDevice);
        if (_pixel == null)
        {
            return;
        }

        var cellSize = _viewModel.CellPixelSize;
        var boardPixelW = _viewModel.BoardWidth * cellSize;
        var boardPixelH = _viewModel.BoardHeight * cellSize;
        var scaleX = (float)bounds.Width / boardPixelW;
        var scaleY = (float)bounds.Height / boardPixelH;

        // Background
        spriteBatch.Draw(_pixel, bounds, _backgroundColor);

        // Grid dots
        for (var gx = 1; gx < _viewModel.BoardWidth; gx++)
        {
            for (var gy = 1; gy < _viewModel.BoardHeight; gy++)
            {
                DrawRect(spriteBatch, bounds, scaleX, scaleY, gx * cellSize, gy * cellSize, 1, 1, _gridDotColor);
            }
        }

        // Switch to additive blending for glow (matching CPU BlendAdditive)
        spriteBatch.End();
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.Additive,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: RasterizerState.CullNone);

        // Food glow
        var foodPulse = 0.55f + 0.45f * (float)Math.Sin(_totalElapsedSeconds * 4.0);
        var glowRadius = (int)(cellSize * (0.9f + 0.4f * foodPulse));
        DrawGlow(spriteBatch, bounds, scaleX, scaleY, _viewModel.FoodPosition, glowRadius, _foodGlowColor, 0.18f * foodPulse);

        // Switch back to alpha blend for solid elements
        spriteBatch.End();
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: RasterizerState.CullNone);

        // Food cell
        DrawRoundedCell(spriteBatch, bounds, scaleX, scaleY, _viewModel.FoodPosition, _foodCoreColor, 1);

        // Snake segments
        var segments = _viewModel.SnakeSegments;
        var segmentCount = segments.Count;

        // Bridges between adjacent segments
        for (var i = 0; i < segmentCount - 1; i++)
        {
            var current = segments[i];
            var next = segments[i + 1];
            var t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            var segColor = LerpColor(_snakeHeadColor, _snakeTailColor, t);
            var tNext = segmentCount > 1 ? (float)(i + 1) / (segmentCount - 1) : 0f;
            var nextColor = LerpColor(_snakeHeadColor, _snakeTailColor, tNext);
            var bridgeColor = LerpColor(segColor, nextColor, 0.5f);
            DrawSegmentBridge(spriteBatch, bounds, scaleX, scaleY, current, next, bridgeColor);
        }

        // Segment cells (back to front so head draws on top)
        for (var i = segmentCount - 1; i >= 0; i--)
        {
            var t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            var segColor = LerpColor(_snakeHeadColor, _snakeTailColor, t);
            var cornerRadius = i == 0 ? 2 : 1;
            DrawRoundedCell(spriteBatch, bounds, scaleX, scaleY, segments[i], segColor, cornerRadius);
        }

        // Head glow (additive)
        if (segmentCount > 0)
        {
            spriteBatch.End();
            spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.Additive,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone);

            DrawGlow(spriteBatch, bounds, scaleX, scaleY, segments[0], cellSize, _snakeHeadGlow, 0.10f);

            spriteBatch.End();
            spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone);

            // Eyes (alpha blend)
            DrawEyes(spriteBatch, bounds, scaleX, scaleY, segments[0]);
        }

        // Vignette (alpha blend — matches CPU BlendOverlay which is standard lerp)
        DrawVignette(spriteBatch, bounds);

        // Border
        DrawBorder(spriteBatch, bounds, scaleX, scaleY, boardPixelW, boardPixelH);

        // Game over overlay (alpha blend — matches CPU BlendOverlay)
        if (_viewModel.IsGameOver)
        {
            spriteBatch.Draw(_pixel, bounds, new Color(0, 0, 0) * 0.4f);
        }

        _needsRedraw = false;
    }

    private void DrawRoundedCell(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, Point cell, Color color, int cornerRadius)
    {
        var cellSize = _viewModel.CellPixelSize;
        var inset = Math.Max(1, cellSize / 8);
        var cx = cell.X * cellSize + inset;
        var cy = cell.Y * cellSize + inset;
        var size = cellSize - (inset * 2);
        var cr = Math.Min(cornerRadius, size / 2);

        var texture = cr == 2 ? _roundedRect2Texture : _roundedRect1Texture;
        if (cr >= 1 && texture != null)
        {
            var destX = bounds.X + (int)(cx * scaleX);
            var destY = bounds.Y + (int)(cy * scaleY);
            var destW = Math.Max(1, (int)(size * scaleX));
            var destH = Math.Max(1, (int)(size * scaleY));
            spriteBatch.Draw(texture, new Rectangle(destX, destY, destW, destH), color);
        }
        else
        {
            DrawRect(spriteBatch, bounds, scaleX, scaleY, cx, cy, size, size, color);
        }
    }

    private void DrawSegmentBridge(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, Point a, Point b, Color color)
    {
        var cellSize = _viewModel.CellPixelSize;
        var inset = Math.Max(1, cellSize / 8);
        var size = cellSize - (inset * 2);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1)
        {
            return;
        }

        int rx;
        int ry;
        int rw;
        int rh;
        if (dx == 1)
        {
            rx = a.X * cellSize + inset + size;
            ry = a.Y * cellSize + inset;
            rw = inset * 2;
            rh = size;
        }
        else if (dx == -1)
        {
            rx = b.X * cellSize + inset + size;
            ry = a.Y * cellSize + inset;
            rw = inset * 2;
            rh = size;
        }
        else if (dy == 1)
        {
            rx = a.X * cellSize + inset;
            ry = a.Y * cellSize + inset + size;
            rw = size;
            rh = inset * 2;
        }
        else
        {
            rx = a.X * cellSize + inset;
            ry = b.Y * cellSize + inset + size;
            rw = size;
            rh = inset * 2;
        }

        DrawRect(spriteBatch, bounds, scaleX, scaleY, rx, ry, rw, rh, color);
    }

    private void DrawGlow(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, Point cell, int radius, Color glowColor, float intensity)
    {
        if (_radialGlowTexture == null)
        {
            return;
        }

        var cellSize = _viewModel.CellPixelSize;
        var centerX = cell.X * cellSize + cellSize / 2;
        var centerY = cell.Y * cellSize + cellSize / 2;

        var x = centerX - radius;
        var y = centerY - radius;
        var size = radius * 2;

        var destX = bounds.X + (int)(x * scaleX);
        var destY = bounds.Y + (int)(y * scaleY);
        var destW = Math.Max(1, (int)(size * scaleX));
        var destH = Math.Max(1, (int)(size * scaleY));

        spriteBatch.Draw(_radialGlowTexture, new Rectangle(destX, destY, destW, destH), glowColor * intensity);
    }

    private void DrawEyes(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, Point head)
    {
        if (_circleTexture == null)
        {
            return;
        }

        var cellSize = _viewModel.CellPixelSize;
        if (cellSize < 10)
        {
            return;
        }

        var cx = head.X * cellSize + cellSize / 2;
        var cy = head.Y * cellSize + cellSize / 2;
        var eyeOffset = Math.Max(2, cellSize / 6);
        var eyeSize = Math.Max(1, cellSize / 9);

        var segments = _viewModel.SnakeSegments;
        var dirX = 1;
        var dirY = 0;
        if (segments.Count > 1)
        {
            dirX = head.X - segments[1].X;
            dirY = head.Y - segments[1].Y;
        }

        int eye1X;
        int eye1Y;
        int eye2X;
        int eye2Y;
        if (dirX != 0)
        {
            eye1X = cx + dirX * (eyeOffset / 2);
            eye1Y = cy - eyeOffset;
            eye2X = cx + dirX * (eyeOffset / 2);
            eye2Y = cy + eyeOffset;
        }
        else
        {
            eye1X = cx - eyeOffset;
            eye1Y = cy + dirY * (eyeOffset / 2);
            eye2X = cx + eyeOffset;
            eye2Y = cy + dirY * (eyeOffset / 2);
        }

        DrawCircle(spriteBatch, bounds, scaleX, scaleY, eye1X, eye1Y, eyeSize, _eyeColor);
        DrawCircle(spriteBatch, bounds, scaleX, scaleY, eye2X, eye2Y, eyeSize, _eyeColor);
    }

    private void DrawCircle(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, int cx, int cy, int radius, Color color)
    {
        var x = cx - radius;
        var y = cy - radius;
        var size = radius * 2 + 1;

        var destX = bounds.X + (int)(x * scaleX);
        var destY = bounds.Y + (int)(y * scaleY);
        var destW = Math.Max(1, (int)(size * scaleX));
        var destH = Math.Max(1, (int)(size * scaleY));

        spriteBatch.Draw(_circleTexture!, new Rectangle(destX, destY, destW, destH), color);
    }

    private void DrawVignette(SpriteBatch spriteBatch, Rectangle bounds)
    {
        if (_vignetteTexture != null)
        {
            spriteBatch.Draw(_vignetteTexture, bounds, Color.White);
        }
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, int boardW, int boardH)
    {
        DrawRect(spriteBatch, bounds, scaleX, scaleY, 0, 0, boardW, 1, _borderColor);
        DrawRect(spriteBatch, bounds, scaleX, scaleY, 0, boardH - 1, boardW, 1, _borderColor);
        DrawRect(spriteBatch, bounds, scaleX, scaleY, 0, 0, 1, boardH, _borderColor);
        DrawRect(spriteBatch, bounds, scaleX, scaleY, boardW - 1, 0, 1, boardH, _borderColor);
    }

    private void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, float scaleX, float scaleY, int x, int y, int w, int h, Color color)
    {
        var destX = bounds.X + (int)(x * scaleX);
        var destY = bounds.Y + (int)(y * scaleY);
        var destW = Math.Max(1, (int)(w * scaleX));
        var destH = Math.Max(1, (int)(h * scaleY));
        spriteBatch.Draw(_pixel!, new Rectangle(destX, destY, destW, destH), color);
    }

    private void OnKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;
        if (_viewModel.HandleKeyInput(args.Key))
        {
            args.Handled = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        _ = sender;
        if (string.Equals(args.PropertyName, nameof(SnakeGameViewModel.BoardVersion), StringComparison.Ordinal))
        {
            _needsRedraw = true;
            _gameSurface?.InvalidateVisual();
        }
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (_gameSurface != null)
        {
            _gameSurface.DrawSurface -= OnDrawGameSurface;
        }

        DisposeTextures();
    }

    private void EnsureTextures(GraphicsDevice graphicsDevice)
    {
        if (_pixel != null && !_pixel.IsDisposed)
        {
            return;
        }

        DisposeTextures();

        _pixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _pixel.SetData([Color.White]);

        _circleTexture = CreateCircleTexture(graphicsDevice, 64);
        _radialGlowTexture = CreateRadialGlowTexture(graphicsDevice, 128);

        var cellSize = _viewModel.CellPixelSize;
        var inset = Math.Max(1, cellSize / 8);
        _roundedRectSize = cellSize - (inset * 2);
        _roundedRect1Texture = CreateRoundedRectTexture(graphicsDevice, _roundedRectSize, Math.Min(1, _roundedRectSize / 2));
        _roundedRect2Texture = CreateRoundedRectTexture(graphicsDevice, _roundedRectSize, Math.Min(2, _roundedRectSize / 2));

        _vignetteTexture = CreateVignetteTexture(graphicsDevice, 256, 256, _vignetteColor, 0.35f);
    }

    private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int diameter)
    {
        var texture = new Texture2D(graphicsDevice, diameter, diameter, false, SurfaceFormat.Color);
        var pixels = new Color[diameter * diameter];
        var center = diameter / 2f;
        var radius = center;

        for (var py = 0; py < diameter; py++)
        {
            var dy = py - center + 0.5f;
            for (var px = 0; px < diameter; px++)
            {
                var dx = px - center + 0.5f;
                var dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    var alpha = Math.Clamp(radius - dist, 0f, 1f);
                    pixels[py * diameter + px] = Color.White * alpha;
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateRadialGlowTexture(GraphicsDevice graphicsDevice, int size)
    {
        var texture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Color);
        var pixels = new Color[size * size];
        var center = size / 2f;
        var r2 = center * center;

        for (var py = 0; py < size; py++)
        {
            var dy = py - center + 0.5f;
            for (var px = 0; px < size; px++)
            {
                var dx = px - center + 0.5f;
                var dist2 = dx * dx + dy * dy;
                if (dist2 < r2)
                {
                    var falloff = 1f - dist2 / r2;
                    falloff *= falloff;
                    var alpha = (byte)(falloff * 255);
                    pixels[py * size + px] = new Color(alpha, alpha, alpha, alpha);
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateRoundedRectTexture(GraphicsDevice graphicsDevice, int size, int cornerRadius)
    {
        var texture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Color);
        var pixels = new Color[size * size];
        var cr = Math.Min(cornerRadius, size / 2);

        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                var localX = dx < cr ? cr - dx : dx >= size - cr ? dx - (size - cr - 1) : 0;
                var localY = dy < cr ? cr - dy : dy >= size - cr ? dy - (size - cr - 1) : 0;
                if (localX > 0 && localY > 0 && localX * localX + localY * localY > cr * cr)
                {
                    continue;
                }

                pixels[dy * size + dx] = Color.White;
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateVignetteTexture(GraphicsDevice graphicsDevice, int width, int height, Color vignetteColor, float strength)
    {
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        var halfW = width * 0.5f;
        var halfH = height * 0.5f;
        var maxDist2 = halfW * halfW + halfH * halfH;
        var edgeThreshold = 0.4f;

        for (var py = 0; py < height; py++)
        {
            var dy = py - halfH + 0.5f;
            var dy2 = dy * dy;
            for (var px = 0; px < width; px++)
            {
                var dx = px - halfW + 0.5f;
                var dist2 = dx * dx + dy2;
                var ratio = dist2 / maxDist2;
                if (ratio <= edgeThreshold)
                {
                    continue;
                }

                var fade = (ratio - edgeThreshold) / (1f - edgeThreshold);
                fade = Math.Min(1f, fade * fade);
                var alpha = strength * fade;
                pixels[py * width + px] = vignetteColor * alpha;
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private void DisposeTextures()
    {
        _pixel?.Dispose();
        _pixel = null;
        _circleTexture?.Dispose();
        _circleTexture = null;
        _radialGlowTexture?.Dispose();
        _radialGlowTexture = null;
        _roundedRect1Texture?.Dispose();
        _roundedRect1Texture = null;
        _roundedRect2Texture?.Dispose();
        _roundedRect2Texture = null;
        _vignetteTexture?.Dispose();
        _vignetteTexture = null;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
