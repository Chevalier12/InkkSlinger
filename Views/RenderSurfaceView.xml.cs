using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class RenderSurfaceView : UserControl, IUiRootUpdateParticipant
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

    private RenderSurface? _gameSurface;
    private Texture2D? _surfaceTexture;
    private GraphicsDevice? _graphicsDevice;
    private Color[]? _framePixels;
    private bool _hasPendingSurfaceUpload = true;
    private double _totalElapsedSeconds;

    public RenderSurfaceView()
    {
        _viewModel = new SnakeGameViewModel();
        InitializeComponent();
        DataContext = _viewModel;
        _gameSurface = this.FindName("GameSurface") as RenderSurface;
        AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnKeyDown, handledEventsToo: true);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += OnUnloaded;
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => _viewModel.IsRunning || _hasPendingSurfaceUpload;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        _totalElapsedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        _viewModel.Advance(gameTime.ElapsedGameTime);

        // Continuously redraw while running for animated effects (food pulse, glow)
        if (_viewModel.IsRunning)
        {
            _hasPendingSurfaceUpload = true;
        }

        if (_graphicsDevice != null)
        {
            EnsureSurfaceResources(_graphicsDevice);
        }

        TryUploadLatestFrameData();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        EnsureSurfaceResources(spriteBatch.GraphicsDevice);
        base.OnRender(spriteBatch);
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
            _hasPendingSurfaceUpload = true;
        }
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        DisposeSurfaceTexture();
    }

    private void EnsureSurfaceResources(GraphicsDevice graphicsDevice)
    {
        if (_gameSurface == null)
        {
            return;
        }

        var textureWidth = _viewModel.BoardWidth * _viewModel.CellPixelSize;
        var textureHeight = _viewModel.BoardHeight * _viewModel.CellPixelSize;
        if (_surfaceTexture == null ||
            _surfaceTexture.IsDisposed ||
            !ReferenceEquals(_graphicsDevice, graphicsDevice) ||
            _surfaceTexture.Width != textureWidth ||
            _surfaceTexture.Height != textureHeight)
        {
            DisposeSurfaceTexture();
            _graphicsDevice = graphicsDevice;
            _surfaceTexture = new Texture2D(graphicsDevice, textureWidth, textureHeight, false, SurfaceFormat.Color);
            _framePixels = new Color[textureWidth * textureHeight];
            _gameSurface.Present(_surfaceTexture);
            _hasPendingSurfaceUpload = true;
        }
    }

    private void RenderFrame(Color[] pixels, int width, int height)
    {
        // Clear to background
        Array.Fill(pixels, _backgroundColor);

        var cellSize = _viewModel.CellPixelSize;

        // Draw subtle grid dots at cell intersections
        for (var gx = 1; gx < _viewModel.BoardWidth; gx++)
        {
            for (var gy = 1; gy < _viewModel.BoardHeight; gy++)
            {
                var px = gx * cellSize;
                var py = gy * cellSize;
                SetPixelSafe(pixels, width, height, px, py, _gridDotColor);
            }
        }

        // Food glow halo (animated pulse)
        var foodPulse = 0.55 + 0.45 * Math.Sin(_totalElapsedSeconds * 4.0);
        var glowRadius = (int)(cellSize * (0.9 + 0.4 * foodPulse));
        DrawGlow(pixels, width, height, _viewModel.FoodPosition, glowRadius, _foodGlowColor, (float)(0.18 * foodPulse));

        // Food: rounded filled cell
        DrawRoundedCell(pixels, width, height, _viewModel.FoodPosition, _foodCoreColor, 1);

        // Snake segments: gradient from head to tail, connected
        var segments = _viewModel.SnakeSegments;
        var segmentCount = segments.Count;

        // Draw connections between adjacent segments (fills the gap)
        for (var i = 0; i < segmentCount - 1; i++)
        {
            var current = segments[i];
            var next = segments[i + 1];
            var t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            var segColor = LerpColor(_snakeHeadColor, _snakeTailColor, t);
            var tNext = segmentCount > 1 ? (float)(i + 1) / (segmentCount - 1) : 0f;
            var nextColor = LerpColor(_snakeHeadColor, _snakeTailColor, tNext);
            var bridgeColor = LerpColor(segColor, nextColor, 0.5f);
            DrawSegmentBridge(pixels, width, height, current, next, bridgeColor);
        }

        // Draw each segment as a rounded rectangle
        for (var i = segmentCount - 1; i >= 0; i--)
        {
            var t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            var segColor = LerpColor(_snakeHeadColor, _snakeTailColor, t);
            var cornerRadius = i == 0 ? 2 : 1;
            DrawRoundedCell(pixels, width, height, segments[i], segColor, cornerRadius);
        }

        // Head glow
        if (segmentCount > 0)
        {
            DrawGlow(pixels, width, height, segments[0], cellSize, _snakeHeadGlow, 0.10f);
        }

        // Eyes on head
        if (segmentCount > 0)
        {
            DrawEyes(pixels, width, height, segments[0]);
        }

        // Vignette overlay: darken edges
        DrawVignette(pixels, width, height);

        // Subtle border
        DrawBorder(pixels, width, height);

        // Game over darkening overlay
        if (_viewModel.IsGameOver)
        {
            ApplyOverlay(pixels, width, height, new Color(0, 0, 0), 0.4f);
        }
    }

    private void DrawRoundedCell(Color[] pixels, int width, int height, Point cell, Color color, int cornerRadius)
    {
        var cellSize = _viewModel.CellPixelSize;
        var inset = Math.Max(1, cellSize / 8);
        var cx = cell.X * cellSize + inset;
        var cy = cell.Y * cellSize + inset;
        var size = cellSize - (inset * 2);
        var cr = Math.Min(cornerRadius, size / 2);

        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                // Check if pixel is inside rounded rect
                var localX = dx < cr ? cr - dx : dx >= size - cr ? dx - (size - cr - 1) : 0;
                var localY = dy < cr ? cr - dy : dy >= size - cr ? dy - (size - cr - 1) : 0;
                if (localX > 0 && localY > 0 && localX * localX + localY * localY > cr * cr)
                {
                    continue;
                }

                SetPixelSafe(pixels, width, height, cx + dx, cy + dy, color);
            }
        }
    }

    private void DrawSegmentBridge(Color[] pixels, int width, int height, Point a, Point b, Color color)
    {
        var cellSize = _viewModel.CellPixelSize;
        var inset = Math.Max(1, cellSize / 8);
        var size = cellSize - (inset * 2);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1)
        {
            return; // not adjacent
        }

        int rx, ry, rw, rh;
        if (dx == 1) // b is right of a
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
        else if (dy == 1) // b is below a
        {
            rx = a.X * cellSize + inset;
            ry = a.Y * cellSize + inset + size;
            rw = size;
            rh = inset * 2;
        }
        else // dy == -1
        {
            rx = a.X * cellSize + inset;
            ry = b.Y * cellSize + inset + size;
            rw = size;
            rh = inset * 2;
        }

        FillRect(pixels, width, height, rx, ry, rw, rh, color);
    }

    private void DrawGlow(Color[] pixels, int width, int height, Point cell, int radius, Color glowColor, float intensity)
    {
        var cellSize = _viewModel.CellPixelSize;
        var centerX = cell.X * cellSize + cellSize / 2;
        var centerY = cell.Y * cellSize + cellSize / 2;
        var r2 = radius * radius;

        var startX = Math.Max(0, centerX - radius);
        var startY = Math.Max(0, centerY - radius);
        var endX = Math.Min(width, centerX + radius);
        var endY = Math.Min(height, centerY + radius);

        for (var py = startY; py < endY; py++)
        {
            var dy2 = (py - centerY) * (py - centerY);
            var rowOff = py * width;
            for (var px = startX; px < endX; px++)
            {
                var dx2 = (px - centerX) * (px - centerX);
                var dist2 = dx2 + dy2;
                if (dist2 >= r2)
                {
                    continue;
                }

                var falloff = 1.0f - (float)dist2 / r2;
                falloff *= falloff; // quadratic falloff for softer glow
                var alpha = intensity * falloff;
                var idx = rowOff + px;
                pixels[idx] = BlendAdditive(pixels[idx], glowColor, alpha);
            }
        }
    }

    private void DrawEyes(Color[] pixels, int width, int height, Point head)
    {
        var cellSize = _viewModel.CellPixelSize;
        if (cellSize < 10)
        {
            return; // too small for eyes
        }

        var cx = head.X * cellSize + cellSize / 2;
        var cy = head.Y * cellSize + cellSize / 2;
        var eyeOffset = Math.Max(2, cellSize / 6);
        var eyeSize = Math.Max(1, cellSize / 9);

        // Determine direction for eye placement
        var segments = _viewModel.SnakeSegments;
        int dirX = 1, dirY = 0;
        if (segments.Count > 1)
        {
            dirX = head.X - segments[1].X;
            dirY = head.Y - segments[1].Y;
        }

        int eye1X, eye1Y, eye2X, eye2Y;
        if (dirX != 0) // moving horizontally
        {
            eye1X = cx + dirX * (eyeOffset / 2);
            eye1Y = cy - eyeOffset;
            eye2X = cx + dirX * (eyeOffset / 2);
            eye2Y = cy + eyeOffset;
        }
        else // moving vertically
        {
            eye1X = cx - eyeOffset;
            eye1Y = cy + dirY * (eyeOffset / 2);
            eye2X = cx + eyeOffset;
            eye2Y = cy + dirY * (eyeOffset / 2);
        }

        FillCircle(pixels, width, height, eye1X, eye1Y, eyeSize, _eyeColor);
        FillCircle(pixels, width, height, eye2X, eye2Y, eyeSize, _eyeColor);
    }

    private static void FillCircle(Color[] pixels, int width, int height, int cx, int cy, int radius, Color color)
    {
        var r2 = radius * radius;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy <= r2)
                {
                    SetPixelSafe(pixels, width, height, cx + dx, cy + dy, color);
                }
            }
        }
    }

    private void DrawVignette(Color[] pixels, int width, int height)
    {
        var strength = 0.35f;
        var halfW = width * 0.5f;
        var halfH = height * 0.5f;
        var maxDist2 = halfW * halfW + halfH * halfH;

        for (var py = 0; py < height; py++)
        {
            var dy = py - halfH;
            var dy2 = dy * dy;
            var rowOff = py * width;

            // Only apply vignette near edges for performance
            var edgeThreshold = 0.4f;
            var yRatio = dy2 / (halfH * halfH);
            if (yRatio < edgeThreshold)
            {
                // Only process left and right edges of this row
                var edgeCols = (int)(width * 0.25f);
                for (var px = 0; px < edgeCols; px++)
                {
                    var dx = px - halfW;
                    var dist2 = dx * dx + dy2;
                    var ratio = dist2 / maxDist2;
                    if (ratio > edgeThreshold)
                    {
                        var fade = (ratio - edgeThreshold) / (1f - edgeThreshold);
                        fade = Math.Min(1f, fade * fade);
                        var alpha = strength * fade;
                        var idx = rowOff + px;
                        pixels[idx] = BlendOverlay(pixels[idx], _vignetteColor, alpha);
                    }
                }

                for (var px = width - edgeCols; px < width; px++)
                {
                    var dx = px - halfW;
                    var dist2 = dx * dx + dy2;
                    var ratio = dist2 / maxDist2;
                    if (ratio > edgeThreshold)
                    {
                        var fade = (ratio - edgeThreshold) / (1f - edgeThreshold);
                        fade = Math.Min(1f, fade * fade);
                        var alpha = strength * fade;
                        var idx = rowOff + px;
                        pixels[idx] = BlendOverlay(pixels[idx], _vignetteColor, alpha);
                    }
                }

                continue;
            }

            for (var px = 0; px < width; px++)
            {
                var dx = px - halfW;
                var dist2 = dx * dx + dy2;
                var ratio = dist2 / maxDist2;
                if (ratio <= edgeThreshold)
                {
                    continue;
                }

                var fade2 = (ratio - edgeThreshold) / (1f - edgeThreshold);
                fade2 = Math.Min(1f, fade2 * fade2);
                var a = strength * fade2;
                var idx = rowOff + px;
                pixels[idx] = BlendOverlay(pixels[idx], _vignetteColor, a);
            }
        }
    }

    private static void DrawBorder(Color[] pixels, int width, int height)
    {
        var borderColor = new Color(25, 55, 80);

        // Top and bottom - 1px subtle lines
        for (var px = 0; px < width; px++)
        {
            pixels[px] = borderColor;
            pixels[(height - 1) * width + px] = borderColor;
        }

        // Left and right
        for (var py = 0; py < height; py++)
        {
            pixels[py * width] = borderColor;
            pixels[py * width + width - 1] = borderColor;
        }
    }

    private static void ApplyOverlay(Color[] pixels, int width, int height, Color overlayColor, float alpha)
    {
        for (var i = 0; i < width * height; i++)
        {
            pixels[i] = BlendOverlay(pixels[i], overlayColor, alpha);
        }
    }

    private static void SetPixelSafe(Color[] pixels, int width, int height, int x, int y, Color color)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            pixels[y * width + x] = color;
        }
    }

    private static Color BlendAdditive(Color baseColor, Color addColor, float alpha)
    {
        return new Color(
            Math.Min(255, baseColor.R + (int)(addColor.R * alpha)),
            Math.Min(255, baseColor.G + (int)(addColor.G * alpha)),
            Math.Min(255, baseColor.B + (int)(addColor.B * alpha)));
    }

    private static Color BlendOverlay(Color baseColor, Color overlayColor, float alpha)
    {
        var invAlpha = 1f - alpha;
        return new Color(
            (int)(baseColor.R * invAlpha + overlayColor.R * alpha),
            (int)(baseColor.G * invAlpha + overlayColor.G * alpha),
            (int)(baseColor.B * invAlpha + overlayColor.B * alpha));
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static void FillRect(Color[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, Color color)
    {
        if (rectWidth <= 0 || rectHeight <= 0)
        {
            return;
        }

        var startX = Math.Max(0, x);
        var startY = Math.Max(0, y);
        var endX = Math.Min(width, x + rectWidth);
        var endY = Math.Min(height, y + rectHeight);
        for (var row = startY; row < endY; row++)
        {
            var rowOffset = row * width;
            for (var column = startX; column < endX; column++)
            {
                pixels[rowOffset + column] = color;
            }
        }
    }

    private void TryUploadLatestFrameData()
    {
        if (!_hasPendingSurfaceUpload || _framePixels == null || _surfaceTexture == null)
        {
            return;
        }

        RenderFrame(_framePixels, _surfaceTexture.Width, _surfaceTexture.Height);
        _surfaceTexture.SetData(_framePixels);
        _hasPendingSurfaceUpload = false;
        _gameSurface?.RefreshSurface();
    }

    private void DisposeSurfaceTexture()
    {
        _gameSurface?.ClearSurface();
        _surfaceTexture?.Dispose();
        _surfaceTexture = null;
        _graphicsDevice = null;
        _framePixels = null;
        _hasPendingSurfaceUpload = true;
    }
}
