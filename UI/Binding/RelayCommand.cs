namespace InkkSlinger;

public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly System.Action<object?> _execute;
    private readonly System.Func<object?, bool>? _canExecute;

    public RelayCommand(System.Action<object?> execute, System.Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event System.EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
    }
}
