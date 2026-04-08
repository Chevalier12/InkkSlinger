using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class SnakeGameViewModel : ObservableObject
{
    private const double StepDurationMilliseconds = 140d;
    private const int InitialSnakeLength = 4;
    private const int MaxQueuedDirections = 2;

    private readonly Random _random;
    private readonly List<Point> _snakeSegments = [];
    private readonly List<Direction> _queuedDirections = [];
    private Direction _direction = Direction.Right;
    private Point _foodPosition;
    private double _accumulatedStepMilliseconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    private int _score;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BestScoreText))]
    private int _bestScore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(PauseButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(PauseButtonText))]
    [NotifyCanExecuteChangedFor(nameof(TogglePauseCommand))]
    private bool _isGameOver;

    [ObservableProperty]
    private int _boardVersion;

    public SnakeGameViewModel()
        : this(new Random(73421))
    {
    }

    internal SnakeGameViewModel(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        RestartGame();
    }

    public int BoardWidth => 20;

    public int BoardHeight => 14;

    public int CellPixelSize => 18;

    public string ScoreText => $"Score: {Score}";

    public string BestScoreText => $"Best: {BestScore}";

    public string StatusText => IsGameOver
        ? "Game over. Press Restart or R."
        : IsRunning
            ? "Running. Click the board, use arrows or WASD."
            : "Paused. Press Space to resume.";

    public string PauseButtonText => IsRunning ? "Pause" : "Resume";

    public string HintText => "Arrows/WASD steer. Space pauses. R restarts.";

    public IReadOnlyList<Point> SnakeSegments => _snakeSegments;

    public Point FoodPosition => _foodPosition;

    public Point HeadPosition => _snakeSegments.Count > 0 ? _snakeSegments[0] : Point.Zero;

    public int SnakeLength => _snakeSegments.Count;

    public void Advance(TimeSpan elapsed)
    {
        if (!IsRunning)
        {
            return;
        }

        _accumulatedStepMilliseconds += Math.Max(0d, elapsed.TotalMilliseconds);
        while (_accumulatedStepMilliseconds >= StepDurationMilliseconds && IsRunning)
        {
            _accumulatedStepMilliseconds -= StepDurationMilliseconds;
            StepSnake();
        }
    }

    public bool HandleKeyInput(Keys key)
    {
        switch (key)
        {
            case Keys.Up:
            case Keys.W:
                return TryQueueDirection(Direction.Up);
            case Keys.Down:
            case Keys.S:
                return TryQueueDirection(Direction.Down);
            case Keys.Left:
            case Keys.A:
                return TryQueueDirection(Direction.Left);
            case Keys.Right:
            case Keys.D:
                return TryQueueDirection(Direction.Right);
            case Keys.Space:
                if (TogglePauseCommand.CanExecute(null))
                {
                    TogglePauseCommand.Execute(null);
                }

                return true;
            case Keys.R:
            case Keys.Enter:
                RestartGameCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private void RestartGame()
    {
        _snakeSegments.Clear();
        var centerX = BoardWidth / 2;
        var centerY = BoardHeight / 2;
        for (var index = 0; index < InitialSnakeLength; index++)
        {
            _snakeSegments.Add(new Point(centerX - index, centerY));
        }

        _direction = Direction.Right;
        _queuedDirections.Clear();
        _accumulatedStepMilliseconds = 0d;
        Score = 0;
        IsGameOver = false;
        IsRunning = true;
        SpawnFood();
        BoardVersion++;
        TogglePauseCommand.NotifyCanExecuteChanged();
    }

    private bool CanTogglePause()
    {
        return !IsGameOver;
    }

    [RelayCommand(CanExecute = nameof(CanTogglePause))]
    private void TogglePause()
    {
        if (IsGameOver)
        {
            return;
        }

        IsRunning = !IsRunning;
    }

    private bool TryQueueDirection(Direction direction)
    {
        var previousPlannedDirection = _queuedDirections.Count > 0
            ? _queuedDirections[^1]
            : _direction;

        if (direction == previousPlannedDirection)
        {
            return false;
        }

        if (_snakeSegments.Count > 1 && IsOpposite(previousPlannedDirection, direction))
        {
            return false;
        }

        if (_queuedDirections.Count >= MaxQueuedDirections)
        {
            return false;
        }

        _queuedDirections.Add(direction);
        return true;
    }

    private void StepSnake()
    {
        if (_queuedDirections.Count > 0)
        {
            _direction = _queuedDirections[0];
            _queuedDirections.RemoveAt(0);
        }

        var nextHead = new Point(
            HeadPosition.X + GetStepX(_direction),
            HeadPosition.Y + GetStepY(_direction));

        if (nextHead.X < 0 || nextHead.X >= BoardWidth || nextHead.Y < 0 || nextHead.Y >= BoardHeight)
        {
            EndGame();
            return;
        }

        var willGrow = nextHead == _foodPosition;
        for (var index = 0; index < _snakeSegments.Count; index++)
        {
            var isTailCellThatWillMove = index == _snakeSegments.Count - 1 && !willGrow;
            if (!isTailCellThatWillMove && _snakeSegments[index] == nextHead)
            {
                EndGame();
                return;
            }
        }

        _snakeSegments.Insert(0, nextHead);
        if (willGrow)
        {
            Score++;
            if (Score > BestScore)
            {
                BestScore = Score;
            }

            if (_snakeSegments.Count >= BoardWidth * BoardHeight)
            {
                IsRunning = false;
                IsGameOver = true;
                BoardVersion++;
                TogglePauseCommand.NotifyCanExecuteChanged();
                return;
            }

            SpawnFood();
        }
        else
        {
            _snakeSegments.RemoveAt(_snakeSegments.Count - 1);
        }

        BoardVersion++;
    }

    private void EndGame()
    {
        IsRunning = false;
        IsGameOver = true;
        BoardVersion++;
        TogglePauseCommand.NotifyCanExecuteChanged();
    }

    private void SpawnFood()
    {
        if (_snakeSegments.Count >= BoardWidth * BoardHeight)
        {
            _foodPosition = Point.Zero;
            return;
        }

        while (true)
        {
            var candidate = new Point(
                _random.Next(BoardWidth),
                _random.Next(BoardHeight));
            if (!_snakeSegments.Contains(candidate))
            {
                _foodPosition = candidate;
                return;
            }
        }
    }

    private static bool IsOpposite(Direction current, Direction next)
    {
        return (current == Direction.Up && next == Direction.Down) ||
               (current == Direction.Down && next == Direction.Up) ||
               (current == Direction.Left && next == Direction.Right) ||
               (current == Direction.Right && next == Direction.Left);
    }

    private static int GetStepX(Direction direction)
    {
        return direction switch
        {
            Direction.Left => -1,
            Direction.Right => 1,
            _ => 0
        };
    }

    private static int GetStepY(Direction direction)
    {
        return direction switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };
    }

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}
