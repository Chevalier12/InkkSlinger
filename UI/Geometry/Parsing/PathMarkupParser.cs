using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class PathMarkupParser
{
    public static PathGeometry Parse(string data)
    {
        var parser = new Parser(data);
        return parser.Parse();
    }

    private sealed class Parser
    {
        private readonly string _data;
        private int _index;
        private char _command;
        private readonly List<GeometryFigure> _figures = new();
        private readonly List<Vector2> _currentPoints = new();
        private Vector2 _current;
        private Vector2 _figureStart;
        private bool _hasOpenFigure;
        private Vector2? _lastCubicControl;
        private Vector2? _lastQuadraticControl;
        private char _lastCurveCommand;

        public Parser(string data)
        {
            _data = data;
        }

        public PathGeometry Parse()
        {
            while (TryReadCommandOrContinue(out var command))
            {
                _command = command;
                ExecuteCommand();
            }

            CloseOpenFigure(false);
            var geometry = new PathGeometry();
            geometry.Figures.AddRange(_figures);
            return geometry;
        }

        private bool TryReadCommandOrContinue(out char command)
        {
            SkipSeparators();
            if (_index >= _data.Length)
            {
                command = default;
                return false;
            }

            var c = _data[_index];
            if (char.IsLetter(c))
            {
                _index++;
                command = c;
                return true;
            }

            if (_command == default)
            {
                throw new FormatException($"Path data is invalid near '{_data[_index..]}'.");
            }

            command = _command;
            return true;
        }

        private void ExecuteCommand()
        {
            switch (_command)
            {
                case 'M':
                    MoveTo(relative: false);
                    break;
                case 'm':
                    MoveTo(relative: true);
                    break;
                case 'L':
                    LineTo(relative: false);
                    break;
                case 'l':
                    LineTo(relative: true);
                    break;
                case 'H':
                    HorizontalTo(relative: false);
                    break;
                case 'h':
                    HorizontalTo(relative: true);
                    break;
                case 'V':
                    VerticalTo(relative: false);
                    break;
                case 'v':
                    VerticalTo(relative: true);
                    break;
                case 'C':
                    CubicTo(relative: false);
                    break;
                case 'c':
                    CubicTo(relative: true);
                    break;
                case 'S':
                    SmoothCubicTo(relative: false);
                    break;
                case 's':
                    SmoothCubicTo(relative: true);
                    break;
                case 'Q':
                    QuadraticTo(relative: false);
                    break;
                case 'q':
                    QuadraticTo(relative: true);
                    break;
                case 'T':
                    SmoothQuadraticTo(relative: false);
                    break;
                case 't':
                    SmoothQuadraticTo(relative: true);
                    break;
                case 'A':
                    ArcTo(relative: false);
                    break;
                case 'a':
                    ArcTo(relative: true);
                    break;
                case 'Z':
                case 'z':
                    CloseOpenFigure(true);
                    ClearCurveState();
                    break;
                default:
                    throw new FormatException($"Path command '{_command}' is not supported.");
            }
        }

        private void MoveTo(bool relative)
        {
            var first = ReadPoint(relative ? _current : Vector2.Zero, relative);
            CloseOpenFigure(false);
            StartFigure(first);
            ClearCurveState();

            while (TryPeekNumberStart())
            {
                var next = ReadPoint(_current, relative);
                AddPoint(next);
            }
        }

        private void LineTo(bool relative)
        {
            EnsureFigureStarted();
            ClearCurveState();
            while (TryPeekNumberStart())
            {
                var point = ReadPoint(_current, relative);
                AddPoint(point);
            }
        }

        private void HorizontalTo(bool relative)
        {
            EnsureFigureStarted();
            ClearCurveState();
            while (TryPeekNumberStart())
            {
                var x = ReadNumber();
                var point = relative
                    ? new Vector2(_current.X + x, _current.Y)
                    : new Vector2(x, _current.Y);
                AddPoint(point);
            }
        }

        private void VerticalTo(bool relative)
        {
            EnsureFigureStarted();
            ClearCurveState();
            while (TryPeekNumberStart())
            {
                var y = ReadNumber();
                var point = relative
                    ? new Vector2(_current.X, _current.Y + y)
                    : new Vector2(_current.X, y);
                AddPoint(point);
            }
        }

        private void CubicTo(bool relative)
        {
            EnsureFigureStarted();
            while (TryPeekNumberStart())
            {
                var c1 = ReadPoint(_current, relative);
                var c2 = ReadPoint(_current, relative);
                var end = ReadPoint(_current, relative);
                AddBezierCubic(_current, c1, c2, end);
                _current = end;
                _lastCubicControl = c2;
                _lastQuadraticControl = null;
                _lastCurveCommand = 'C';
            }
        }

        private void SmoothCubicTo(bool relative)
        {
            EnsureFigureStarted();
            while (TryPeekNumberStart())
            {
                var c1 = (_lastCurveCommand == 'C' || _lastCurveCommand == 'S') && _lastCubicControl.HasValue
                    ? ReflectControlPoint(_current, _lastCubicControl.Value)
                    : _current;
                var c2 = ReadPoint(_current, relative);
                var end = ReadPoint(_current, relative);
                AddBezierCubic(_current, c1, c2, end);
                _current = end;
                _lastCubicControl = c2;
                _lastQuadraticControl = null;
                _lastCurveCommand = 'S';
            }
        }

        private void QuadraticTo(bool relative)
        {
            EnsureFigureStarted();
            while (TryPeekNumberStart())
            {
                var c = ReadPoint(_current, relative);
                var end = ReadPoint(_current, relative);
                AddBezierQuadratic(_current, c, end);
                _current = end;
                _lastQuadraticControl = c;
                _lastCubicControl = null;
                _lastCurveCommand = 'Q';
            }
        }

        private void SmoothQuadraticTo(bool relative)
        {
            EnsureFigureStarted();
            while (TryPeekNumberStart())
            {
                var control = (_lastCurveCommand == 'Q' || _lastCurveCommand == 'T') && _lastQuadraticControl.HasValue
                    ? ReflectControlPoint(_current, _lastQuadraticControl.Value)
                    : _current;
                var end = ReadPoint(_current, relative);
                AddBezierQuadratic(_current, control, end);
                _current = end;
                _lastQuadraticControl = control;
                _lastCubicControl = null;
                _lastCurveCommand = 'T';
            }
        }

        private void ArcTo(bool relative)
        {
            EnsureFigureStarted();
            ClearCurveState();
            while (TryPeekNumberStart())
            {
                var rx = MathF.Abs(ReadNumber());
                var ry = MathF.Abs(ReadNumber());
                var xAxisRotation = ReadNumber();
                var largeArc = ReadFlag();
                var sweep = ReadFlag();
                var end = ReadPoint(_current, relative);
                AddArc(_current, rx, ry, xAxisRotation, largeArc, sweep, end);
                _current = end;
            }
        }

        private void AddArc(
            Vector2 start,
            float rx,
            float ry,
            float xAxisRotationDegrees,
            bool largeArc,
            bool sweep,
            Vector2 end)
        {
            if (Vector2.DistanceSquared(start, end) <= 0.000001f)
            {
                return;
            }

            if (rx <= 0f || ry <= 0f)
            {
                AddPoint(end);
                return;
            }

            var phi = DegreesToRadians(xAxisRotationDegrees % 360f);
            var cosPhi = MathF.Cos(phi);
            var sinPhi = MathF.Sin(phi);

            var dx2 = (start.X - end.X) * 0.5f;
            var dy2 = (start.Y - end.Y) * 0.5f;
            var x1Prime = (cosPhi * dx2) + (sinPhi * dy2);
            var y1Prime = (-sinPhi * dx2) + (cosPhi * dy2);

            var rxSq = rx * rx;
            var rySq = ry * ry;
            var x1PrimeSq = x1Prime * x1Prime;
            var y1PrimeSq = y1Prime * y1Prime;

            var lambda = (x1PrimeSq / rxSq) + (y1PrimeSq / rySq);
            if (lambda > 1f)
            {
                var scale = MathF.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
                rxSq = rx * rx;
                rySq = ry * ry;
            }

            var numerator = (rxSq * rySq) - (rxSq * y1PrimeSq) - (rySq * x1PrimeSq);
            var denominator = (rxSq * y1PrimeSq) + (rySq * x1PrimeSq);
            var coeff = 0f;
            if (denominator > 0f)
            {
                var ratio = MathF.Max(0f, numerator / denominator);
                coeff = (largeArc == sweep ? -1f : 1f) * MathF.Sqrt(ratio);
            }

            var cxPrime = coeff * ((rx * y1Prime) / ry);
            var cyPrime = coeff * (-(ry * x1Prime) / rx);

            var cx = (cosPhi * cxPrime) - (sinPhi * cyPrime) + ((start.X + end.X) * 0.5f);
            var cy = (sinPhi * cxPrime) + (cosPhi * cyPrime) + ((start.Y + end.Y) * 0.5f);

            var ux = (x1Prime - cxPrime) / rx;
            var uy = (y1Prime - cyPrime) / ry;
            var vx = (-x1Prime - cxPrime) / rx;
            var vy = (-y1Prime - cyPrime) / ry;

            var theta1 = MathF.Atan2(uy, ux);
            var deltaTheta = SignedAngle(ux, uy, vx, vy);

            if (!sweep && deltaTheta > 0f)
            {
                deltaTheta -= MathF.PI * 2f;
            }
            else if (sweep && deltaTheta < 0f)
            {
                deltaTheta += MathF.PI * 2f;
            }

            if (MathF.Abs(deltaTheta) <= 0.000001f)
            {
                AddPoint(end);
                return;
            }

            const float maxArcStepRadians = MathF.PI / 12f;
            var segments = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(deltaTheta) / maxArcStepRadians));
            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var theta = theta1 + (deltaTheta * t);
                var cosTheta = MathF.Cos(theta);
                var sinTheta = MathF.Sin(theta);
                var x = cx + (rx * cosPhi * cosTheta) - (ry * sinPhi * sinTheta);
                var y = cy + (rx * sinPhi * cosTheta) + (ry * cosPhi * sinTheta);
                _currentPoints.Add(new Vector2(x, y));
            }

            _currentPoints[^1] = end;
        }

        private static float SignedAngle(float ux, float uy, float vx, float vy)
        {
            var dot = (ux * vx) + (uy * vy);
            var det = (ux * vy) - (uy * vx);
            return MathF.Atan2(det, dot);
        }

        private static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }

        private static Vector2 ReflectControlPoint(Vector2 around, Vector2 control)
        {
            return new Vector2((2f * around.X) - control.X, (2f * around.Y) - control.Y);
        }

        private void ClearCurveState()
        {
            _lastCubicControl = null;
            _lastQuadraticControl = null;
            _lastCurveCommand = default;
        }

        private void AddBezierQuadratic(Vector2 start, Vector2 control, Vector2 end)
        {
            const int segments = 12;
            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var u = 1f - t;
                var point =
                    (u * u * start) +
                    (2f * u * t * control) +
                    (t * t * end);
                _currentPoints.Add(point);
            }
        }

        private void AddBezierCubic(Vector2 start, Vector2 c1, Vector2 c2, Vector2 end)
        {
            const int segments = 16;
            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var u = 1f - t;
                var point =
                    (u * u * u * start) +
                    (3f * u * u * t * c1) +
                    (3f * u * t * t * c2) +
                    (t * t * t * end);
                _currentPoints.Add(point);
            }
        }

        private void StartFigure(Vector2 point)
        {
            _currentPoints.Clear();
            _currentPoints.Add(point);
            _current = point;
            _figureStart = point;
            _hasOpenFigure = true;
        }

        private void AddPoint(Vector2 point)
        {
            _currentPoints.Add(point);
            _current = point;
        }

        private void CloseOpenFigure(bool closed)
        {
            if (!_hasOpenFigure)
            {
                return;
            }

            if (closed && _currentPoints.Count > 0)
            {
                var first = _currentPoints[0];
                var last = _currentPoints[^1];
                if (Vector2.DistanceSquared(first, last) > 0.001f)
                {
                    _currentPoints.Add(first);
                }
            }

            if (_currentPoints.Count > 1)
            {
                _figures.Add(new GeometryFigure(_currentPoints.ToArray(), closed));
            }

            _current = closed ? _figureStart : _current;
            _currentPoints.Clear();
            _hasOpenFigure = false;
        }

        private void EnsureFigureStarted()
        {
            if (_hasOpenFigure)
            {
                return;
            }

            StartFigure(_current);
        }

        private Vector2 ReadPoint(Vector2 origin, bool relative)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            if (!relative)
            {
                return new Vector2(x, y);
            }

            return new Vector2(origin.X + x, origin.Y + y);
        }

        private bool ReadFlag()
        {
            SkipSeparators();
            if (_index >= _data.Length)
            {
                throw new FormatException("Unexpected end of path data while reading arc flag.");
            }

            var c = _data[_index];
            if (c == '0')
            {
                _index++;
                return false;
            }

            if (c == '1')
            {
                _index++;
                return true;
            }

            throw new FormatException($"Arc flag must be 0 or 1 near '{_data[_index..]}'.");
        }

        private float ReadNumber()
        {
            SkipSeparators();
            if (_index >= _data.Length)
            {
                throw new FormatException("Unexpected end of path data while reading number.");
            }

            var start = _index;
            var hasDecimal = false;
            var hasExponent = false;

            if (_data[_index] == '+' || _data[_index] == '-')
            {
                _index++;
            }

            while (_index < _data.Length)
            {
                var c = _data[_index];
                if (char.IsDigit(c))
                {
                    _index++;
                    continue;
                }

                if (c == '.' && !hasDecimal)
                {
                    hasDecimal = true;
                    _index++;
                    continue;
                }

                if ((c == 'e' || c == 'E') && !hasExponent)
                {
                    hasExponent = true;
                    _index++;
                    if (_index < _data.Length && (_data[_index] == '+' || _data[_index] == '-'))
                    {
                        _index++;
                    }

                    continue;
                }

                break;
            }

            if (start == _index)
            {
                throw new FormatException($"Expected a number near '{_data[_index..]}'.");
            }

            var token = _data[start.._index];
            return float.Parse(token, CultureInfo.InvariantCulture);
        }

        private bool TryPeekNumberStart()
        {
            SkipSeparators();
            if (_index >= _data.Length)
            {
                return false;
            }

            var c = _data[_index];
            return char.IsDigit(c) || c == '-' || c == '+' || c == '.';
        }

        private void SkipSeparators()
        {
            while (_index < _data.Length)
            {
                var c = _data[_index];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    _index++;
                    continue;
                }

                break;
            }
        }
    }
}
