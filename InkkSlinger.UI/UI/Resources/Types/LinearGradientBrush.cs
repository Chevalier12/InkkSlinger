using System;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class LinearGradientBrush : Brush
{
    private Vector2 _startPoint = Vector2.Zero;
    private Vector2 _endPoint = Vector2.One;
    private GradientStopCollection _gradientStops;

    public LinearGradientBrush()
    {
        _gradientStops = new GradientStopCollection();
        _gradientStops.Changed += OnGradientStopsChanged;
    }

    public Vector2 StartPoint
    {
        get
        {
            ReadPreamble();
            return _startPoint;
        }
        set
        {
            WritePreamble();
            if (_startPoint == value)
            {
                return;
            }

            _startPoint = value;
            WritePostscript();
        }
    }

    public Vector2 EndPoint
    {
        get
        {
            ReadPreamble();
            return _endPoint;
        }
        set
        {
            WritePreamble();
            if (_endPoint == value)
            {
                return;
            }

            _endPoint = value;
            WritePostscript();
        }
    }

    public GradientStopCollection GradientStops
    {
        get
        {
            ReadPreamble();
            return _gradientStops;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            WritePreamble();
            if (ReferenceEquals(_gradientStops, value))
            {
                return;
            }

            _gradientStops.Changed -= OnGradientStopsChanged;
            _gradientStops = value;
            _gradientStops.Changed += OnGradientStopsChanged;
            WritePostscript();
        }
    }

    public override Color ToColor()
    {
        return SampleColor(new LayoutRect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }

    internal override Color SampleColor(LayoutRect bounds, Vector2 point)
    {
        ReadPreamble();

        var stopCount = _gradientStops.Count;
        if (stopCount == 0)
        {
            return Color.Transparent;
        }

        if (stopCount == 1)
        {
            return _gradientStops[0].Color;
        }

        var start = new Vector2(
            bounds.X + (bounds.Width * _startPoint.X),
            bounds.Y + (bounds.Height * _startPoint.Y));
        var end = new Vector2(
            bounds.X + (bounds.Width * _endPoint.X),
            bounds.Y + (bounds.Height * _endPoint.Y));
        var direction = end - start;
        var directionLengthSquared = direction.LengthSquared();
        if (directionLengthSquared <= 0.000001f)
        {
            return _gradientStops[^1].Color;
        }

        var offset = Vector2.Dot(point - start, direction) / directionLengthSquared;
        return InterpolateColor(offset);
    }

    internal override string GetRenderSignature()
    {
        var builder = new StringBuilder();
        builder.Append("linear:");
        builder.Append(_startPoint.X.ToString("R", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(_startPoint.Y.ToString("R", CultureInfo.InvariantCulture));
        builder.Append(';');
        builder.Append(_endPoint.X.ToString("R", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(_endPoint.Y.ToString("R", CultureInfo.InvariantCulture));

        for (var i = 0; i < _gradientStops.Count; i++)
        {
            builder.Append('|');
            builder.Append(_gradientStops[i].GetRenderSignature());
        }

        return builder.ToString();
    }

    protected override Freezable CreateInstanceCore()
    {
        return new LinearGradientBrush();
    }

    protected override void CloneCore(Freezable source)
    {
        if (source is not LinearGradientBrush brush)
        {
            return;
        }

        _startPoint = brush._startPoint;
        _endPoint = brush._endPoint;
        GradientStops = (GradientStopCollection)brush.GradientStops.Clone();
    }

    protected override void CloneCurrentValueCore(Freezable source)
    {
        if (source is not LinearGradientBrush brush)
        {
            return;
        }

        _startPoint = brush._startPoint;
        _endPoint = brush._endPoint;
        GradientStops = (GradientStopCollection)brush.GradientStops.CloneCurrentValue();
    }

    protected override bool FreezeCore(bool isChecking)
    {
        return FreezeValue(_gradientStops, isChecking);
    }

    private Color InterpolateColor(float offset)
    {
        GradientStop? lower = null;
        GradientStop? upper = null;
        for (var i = 0; i < _gradientStops.Count; i++)
        {
            var current = _gradientStops[i];
            if (lower == null || current.Offset > lower.Offset)
            {
                if (current.Offset <= offset)
                {
                    lower = current;
                }
            }

            if (current.Offset >= offset)
            {
                if (upper == null || current.Offset < upper.Offset)
                {
                    upper = current;
                }
            }
        }

        lower ??= GetExtremeStop(useMaximumOffset: false);
        upper ??= GetExtremeStop(useMaximumOffset: true);
        if (lower == null && upper == null)
        {
            return Color.Transparent;
        }

        if (lower == null)
        {
            return upper!.Color;
        }

        if (upper == null)
        {
            return lower.Color;
        }

        var denominator = upper.Offset - lower.Offset;
        if (MathF.Abs(denominator) <= 0.000001f)
        {
            return upper.Color;
        }

        var t = Math.Clamp((offset - lower.Offset) / denominator, 0f, 1f);
        return Color.Lerp(lower.Color, upper.Color, t);
    }

    private GradientStop? GetExtremeStop(bool useMaximumOffset)
    {
        GradientStop? candidate = null;
        for (var i = 0; i < _gradientStops.Count; i++)
        {
            var current = _gradientStops[i];
            if (candidate == null)
            {
                candidate = current;
                continue;
            }

            if (useMaximumOffset)
            {
                if (current.Offset >= candidate.Offset)
                {
                    candidate = current;
                }
            }
            else if (current.Offset <= candidate.Offset)
            {
                candidate = current;
            }
        }

        return candidate;
    }

    private void OnGradientStopsChanged()
    {
        WritePostscript();
    }
}