namespace InkkSlinger;

public static class CommandManager
{
    static CommandManager()
    {
        FocusManager.FocusChanged += (_, _) => InvalidateRequerySuggested();
    }

    public static event System.EventHandler? RequerySuggested;

    public static void InvalidateRequerySuggested()
    {
        Dispatcher.VerifyAccess();
        RequerySuggested?.Invoke(null, System.EventArgs.Empty);
    }

    public static bool CanExecute(RoutedCommand command, object? parameter, UIElement? target)
    {
        if (target == null)
        {
            return false;
        }

        if (!TryFindBinding(command, target, out var bindingOwner, out var binding))
        {
            return false;
        }

        var args = new CanExecuteRoutedEventArgs(command, parameter, target)
        {
            CanExecute = binding.HasExecutedHandlers
        };

        binding.RaiseCanExecute(bindingOwner, args);
        return args.CanExecute;
    }

    public static void Execute(RoutedCommand command, object? parameter, UIElement? target)
    {
        if (target == null)
        {
            return;
        }

        if (!TryFindBinding(command, target, out var bindingOwner, out var binding))
        {
            return;
        }

        var canExecuteArgs = new CanExecuteRoutedEventArgs(command, parameter, target)
        {
            CanExecute = binding.HasExecutedHandlers
        };
        binding.RaiseCanExecute(bindingOwner, canExecuteArgs);

        if (!canExecuteArgs.CanExecute)
        {
            return;
        }

        if (!binding.HasExecutedHandlers)
        {
            return;
        }

        var executedArgs = new ExecutedRoutedEventArgs(command, parameter, target);
        binding.RaiseExecuted(bindingOwner, executedArgs);
    }

    private static bool TryFindBinding(
        RoutedCommand command,
        UIElement start,
        out UIElement bindingOwner,
        out CommandBinding binding)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            foreach (var candidate in current.CommandBindings)
            {
                if (ReferenceEquals(candidate.Command, command))
                {
                    bindingOwner = current;
                    binding = candidate;
                    return true;
                }
            }
        }

        bindingOwner = null!;
        binding = null!;
        return false;
    }
}
