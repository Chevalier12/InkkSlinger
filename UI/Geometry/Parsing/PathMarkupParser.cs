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
                case 'Q':
                    QuadraticTo(relative: false);
                    break;
                case 'q':
                    QuadraticTo(relative: true);
                    break;
                case 'Z':
                case 'z':
                    CloseOpenFigure(true);
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

            while (TryPeekNumberStart())
            {
                var next = ReadPoint(_current, relative);
                AddPoint(next);
            }
        }

        private void LineTo(bool relative)
        {
            EnsureFigureStarted();
            while (TryPeekNumberStart())
            {
                var point = ReadPoint(_current, relative);
                AddPoint(point);
            }
        }

        private void HorizontalTo(bool relative)
        {
            EnsureFigureStarted();
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
            }
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
