using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class RepeatButton : Button
{
    public static readonly DependencyProperty RepeatDelayProperty =
        DependencyProperty.Register(
            nameof(RepeatDelay),
            typeof(float),
            typeof(RepeatButton),
            new FrameworkPropertyMetadata(
                0.35f,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty RepeatIntervalProperty =
        DependencyProperty.Register(
            nameof(RepeatInterval),
            typeof(float),
            typeof(RepeatButton),
            new FrameworkPropertyMetadata(
                0.05f,
                coerceValueCallback: static (_, value) => value is float v && v > 0f ? v : 0.05f));

    private float _heldSeconds;
    private bool _repeatStarted;

    public float RepeatDelay
    {
        get => GetValue<float>(RepeatDelayProperty);
        set => SetValue(RepeatDelayProperty, value);
    }

    public float RepeatInterval
    {
        get => GetValue<float>(RepeatIntervalProperty);
        set => SetValue(RepeatIntervalProperty, value);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var isHeld = IsEnabled && IsPressed;
        if (!isHeld)
        {
            _heldSeconds = 0f;
            _repeatStarted = false;
            return;
        }

        _heldSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!_repeatStarted)
        {
            if (_heldSeconds < RepeatDelay)
            {
                return;
            }

            _repeatStarted = true;
            _heldSeconds -= RepeatDelay;
            OnClick();
        }

        while (_heldSeconds >= RepeatInterval)
        {
            _heldSeconds -= RepeatInterval;
            OnClick();
        }
    }

}
