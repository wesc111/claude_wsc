using System.Windows.Input;

namespace ChessViewer;

// Avalonia has no CommandManager.RequerySuggested (WPF's automatic re-query on input events),
// so CanExecute changes must be raised explicitly — see MainViewModel.RaiseNavCommandsChanged.
public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
