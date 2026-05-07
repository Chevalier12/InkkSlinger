using System;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private const float LineScrollStep = DefaultLineScrollStep;

    private enum ScrollCommandKind
    {
        SetHorizontalOffset,
        SetVerticalOffset
    }

    private readonly record struct ScrollCommand(ScrollCommandKind Kind, float Value);

    public void LineUp() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, VerticalOffset - LineScrollStep);

    public void LineDown() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, VerticalOffset + LineScrollStep);

    public void LineLeft() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, HorizontalOffset - LineScrollStep);

    public void LineRight() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, HorizontalOffset + LineScrollStep);

    public void PageUp() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, VerticalOffset - MathF.Max(1f, ViewportHeight));

    public void PageDown() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, VerticalOffset + MathF.Max(1f, ViewportHeight));

    public void PageLeft() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, HorizontalOffset - MathF.Max(1f, ViewportWidth));

    public void PageRight() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, HorizontalOffset + MathF.Max(1f, ViewportWidth));

    public void ScrollToTop() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, 0f);

    public void ScrollToBottom() => EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, ScrollableHeight);

    public void ScrollToLeftEnd() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, 0f);

    public void ScrollToRightEnd() => EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, ScrollableWidth);

    public void ScrollToHome()
    {
        EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, 0f, executeImmediately: false);
        EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, 0f);
    }

    public void ScrollToEnd()
    {
        EnqueueScrollCommand(ScrollCommandKind.SetHorizontalOffset, 0f, executeImmediately: false);
        EnqueueScrollCommand(ScrollCommandKind.SetVerticalOffset, ScrollableHeight);
    }

    private void EnqueueScrollCommand(ScrollCommandKind kind, float value, bool executeImmediately = true)
    {
        _scrollCommandQueue.Enqueue(new ScrollCommand(kind, value));
        InvalidateArrange();
        if (executeImmediately)
        {
            ExecuteQueuedScrollCommands();
        }
    }

    private void ExecuteQueuedScrollCommands()
    {
        if (_isExecutingScrollCommandQueue)
        {
            return;
        }

        _isExecutingScrollCommandQueue = true;
        try
        {
            while (_scrollCommandQueue.Count > 0)
            {
                var command = _scrollCommandQueue.Dequeue();
                switch (command.Kind)
                {
                    case ScrollCommandKind.SetHorizontalOffset:
                        ScrollToHorizontalOffset(command.Value);
                        break;
                    case ScrollCommandKind.SetVerticalOffset:
                        ScrollToVerticalOffset(command.Value);
                        break;
                }
            }
        }
        finally
        {
            _isExecutingScrollCommandQueue = false;
        }
    }

    private void ExecuteQueuedScrollCommandsForLayout()
    {
        ExecuteQueuedScrollCommands();
    }

    private void HandleKeyDownFromInput(KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case Keys.Up:
                LineUp();
                args.Handled = true;
                break;
            case Keys.Down:
                LineDown();
                args.Handled = true;
                break;
            case Keys.Left:
                LineLeft();
                args.Handled = true;
                break;
            case Keys.Right:
                LineRight();
                args.Handled = true;
                break;
            case Keys.PageUp:
                PageUp();
                args.Handled = true;
                break;
            case Keys.PageDown:
                PageDown();
                args.Handled = true;
                break;
            case Keys.Home:
                ScrollToHome();
                args.Handled = true;
                break;
            case Keys.End:
                ScrollToEnd();
                args.Handled = true;
                break;
        }
    }
}
