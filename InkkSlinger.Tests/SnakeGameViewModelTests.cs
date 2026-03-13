using System;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SnakeGameViewModelTests
{
    [Fact]
    public void Constructor_InitializesRunningBoard()
    {
        var viewModel = new SnakeGameViewModel(new Random(7));

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.IsGameOver);
        Assert.Equal(0, viewModel.Score);
        Assert.Equal(4, viewModel.SnakeLength);
        Assert.True(viewModel.BoardVersion >= 1);
    }

    [Fact]
    public void OppositeDirectionInput_IsIgnored()
    {
        var viewModel = new SnakeGameViewModel(new Random(7));
        var originalHead = viewModel.HeadPosition;

        var handled = viewModel.HandleKeyInput(Keys.Left);
        viewModel.Advance(TimeSpan.FromMilliseconds(160));

        Assert.False(handled);
        Assert.True(viewModel.HeadPosition.X > originalHead.X);
    }

    [Fact]
    public void QuickPerpendicularTurns_AreBufferedAcrossTwoSteps()
    {
        var viewModel = new SnakeGameViewModel(new Random(7));
        var originalHead = viewModel.HeadPosition;

        var upHandled = viewModel.HandleKeyInput(Keys.Up);
        var leftHandled = viewModel.HandleKeyInput(Keys.Left);

        viewModel.Advance(TimeSpan.FromMilliseconds(160));
        var afterFirstStep = viewModel.HeadPosition;
        viewModel.Advance(TimeSpan.FromMilliseconds(160));

        Assert.True(upHandled);
        Assert.True(leftHandled);
        Assert.Equal(originalHead.X, afterFirstStep.X);
        Assert.True(afterFirstStep.Y < originalHead.Y);
        Assert.True(viewModel.HeadPosition.X < afterFirstStep.X);
        Assert.Equal(afterFirstStep.Y, viewModel.HeadPosition.Y);
    }

    [Fact]
    public void TogglePause_PreventsBoardAdvanceUntilResumed()
    {
        var viewModel = new SnakeGameViewModel(new Random(7));
        var originalVersion = viewModel.BoardVersion;

        viewModel.TogglePauseCommand.Execute(null);
        viewModel.Advance(TimeSpan.FromMilliseconds(500));

        Assert.False(viewModel.IsRunning);
        Assert.Equal(originalVersion, viewModel.BoardVersion);

        viewModel.TogglePauseCommand.Execute(null);
        viewModel.Advance(TimeSpan.FromMilliseconds(160));

        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.BoardVersion > originalVersion);
    }

    [Fact]
    public void Advance_IntoWall_EndsGame()
    {
        var viewModel = new SnakeGameViewModel(new Random(7));

        for (var index = 0; index < 32 && viewModel.IsRunning; index++)
        {
            viewModel.Advance(TimeSpan.FromMilliseconds(160));
        }

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.IsGameOver);
        Assert.False(viewModel.TogglePauseCommand.CanExecute(null));
    }
}
