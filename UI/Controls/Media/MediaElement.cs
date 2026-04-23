using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum MediaElementState
{
    Closed,
    Opening,
    Stopped,
    Playing,
    Paused
}

public enum MediaState
{
    Manual,
    Play,
    Close,
    Stop,
    Pause
}

public sealed class MediaFailedEventArgs : EventArgs
{
    public MediaFailedEventArgs(string message, Exception? errorException = null)
    {
        ErrorMessage = message;
        ErrorException = errorException;
    }

    public string ErrorMessage { get; }

    public Exception? ErrorException { get; }
}

public class MediaElement : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(Uri),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                static (d, e) => ((MediaElement)d).OnSourceChanged((Uri?)e.OldValue, (Uri?)e.NewValue)));

    public static readonly DependencyProperty LoadedBehaviorProperty =
        DependencyProperty.Register(
            nameof(LoadedBehavior),
            typeof(MediaState),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Play));

    public static readonly DependencyProperty UnloadedBehaviorProperty =
        DependencyProperty.Register(
            nameof(UnloadedBehavior),
            typeof(MediaState),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(MediaState.Close));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(
            nameof(Volume),
            typeof(float),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(0.5f));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(
            nameof(IsMuted),
            typeof(bool),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(
            nameof(Position),
            typeof(TimeSpan),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(MediaElement),
            new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsRender));

    private static Texture2D? _fallbackTexture;
    private MediaElementState _state = MediaElementState.Closed;
    private TimeSpan _naturalDuration = TimeSpan.Zero;

    public Uri? Source
    {
        get => GetValue<Uri?>(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public MediaState LoadedBehavior
    {
        get => GetValue<MediaState>(LoadedBehaviorProperty);
        set => SetValue(LoadedBehaviorProperty, value);
    }

    public MediaState UnloadedBehavior
    {
        get => GetValue<MediaState>(UnloadedBehaviorProperty);
        set => SetValue(UnloadedBehaviorProperty, value);
    }

    public float Volume
    {
        get => GetValue<float>(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public bool IsMuted
    {
        get => GetValue<bool>(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public TimeSpan Position
    {
        get => GetValue<TimeSpan>(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public bool ScrubbingEnabled { get; set; }

    public float Balance { get; set; }

    public float SpeedRatio { get; set; } = 1f;

    public bool CanPause => _state == MediaElementState.Playing || _state == MediaElementState.Paused;

    public bool HasAudio => false;

    public bool HasVideo => Source != null;

    public TimeSpan NaturalDuration => _naturalDuration;

    public MediaElementState CurrentState => _state;

    public event EventHandler? MediaOpened;

    public event EventHandler<MediaFailedEventArgs>? MediaFailed;

    public event EventHandler? MediaEnded;

    public void Play()
    {
        if (Source == null)
        {
            RaiseMediaFailed("Cannot play when Source is null.");
            return;
        }

        if (_state == MediaElementState.Closed || _state == MediaElementState.Opening)
        {
            _state = MediaElementState.Stopped;
            MediaOpened?.Invoke(this, EventArgs.Empty);
        }

        _state = MediaElementState.Playing;
        InvalidateVisual();
    }

    public void Pause()
    {
        if (_state == MediaElementState.Playing)
        {
            _state = MediaElementState.Paused;
            InvalidateVisual();
        }
    }

    public void Stop()
    {
        if (_state == MediaElementState.Closed)
        {
            return;
        }

        Position = TimeSpan.Zero;
        _state = MediaElementState.Stopped;
        InvalidateVisual();
    }

    public void Close()
    {
        Position = TimeSpan.Zero;
        _naturalDuration = TimeSpan.Zero;
        _state = MediaElementState.Closed;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(MathF.Max(0f, availableSize.X), MathF.Max(0f, availableSize.Y));
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return;
        }

        var texture = GetFallbackTexture(spriteBatch.GraphicsDevice);
        if (texture == null)
        {
            return;
        }

        var color = _state == MediaElementState.Playing ? new Color(28, 28, 28) : new Color(20, 20, 20);
        spriteBatch.Draw(texture, new Rectangle((int)slot.X, (int)slot.Y, (int)slot.Width, (int)slot.Height), color * Opacity);
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);

        if (newParent == null)
        {
            ApplyBehavior(UnloadedBehavior);
            return;
        }

        ApplyBehavior(LoadedBehavior);
    }

    private void OnSourceChanged(Uri? oldValue, Uri? newValue)
    {
        if (Equals(oldValue, newValue))
        {
            return;
        }

        Position = TimeSpan.Zero;
        _naturalDuration = TimeSpan.Zero;
        _state = newValue == null ? MediaElementState.Closed : MediaElementState.Opening;
        InvalidateVisual();
    }

    private void ApplyBehavior(MediaState behavior)
    {
        switch (behavior)
        {
            case MediaState.Play:
                Play();
                break;
            case MediaState.Pause:
                Pause();
                break;
            case MediaState.Stop:
                Stop();
                break;
            case MediaState.Close:
                Close();
                break;
            default:
                break;
        }
    }

    private static Texture2D? GetFallbackTexture(GraphicsDevice graphicsDevice)
    {
        if (_fallbackTexture != null && !_fallbackTexture.IsDisposed)
        {
            return _fallbackTexture;
        }

        _fallbackTexture = new Texture2D(graphicsDevice, 1, 1);
        _fallbackTexture.SetData(new[] { Color.White });
        return _fallbackTexture;
    }

    private void RaiseMediaFailed(string message, Exception? ex = null)
    {
        MediaFailed?.Invoke(this, new MediaFailedEventArgs(message, ex));
    }
}
