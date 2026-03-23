using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace InkkSlinger;

public enum MediaState
{
    Closed,
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped
}

public class MediaElement : Control
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(Uri),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is MediaElement element)
                    {
                        element.OnSourceChanged(args.OldValue as Uri, args.NewValue as Uri);
                    }
                }));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(
            nameof(Volume),
            typeof(float),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(
                0.5f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is MediaElement element)
                    {
                        element.OnVolumeChanged((float)args.OldValue, (float)args.NewValue);
                    }
                }));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(
            nameof(IsMuted),
            typeof(bool),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is MediaElement element)
                    {
                        element.OnIsMutedChanged((bool)args.OldValue, (bool)args.NewValue);
                    }
                }));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Stretch.None));

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(
            nameof(Position),
            typeof(TimeSpan),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(
            nameof(Duration),
            typeof(TimeSpan),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty PlaybackRateProperty =
        DependencyProperty.Register(
            nameof(PlaybackRate),
            typeof(float),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(1.0f));

    public static readonly RoutedEvent MediaOpenedEvent = new(nameof(MediaOpened), RoutingStrategy.Bubble);
    public static readonly RoutedEvent MediaEndedEvent = new(nameof(MediaEnded), RoutingStrategy.Bubble);
    public static readonly RoutedEvent MediaFailedEvent = new(nameof(MediaFailed), RoutingStrategy.Bubble);

    public Uri? Source
    {
        get => GetValue<Uri>(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public float Volume
    {
        get => GetValue<float>(VolumeProperty);
        set => SetValue(VolumeProperty, Math.Clamp(value, 0f, 1f));
    }

    public bool IsMuted
    {
        get => GetValue<bool>(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public TimeSpan Position
    {
        get => GetValue<TimeSpan>(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue<TimeSpan>(DurationProperty);
        private set => SetValue(DurationProperty, value);
    }

    public float PlaybackRate
    {
        get => GetValue<float>(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, Math.Clamp(value, 0f, 10f));
    }

    public MediaState State { get; private set; } = MediaState.Closed;

    public event EventHandler? MediaOpened
    {
        add => AddHandler(MediaOpenedEvent, value);
        remove => RemoveHandler(MediaOpenedEvent, value);
    }

    public event EventHandler? MediaEnded
    {
        add => AddHandler(MediaEndedEvent, value);
        remove => RemoveHandler(MediaEndedEvent, value);
    }

    public event EventHandler? MediaFailed
    {
        add => AddHandler(MediaFailedEvent, value);
        remove => RemoveHandler(MediaFailedEvent, value);
    }

    private Song? _currentSong;
    private Video? _currentVideo;
    private bool _isUpdatingPosition;

    public MediaElement()
    {
        Background = Color.Black;
    }

    private void OnSourceChanged(Uri? oldSource, Uri? newSource)
    {
        Stop();

        if (newSource == null)
        {
            _currentSong = null;
            _currentVideo = null;
            State = MediaState.Closed;
            Duration = TimeSpan.Zero;
            return;
        }

        try
        {
            State = MediaState.Opening;

            // Try to load as Song (audio)
            var path = newSource.LocalPath;
            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".wma", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
            {
                // Note: In a real implementation, we'd need to handle content loading differently
                // For now, we set up the framework
                Duration = TimeSpan.FromMinutes(3); // Placeholder
            }

            State = MediaState.Stopped;
            RaiseRoutedEventInternal(MediaOpenedEvent, new RoutedSimpleEventArgs(MediaOpenedEvent));
        }
        catch (Exception)
        {
            State = MediaState.Closed;
            RaiseRoutedEventInternal(MediaFailedEvent, new RoutedSimpleEventArgs(MediaFailedEvent));
        }
    }

    private void OnVolumeChanged(float oldVolume, float newVolume)
    {
        MediaPlayer.Volume = IsMuted ? 0 : newVolume;
    }

    private void OnIsMutedChanged(bool oldMuted, bool newMuted)
    {
        MediaPlayer.Volume = newMuted ? 0 : Volume;
    }

    public void Play()
    {
        if (_currentSong != null)
        {
            MediaPlayer.Play(_currentSong);
            State = MediaState.Playing;
        }
        else
        {
            // For demo purposes, simulate playback
            State = MediaState.Playing;
        }
    }

    public void Pause()
    {
        if (State == MediaState.Playing)
        {
            MediaPlayer.Pause();
            State = MediaState.Paused;
        }
    }

    public void Stop()
    {
        MediaPlayer.Stop();
        State = MediaState.Stopped;
        Position = TimeSpan.Zero;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (State == MediaState.Playing && !_isUpdatingPosition)
        {
            // Update position from MediaPlayer
            try
            {
                _isUpdatingPosition = true;
                Position = MediaPlayer.PlayPosition;
            }
            catch
            {
                // MediaPlayer.PlayPosition may throw if no media is loaded
            }
            finally
            {
                _isUpdatingPosition = false;
            }

            // Check for media end
            if (MediaPlayer.State == MediaState.Stopped && Duration > TimeSpan.Zero)
            {
                if (Position >= Duration - TimeSpan.FromMilliseconds(500))
                {
                    State = MediaState.Stopped;
                    RaiseRoutedEventInternal(MediaEndedEvent, new RoutedSimpleEventArgs(MediaEndedEvent));
                }
            }
        }
    }

    protected override void OnRender(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
    {
        // Draw background
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);

        // Draw media content placeholder
        // In a full implementation, this would render video frames
        if (State == MediaState.Playing || State == MediaState.Paused)
        {
            // Draw play/pause indicator
            var centerX = LayoutSlot.X + LayoutSlot.Width / 2;
            var centerY = LayoutSlot.Y + LayoutSlot.Height / 2;
            var indicatorSize = Math.Min(LayoutSlot.Width, LayoutSlot.Height) * 0.1f;

            if (State == MediaState.Paused)
            {
                // Draw pause indicator (two bars)
                var barWidth = indicatorSize * 0.3f;
                var barHeight = indicatorSize;
                var barSpacing = indicatorSize * 0.2f;

                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(centerX - barWidth - barSpacing, centerY - barHeight / 2, barWidth, barHeight),
                    Color.White * Opacity);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(centerX + barSpacing, centerY - barHeight / 2, barWidth, barHeight),
                    Color.White * Opacity);
            }
            else
            {
                // Draw play indicator (triangle)
                var triangle = new Vector2[]
                {
                    new Vector2(centerX - indicatorSize / 2, centerY - indicatorSize / 2),
                    new Vector2(centerX - indicatorSize / 2, centerY + indicatorSize / 2),
                    new Vector2(centerX + indicatorSize / 2, centerY)
                };
                // Simplified: draw a small indicator
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(centerX - indicatorSize / 2, centerY - indicatorSize / 2, indicatorSize, indicatorSize),
                    Color.White * Opacity * 0.5f);
            }
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }
}
