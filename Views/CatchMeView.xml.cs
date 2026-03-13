using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class CatchMeView : UserControl
{
    private static readonly Random Random = new();

    private Canvas? _playfield;
    private FrameworkElement? _runner;

    public CatchMeView()
    {
        InitializeComponent();

        _playfield = this.FindName("Playfield") as Canvas;
        _runner = this.FindName("RunnerEllipse") as FrameworkElement;

        if (_runner != null)
        {
            _runner.AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, OnRunnerMouseEnter);
        }
    }

    private void OnRunnerMouseEnter(object? sender, MouseRoutedEventArgs args)
    {
        MoveRunnerAway(args.Position);
    }

    private void MoveRunnerAway(Vector2 pointerPosition)
    {
        if (_playfield == null || _runner == null)
        {
            return;
        }

        var runnerWidth = ResolveRunnerDimension(_runner.Width, fallback: 72f);
        var runnerHeight = ResolveRunnerDimension(_runner.Height, fallback: 72f);
        var availableWidth = MathF.Max(0f, _playfield.ActualWidth);
        var availableHeight = MathF.Max(0f, _playfield.ActualHeight);
        if (availableWidth <= runnerWidth || availableHeight <= runnerHeight)
        {
            return;
        }

        var currentLeft = Canvas.GetLeft(_runner);
        if (float.IsNaN(currentLeft))
        {
            currentLeft = 0f;
        }

        var currentTop = Canvas.GetTop(_runner);
        if (float.IsNaN(currentTop))
        {
            currentTop = 0f;
        }

        var runnerCenter = new Vector2(currentLeft + (runnerWidth / 2f), currentTop + (runnerHeight / 2f));
        var pointerLocal = new Vector2(
            pointerPosition.X - _playfield.LayoutSlot.X,
            pointerPosition.Y - _playfield.LayoutSlot.Y);

        var awayDirection = runnerCenter - pointerLocal;
        if (awayDirection.LengthSquared() < 0.01f)
        {
            awayDirection = RandomDirection();
        }
        else
        {
            awayDirection = Vector2.Normalize(awayDirection);
        }

        var jumpDistance = MathF.Max(60f, MathF.Min(availableWidth, availableHeight) * 0.42f);
        jumpDistance *= 0.8f + ((float)Random.NextDouble() * 0.35f);
        var jitter = new Vector2(
            ((float)Random.NextDouble() - 0.5f) * 70f,
            ((float)Random.NextDouble() - 0.5f) * 70f);

        var targetCenter = runnerCenter + (awayDirection * jumpDistance) + jitter;
        var minCenterX = runnerWidth / 2f;
        var maxCenterX = availableWidth - (runnerWidth / 2f);
        var minCenterY = runnerHeight / 2f;
        var maxCenterY = availableHeight - (runnerHeight / 2f);
        targetCenter.X = Math.Clamp(targetCenter.X, minCenterX, maxCenterX);
        targetCenter.Y = Math.Clamp(targetCenter.Y, minCenterY, maxCenterY);

        var targetLeft = targetCenter.X - (runnerWidth / 2f);
        var targetTop = targetCenter.Y - (runnerHeight / 2f);

        if (MathF.Abs(targetLeft - currentLeft) < 6f && MathF.Abs(targetTop - currentTop) < 6f)
        {
            targetLeft = Math.Clamp(targetLeft + (((float)Random.NextDouble() - 0.5f) * 80f), 0f, availableWidth - runnerWidth);
            targetTop = Math.Clamp(targetTop + (((float)Random.NextDouble() - 0.5f) * 80f), 0f, availableHeight - runnerHeight);
        }

        var storyboard = new Storyboard();

        var leftAnimation = new DoubleAnimation
        {
            To = targetLeft,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetName(leftAnimation, "RunnerEllipse");
        Storyboard.SetTargetProperty(leftAnimation, "(Canvas.Left)");
        storyboard.Children.Add(leftAnimation);

        var topAnimation = new DoubleAnimation
        {
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetName(topAnimation, "RunnerEllipse");
        Storyboard.SetTargetProperty(topAnimation, "(Canvas.Top)");
        storyboard.Children.Add(topAnimation);

        storyboard.Begin(this);
    }

    private static float ResolveRunnerDimension(float dimension, float fallback)
    {
        return float.IsNaN(dimension) || dimension <= 0f
            ? fallback
            : dimension;
    }

    private static Vector2 RandomDirection()
    {
        var angle = (float)(Random.NextDouble() * MathF.Tau);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}



